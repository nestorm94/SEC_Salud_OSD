/*
================================================================================
 sql-fix-catalogo-areas.sql
================================================================================
 PROPÓSITO:
   Regenera usp_Catalogo_Areas_Listar resolviendo el nombre real de la columna
   Área desde sys.columns (evita errores por acentos en scripts UTF-8 mal codificados).

 BASE DE DATOS DESTINO:
   ObservatorioDB u ObservatorioDB_ASIS_Test (requiere ufn_Proyeccion_VistaDefault).

 DEPENDENCIAS (ejecutar antes):
   - Vistas de proyección DANE y función dbo.ufn_Proyeccion_VistaDefault

 ORDEN DE EJECUCIÓN:
   Independiente; alternativa: scripts\fix-catalogo-areas.ps1 (mejor para encoding).

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB -E -i scripts\sql-fix-catalogo-areas.sql
================================================================================
*/
SET NOCOUNT ON;
GO

/* --- Resolver @colArea desde sys.columns (column_id=5 o nombre con "rea") --- */
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

/* --- CREATE OR ALTER PROCEDURE: SP dinámico DISTINCT áreas con nombre resuelto --- */
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
