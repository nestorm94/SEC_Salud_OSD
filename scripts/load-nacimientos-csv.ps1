# Carga CSV nacimientos -> staging + usp_normalizar_nacimientos_casanare
param(
    [string]$Server = 'localhost\SQLEXPRESS2025',
    [string]$Database = 'ObservatorioDB_ASIS_Test',
    [string]$CsvPath = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $CsvPath) {
    $candidates = @(
        (Join-Path $PSScriptRoot 'data\nacimientos_casanare_limpio_estandarizado.csv'),
        (Join-Path $env:USERPROFILE 'Downloads\nacimientos_casanare_limpio_estandarizado (2).csv'),
        (Join-Path $env:USERPROFILE 'Downloads\nacimientos_casanare_limpio_estandarizado.csv')
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { $CsvPath = $c; break }
    }
}
if (-not (Test-Path -LiteralPath $CsvPath)) {
    Write-Error "No se encontro CSV. Use -CsvPath 'ruta\archivo.csv'"
}

Write-Host "CSV: $CsvPath" -ForegroundColor Cyan
Write-Host "BD:  $Database en $Server" -ForegroundColor Cyan

$structSql = Join-Path $PSScriptRoot 'asis-test-clone\20_fact_nacimientos_estructura.sql'
$normSql = Join-Path $PSScriptRoot 'asis-test-clone\21_usp_normalizar_nacimientos_casanare.sql'
$viewsSql = Join-Path $PSScriptRoot 'asis-test-clone\22_vistas_asis_nacimientos.sql'
$catalogEdu = Join-Path $PSScriptRoot 'asis-test-clone\24_catalogos_nacimientos_educacion_etnia.sql'

$catalogSql = Join-Path $PSScriptRoot 'asis-test-clone\19_catalogo_nacimientos_peso_semanas.sql'

foreach ($f in @($catalogSql, $catalogEdu, $structSql, $normSql)) {
    if (Test-Path $f) {
        sqlcmd -S $Server -d $Database -E -f 65001 -i $f | Out-Host
    }
}

Add-Type -AssemblyName System.Data
$connStr = "Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection $connStr
$conn.Open()

$cmd = $conn.CreateCommand()
$cmd.CommandText = 'TRUNCATE TABLE dbo.nacimientos_casanare_staging'
try { $cmd.ExecuteNonQuery() | Out-Null } catch { Write-Warning $_.Exception.Message }

$rows = Import-Csv -LiteralPath $CsvPath -Delimiter ';' -Encoding ([System.Text.UTF8Encoding]::new($false))
$dt = New-Object System.Data.DataTable
foreach ($col in @(
    'codigo_departamento','nombre_departamento','codigo_municipio','nombre_municipio','vigencia',
    'codigo_area_residencia','nombre_area_residencia','grupo_etareo_quinquenios_dane',
    'nivel_educativo','pertenencia_etnica','sexo','peso_al_nacer','semanas_gestacion','nacimientos'
)) {
    [void]$dt.Columns.Add($col)
}

foreach ($r in $rows) {
    if ([string]::IsNullOrWhiteSpace($r.vigencia)) { continue }
    $dr = $dt.NewRow()
    foreach ($col in $dt.Columns.ColumnName) {
        $v = $r.$col
        if ($col -eq 'vigencia' -or $col -eq 'nacimientos') {
            $dr[$col] = if ([string]::IsNullOrWhiteSpace($v)) { [DBNull]::Value } else { [int]$v }
        } else {
            $dr[$col] = if ([string]::IsNullOrWhiteSpace($v)) { [DBNull]::Value } else { $v }
        }
    }
    $dt.Rows.Add($dr) | Out-Null
}

$bulk = New-Object System.Data.SqlClient.SqlBulkCopy $conn
$bulk.DestinationTableName = 'dbo.nacimientos_casanare_staging'
$bulk.BatchSize = 5000
$bulk.WriteToServer($dt)
$conn.Close()

Write-Host "Staging: $($dt.Rows.Count) filas cargadas." -ForegroundColor Green

sqlcmd -S $Server -d $Database -E -f 65001 -Q "EXEC dbo.usp_normalizar_nacimientos_casanare @reemplazar = 1"
if (Test-Path $viewsSql) {
    sqlcmd -S $Server -d $Database -E -f 65001 -i $viewsSql | Out-Host
}

$fixEnc = Join-Path $PSScriptRoot 'asis-test-clone\23_fix_encoding_nacimientos.sql'
if (Test-Path $fixEnc) {
    sqlcmd -S $Server -d $Database -E -f 65001 -i $fixEnc | Out-Host
}

sqlcmd -S $Server -d $Database -E -f 65001 -Q @"
SELECT SUM(numero_nacimientos) AS total_fact FROM dbo.fact_nacimientos_casanare_normalizada;
SELECT TOP 3 * FROM dbo.vw_ASIS_Nacimientos_Total ORDER BY vigencia DESC;
"@

Write-Host 'Listo.' -ForegroundColor Green
