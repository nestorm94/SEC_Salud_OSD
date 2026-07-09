/*
================================================================================
 sql-fix-catalogo-anios.sql
================================================================================
 PROPÓSITO:
   Regenera usp_Catalogo_Anios_Listar resolviendo el nombre real de la columna
   año desde sys.columns (evita errores por acentos en scripts UTF-8 mal codificados).

 BASE DE DATOS DESTINO:
   ObservatorioDB u ObservatorioDB_ASIS_Test (requiere ufn_Proyeccion_VistaDefault).

 DEPENDENCIAS (ejecutar antes):
   - Vistas de proyección DANE y función dbo.ufn_Proyeccion_VistaDefault

 ORDEN DE EJECUCIÓN:
   Independiente; ejecutar cuando el catálogo de años falle por nombre de columna.

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB -E -i scripts\sql-fix-catalogo-anios.sql
================================================================================
*/
SET NOCOUNT ON;
GO

/* --- Resolver @colAnio desde sys.columns de la vista de proyección default --- */
DECLARE @vista nvarchar(256) = dbo.ufn_Proyeccion_VistaDefault();
DECLARE @colAnio sysname;

IF @vista IS NOT NULL
BEGIN
    SELECT @colAnio = c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(@vista)
      AND c.column_id = (
          SELECT MIN(c2.column_id)
          FROM sys.columns c2
          WHERE c2.object_id = OBJECT_ID(@vista)
            AND (c2.name LIKE N'%Año%' OR c2.name LIKE N'%Ano%' OR c2.name LIKE N'%ano%' OR c2.name LIKE N'%A%o%')
      );
END

IF @colAnio IS NULL
    SET @colAnio = N'Año';

/* --- CREATE OR ALTER PROCEDURE: SP dinámico con nombre de columna resuelto --- */
DECLARE @sql nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.usp_Catalogo_Anios_Listar
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @v nvarchar(256) = dbo.ufn_Proyeccion_VistaDefault();
    IF @v IS NULL
    BEGIN
        SELECT TOP (0) CAST(NULL AS nvarchar(10)) AS Valor;
        RETURN;
    END;

    DECLARE @dyn nvarchar(max) = N''
DECLARE @min int, @max int;
SELECT @min = MIN(TRY_CONVERT(int, [' + QUOTENAME(@colAnio) + N'])),
       @max = MAX(TRY_CONVERT(int, [' + QUOTENAME(@colAnio) + N']))
FROM '' + @v + N'' WITH (NOLOCK)
WHERE [' + QUOTENAME(@colAnio) + N'] IS NOT NULL;

;WITH n AS (
    SELECT @max AS y
    UNION ALL
    SELECT y - 1 FROM n WHERE y > @min
)
SELECT CAST(y AS nvarchar(10)) AS Valor
FROM n
OPTION (MAXRECURSION 32767);'';

    EXEC sp_executesql @dyn;
END
GO

PRINT N'usp_Catalogo_Anios_Listar actualizado.';
GO
