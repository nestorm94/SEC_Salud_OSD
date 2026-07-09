# Regenera usp_Catalogo_Areas_Listar con el nombre real de columna Área (sin depender de UTF-8 en .sql)

# Parámetros de conexión y bases de datos a actualizar
param(
    # Instancia de SQL Server
    [string]$Server = 'localhost\SQLEXPRESS2025',
    # Lista de bases de datos donde regenerar el procedimiento
    [string[]]$Databases = @('ObservatorioDB', 'ObservatorioDB_ASIS_Test')
)

$ErrorActionPreference = 'Stop'

# Procesar cada base de datos configurada
foreach ($db in $Databases) {
    $cs = "Server=$Server;Database=$db;Trusted_Connection=True;TrustServerCertificate=True"
    $cn = New-Object System.Data.SqlClient.SqlConnection $cs
    $cn.Open()
    try {
        # Obtener vista de proyección y columna de área geográfica
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = 'SELECT dbo.ufn_Proyeccion_VistaDefault()'
        $vista = [string]$cmd.ExecuteScalar()
        if ([string]::IsNullOrWhiteSpace($vista)) {
            Write-Warning "[$db] Sin vista de proyección; se omite."
            continue
        }

        $cmd.CommandText = @"
SELECT c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@v)
  AND (
    c.column_id = 5
    OR c.name LIKE N'%rea%'
    OR c.name LIKE N'%REA%'
  )
ORDER BY CASE WHEN c.column_id = 5 THEN 0 ELSE 1 END
"@
        $null = $cmd.Parameters.Clear()
        $null = $cmd.Parameters.AddWithValue('@v', $vista)
        $colArea = [string]$cmd.ExecuteScalar()
        if ([string]::IsNullOrWhiteSpace($colArea)) {
            throw "[$db] Columna Área no encontrada en $vista"
        }

        # Construcción del cuerpo dinámico del procedimiento
        $qCol = '[' + $colArea.Replace(']', ']]') + ']'
        $body = @"
DECLARE @v nvarchar(256) = dbo.ufn_Proyeccion_VistaDefault();
IF @v IS NULL
BEGIN
    SELECT TOP (0) CAST(NULL AS nvarchar(200)) AS Valor;
    RETURN;
END;

DECLARE @dyn nvarchar(max) = N'
SELECT DISTINCT LTRIM(RTRIM(CAST($qCol AS nvarchar(200)))) AS Valor
FROM ' + @v + N' WITH (NOLOCK)
WHERE $qCol IS NOT NULL
  AND LTRIM(RTRIM(CAST($qCol AS nvarchar(200)))) <> N''''
ORDER BY 1;';

EXEC sp_executesql @dyn;
"@

        # Creación o actualización del procedimiento almacenado
        $create = @"
CREATE OR ALTER PROCEDURE dbo.usp_Catalogo_Areas_Listar
AS
BEGIN
    SET NOCOUNT ON;
$body
END
"@
        $cmd.CommandText = $create
        $null = $cmd.Parameters.Clear()
        $cmd.ExecuteNonQuery() | Out-Null
        Write-Host "OK [$db]: usp_Catalogo_Areas_Listar (columna '$colArea' en $vista)"
    }
    finally {
        $cn.Close()
    }
}
