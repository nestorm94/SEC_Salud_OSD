# Regenera usp_Catalogo_Anios_Listar con el nombre real de columna año (sin depender de UTF-8 en .sql)

# Configuración y conexión a SQL Server
$ErrorActionPreference = 'Stop'
$cs = "Server=localhost\SQLEXPRESS2025;Database=ObservatorioDB;Trusted_Connection=True;TrustServerCertificate=True"
$cn = New-Object System.Data.SqlClient.SqlConnection $cs
$cn.Open()
try {
    # Obtener la vista de proyección por defecto y la columna de año (column_id = 7)
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = "SELECT dbo.ufn_Proyeccion_VistaDefault()"
    $vista = [string]$cmd.ExecuteScalar()
    if ([string]::IsNullOrWhiteSpace($vista)) { throw 'No hay vista de proyeccion por defecto.' }

    $cmd.CommandText = @"
SELECT c.name FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@v) AND c.column_id = 7
"@
    $null = $cmd.Parameters.Clear()
    $null = $cmd.Parameters.AddWithValue('@v', $vista)
    $colAnio = [string]$cmd.ExecuteScalar()
    if ([string]::IsNullOrWhiteSpace($colAnio)) { throw "Columna año no encontrada en $vista" }

    # Construcción del cuerpo del procedimiento con el nombre real de columna
    $qCol = '[' + $colAnio.Replace(']', ']]') + ']'
    $body = @"
DECLARE @min int, @max int;
SELECT @min = MIN(TRY_CONVERT(int, $qCol)), @max = MAX(TRY_CONVERT(int, $qCol))
FROM $vista WITH (NOLOCK) WHERE $qCol IS NOT NULL;
;WITH n AS (SELECT @max AS y UNION ALL SELECT y - 1 FROM n WHERE y > @min)
SELECT CAST(y AS nvarchar(10)) AS Valor FROM n OPTION (MAXRECURSION 32767);
"@

    # Creación o actualización del procedimiento almacenado
    $create = @"
CREATE OR ALTER PROCEDURE dbo.usp_Catalogo_Anios_Listar
AS
BEGIN
    SET NOCOUNT ON;
    $body
END
"@
    $cmd.CommandText = $create
    $null = $cmd.Parameters.Clear()
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host "OK: usp_Catalogo_Anios_Listar (columna $colAnio en $vista)"
}
finally { $cn.Close() }
