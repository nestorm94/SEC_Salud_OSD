/*
Corrige usp_Catalogo_Anios_Listar: usa el nombre real de la columna año desde sys.columns
(evita error por acentos al desplegar el script desde archivos UTF-8 incorrectos).
*/
SET NOCOUNT ON;
GO

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
