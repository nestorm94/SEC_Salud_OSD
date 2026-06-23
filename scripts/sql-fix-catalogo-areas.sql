/*
Corrige usp_Catalogo_Areas_Listar: usa el nombre real de la columna Área desde sys.columns
(evita error por acentos al desplegar el script desde archivos UTF-8 incorrectos).

Preferir: .\scripts\fix-catalogo-areas.ps1 (lee el nombre real sin problemas de codificación).
*/
SET NOCOUNT ON;
GO

DECLARE @vista nvarchar(256) = dbo.ufn_Proyeccion_VistaDefault();
DECLARE @colArea sysname;

IF @vista IS NOT NULL
BEGIN
    SELECT TOP (1) @colArea = c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(@vista)
      AND (c.column_id = 5 OR c.name LIKE N'%rea%' OR c.name LIKE N'%REA%')
    ORDER BY CASE WHEN c.column_id = 5 THEN 0 ELSE 1 END;
END

IF @colArea IS NULL
    SET @colArea = N'Área';

DECLARE @sql nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.usp_Catalogo_Areas_Listar
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @v nvarchar(256) = dbo.ufn_Proyeccion_VistaDefault();
    IF @v IS NULL
    BEGIN
        SELECT TOP (0) CAST(NULL AS nvarchar(200)) AS Valor;
        RETURN;
    END;

    DECLARE @dyn nvarchar(max) = N''
SELECT DISTINCT LTRIM(RTRIM(CAST([' + QUOTENAME(@colArea) + N'] AS nvarchar(200)))) AS Valor
FROM '' + @v + N'' WITH (NOLOCK)
WHERE [' + QUOTENAME(@colArea) + N'] IS NOT NULL
  AND LTRIM(RTRIM(CAST([' + QUOTENAME(@colArea) + N'] AS nvarchar(200)))) <> N''''''''
ORDER BY 1;'';

    EXEC sp_executesql @dyn;
END
GO';

EXEC sp_executesql @sql;
GO

PRINT N'usp_Catalogo_Areas_Listar actualizado.';
GO
