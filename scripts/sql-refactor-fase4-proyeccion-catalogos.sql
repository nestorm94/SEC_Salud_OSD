/*
FASE 4 - Proyeccion de poblacion y catalogos.
Vistas/SP para lecturas; logica de filtros y paginacion centralizada en SQL Server.
*/
SET NOCOUNT ON;
GO

/* =========================
   Helper: primera vista de poblacion disponible
   ========================= */
CREATE OR ALTER FUNCTION dbo.ufn_Proyeccion_VistaDefault()
RETURNS nvarchar(256)
AS
BEGIN
    DECLARE @v nvarchar(256) = NULL;
    IF OBJECT_ID(N'dbo.vw_Poblacion_Nacional_Casanare', N'V') IS NOT NULL
        SET @v = N'dbo.vw_Poblacion_Nacional_Casanare';
    ELSE IF OBJECT_ID(N'dbo.vw_Reporte_Poblacion_CursoVida_Unificado', N'V') IS NOT NULL
        SET @v = N'dbo.vw_Reporte_Poblacion_CursoVida_Unificado';
    ELSE IF OBJECT_ID(N'dbo.vw_Reporte_Poblacion_Quinquenios_Unificado', N'V') IS NOT NULL
        SET @v = N'dbo.vw_Reporte_Poblacion_Quinquenios_Unificado';
    RETURN @v;
END;
GO

/* =========================
   CATALOGOS
   ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_Catalogo_Departamentos_Listar
AS
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID(N'dbo.dim_departamento', N'U') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.dim_departamento', N'estado') IS NOT NULL
            SELECT LTRIM(RTRIM(CAST(cod_departamento AS nvarchar(10)))) AS CodigoDane,
                   LTRIM(RTRIM(CAST(nombre_departamento AS nvarchar(300)))) AS Nombre
            FROM dbo.dim_departamento
            WHERE (estado = 1 OR estado IS NULL)
            ORDER BY 2;
        ELSE
            SELECT LTRIM(RTRIM(CAST(cod_departamento AS nvarchar(10)))) AS CodigoDane,
                   LTRIM(RTRIM(CAST(nombre_departamento AS nvarchar(300)))) AS Nombre
            FROM dbo.dim_departamento
            ORDER BY 2;
        RETURN;
    END;

    IF OBJECT_ID(N'dbo.dim_departamentos', N'U') IS NOT NULL
    BEGIN
        SELECT LTRIM(RTRIM(CAST(codigo_departamento AS nvarchar(10)))) AS CodigoDane,
               LTRIM(RTRIM(CAST(nombre_departamento AS nvarchar(300)))) AS Nombre
        FROM dbo.dim_departamentos
        ORDER BY 2;
        RETURN;
    END;

    SELECT TOP (0)
        CAST(NULL AS nvarchar(10)) AS CodigoDane,
        CAST(NULL AS nvarchar(300)) AS Nombre;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Catalogo_Municipios_Listar
    @CodigoDepartamento nvarchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @dep nvarchar(10) = NULLIF(LTRIM(RTRIM(@CodigoDepartamento)), N'');

    IF OBJECT_ID(N'dbo.dim_municipio', N'U') IS NOT NULL
    BEGIN
        DECLARE @colCod sysname = NULL;
        DECLARE @colDep sysname = NULL;
        IF COL_LENGTH(N'dbo.dim_municipio', N'codigo_dane') IS NOT NULL SET @colCod = N'codigo_dane';
        ELSE IF COL_LENGTH(N'dbo.dim_municipio', N'codigo_municipio') IS NOT NULL SET @colCod = N'codigo_municipio';
        ELSE IF COL_LENGTH(N'dbo.dim_municipio', N'cod_municipio') IS NOT NULL SET @colCod = N'cod_municipio';

        IF COL_LENGTH(N'dbo.dim_municipio', N'cod_departamento') IS NOT NULL SET @colDep = N'cod_departamento';
        ELSE IF COL_LENGTH(N'dbo.dim_municipio', N'codigo_departamento') IS NOT NULL SET @colDep = N'codigo_departamento';

        IF @colCod IS NOT NULL AND @colDep IS NOT NULL
        BEGIN
            DECLARE @sql nvarchar(max) = N'
SELECT LTRIM(RTRIM(CAST(' + QUOTENAME(@colCod) + N' AS nvarchar(10)))) AS CodigoDaneMunicipio,
       LTRIM(RTRIM(CAST(' + QUOTENAME(@colDep) + N' AS nvarchar(10)))) AS CodigoDepartamento,
       LTRIM(RTRIM(CAST(nombre_municipio AS nvarchar(300)))) AS NombreMunicipio,
       ' + CASE WHEN COL_LENGTH(N'dbo.dim_municipio', N'regional') IS NOT NULL
                THEN N'LTRIM(RTRIM(CAST(ISNULL(regional, '''') AS nvarchar(200))))'
                ELSE N'CAST(N'''' AS nvarchar(200))' END + N' AS Regional
FROM dbo.dim_municipio
WHERE 1=1';

            IF COL_LENGTH(N'dbo.dim_municipio', N'estado') IS NOT NULL
                SET @sql += N' AND (estado = 1 OR estado IS NULL)';
            IF @dep IS NOT NULL
                SET @sql += N' AND LTRIM(RTRIM(CAST(' + QUOTENAME(@colDep) + N' AS nvarchar(10)))) = @dep';
            SET @sql += N' ORDER BY 3;';

            EXEC sp_executesql @sql, N'@dep nvarchar(10)', @dep = @dep;
            RETURN;
        END;
    END;

    IF OBJECT_ID(N'dbo.dim_municipios', N'U') IS NOT NULL
    BEGIN
        DECLARE @colCod2 sysname = NULL;
        DECLARE @colDep2 sysname = NULL;
        IF COL_LENGTH(N'dbo.dim_municipios', N'codigo_dane') IS NOT NULL SET @colCod2 = N'codigo_dane';
        ELSE IF COL_LENGTH(N'dbo.dim_municipios', N'codigo_municipio') IS NOT NULL SET @colCod2 = N'codigo_municipio';
        ELSE IF COL_LENGTH(N'dbo.dim_municipios', N'cod_municipio') IS NOT NULL SET @colCod2 = N'cod_municipio';

        IF COL_LENGTH(N'dbo.dim_municipios', N'cod_departamento') IS NOT NULL SET @colDep2 = N'cod_departamento';
        ELSE IF COL_LENGTH(N'dbo.dim_municipios', N'codigo_departamento') IS NOT NULL SET @colDep2 = N'codigo_departamento';

        IF @colCod2 IS NOT NULL AND @colDep2 IS NOT NULL
        BEGIN
            DECLARE @sql2 nvarchar(max) = N'
SELECT LTRIM(RTRIM(CAST(' + QUOTENAME(@colCod2) + N' AS nvarchar(10)))) AS CodigoDaneMunicipio,
       LTRIM(RTRIM(CAST(' + QUOTENAME(@colDep2) + N' AS nvarchar(10)))) AS CodigoDepartamento,
       LTRIM(RTRIM(CAST(nombre_municipio AS nvarchar(300)))) AS NombreMunicipio,
       ' + CASE WHEN COL_LENGTH(N'dbo.dim_municipios', N'regional') IS NOT NULL
                THEN N'LTRIM(RTRIM(CAST(ISNULL(regional, '''') AS nvarchar(200))))'
                ELSE N'CAST(N'''' AS nvarchar(200))' END + N' AS Regional
FROM dbo.dim_municipios
WHERE 1=1';

            IF COL_LENGTH(N'dbo.dim_municipios', N'estado') IS NOT NULL
                SET @sql2 += N' AND (estado = 1 OR estado IS NULL)';
            IF @dep IS NOT NULL
                SET @sql2 += N' AND LTRIM(RTRIM(CAST(' + QUOTENAME(@colDep2) + N' AS nvarchar(10)))) = @dep';
            SET @sql2 += N' ORDER BY 3;';

            EXEC sp_executesql @sql2, N'@dep nvarchar(10)', @dep = @dep;
            RETURN;
        END;
    END;

    SELECT TOP (0)
        CAST(NULL AS nvarchar(10)) AS CodigoDaneMunicipio,
        CAST(NULL AS nvarchar(10)) AS CodigoDepartamento,
        CAST(NULL AS nvarchar(300)) AS NombreMunicipio,
        CAST(NULL AS nvarchar(200)) AS Regional;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Catalogo_Regionales_Listar
AS
BEGIN
    SET NOCOUNT ON;

  /* SQL dinámico: evita error de compilación si dim_* existe sin columna regional */
    IF OBJECT_ID(N'dbo.dim_municipio', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.dim_municipio', N'regional') IS NOT NULL
    BEGIN
        EXEC sp_executesql N'
SELECT DISTINCT LTRIM(RTRIM(CAST(regional AS nvarchar(200)))) AS Valor
FROM dbo.dim_municipio
WHERE ISNULL(LTRIM(RTRIM(CAST(regional AS nvarchar(200)))), N'''') <> N''''
ORDER BY 1;';
        RETURN;
    END;

    IF OBJECT_ID(N'dbo.dim_municipios', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.dim_municipios', N'regional') IS NOT NULL
    BEGIN
        EXEC sp_executesql N'
SELECT DISTINCT LTRIM(RTRIM(CAST(regional AS nvarchar(200)))) AS Valor
FROM dbo.dim_municipios
WHERE ISNULL(LTRIM(RTRIM(CAST(regional AS nvarchar(200)))), N'''') <> N''''
ORDER BY 1;';
        RETURN;
    END;

    DECLARE @vista nvarchar(256) = dbo.ufn_Proyeccion_VistaDefault();
    IF @vista IS NULL
    BEGIN
        SELECT TOP (0) CAST(NULL AS nvarchar(200)) AS Valor;
        RETURN;
    END;

    DECLARE @sql nvarchar(max) = N'
SELECT DISTINCT LTRIM(RTRIM(CAST([Regional] AS nvarchar(200)))) AS Valor
FROM ' + @vista + N' WITH (NOLOCK)
WHERE [Regional] IS NOT NULL
  AND LTRIM(RTRIM(CAST([Regional] AS nvarchar(200)))) <> N''''
ORDER BY 1;';
    EXEC sp_executesql @sql;
END;
GO

/* Área: nombre de columna resuelto en tiempo de despliegue (ver scripts/fix-catalogo-areas.ps1). */
DECLARE @vistaArea nvarchar(256) = dbo.ufn_Proyeccion_VistaDefault();
DECLARE @colArea sysname;
IF @vistaArea IS NOT NULL
    SELECT TOP (1) @colArea = c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(@vistaArea)
      AND (c.column_id = 5 OR c.name LIKE N'%rea%' OR c.name LIKE N'%REA%')
    ORDER BY CASE WHEN c.column_id = 5 THEN 0 ELSE 1 END;
IF @colArea IS NULL SET @colArea = N'Área';

DECLARE @sqlArea nvarchar(max) = N'
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
END';
EXEC sp_executesql @sqlArea;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Catalogo_Sexos_Listar
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @vista nvarchar(256) = dbo.ufn_Proyeccion_VistaDefault();
    IF @vista IS NULL
    BEGIN
        SELECT TOP (0) CAST(NULL AS nvarchar(200)) AS Valor;
        RETURN;
    END;

    DECLARE @sql nvarchar(max) = N'
SELECT DISTINCT LTRIM(RTRIM(CAST([Sexo] AS nvarchar(200)))) AS Valor
FROM ' + @vista + N' WITH (NOLOCK)
WHERE [Sexo] IS NOT NULL
  AND LTRIM(RTRIM(CAST([Sexo] AS nvarchar(200)))) <> N''''
ORDER BY 1;';
    EXEC sp_executesql @sql;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Catalogo_Anios_Listar
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @vista nvarchar(256) = dbo.ufn_Proyeccion_VistaDefault();
    IF @vista IS NULL
    BEGIN
        SELECT TOP (0) CAST(NULL AS int) AS Anio;
        RETURN;
    END;

    DECLARE @sql nvarchar(max) = N'
DECLARE @min int, @max int;
SELECT @min = MIN(TRY_CONVERT(int, [Año])), @max = MAX(TRY_CONVERT(int, [Año]))
FROM ' + @vista + N' WITH (NOLOCK)
WHERE [Año] IS NOT NULL;

;WITH n AS (
    SELECT @max AS y
    UNION ALL
    SELECT y - 1 FROM n WHERE y > @min
)
SELECT CAST(y AS nvarchar(10)) AS Valor
FROM n
OPTION (MAXRECURSION 32767);';

    EXEC sp_executesql @sql;
END;
GO

/* =========================
   PROYECCION PAGINADA
   ========================= */
CREATE OR ALTER PROCEDURE dbo.usp_ProyeccionPoblacion_ConsultarPaginado
    @Clave nvarchar(50),
    @Pagina int = 1,
    @TamanoPagina int = 50,
    @Territorio nvarchar(400) = NULL,
    @Regional nvarchar(200) = NULL,
    @Area nvarchar(200) = NULL,
    @Sexo nvarchar(200) = NULL,
    @Anio int = NULL,
    @CodigoDepartamento nvarchar(10) = NULL,
    @CodigoMunicipio nvarchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @vista nvarchar(256) = NULL;
    IF @Clave = N'nacional-casanare' AND OBJECT_ID(N'dbo.vw_Poblacion_Nacional_Casanare', N'V') IS NOT NULL
        SET @vista = N'dbo.vw_Poblacion_Nacional_Casanare';
    ELSE IF @Clave = N'curso-vida' AND OBJECT_ID(N'dbo.vw_Reporte_Poblacion_CursoVida_Unificado', N'V') IS NOT NULL
        SET @vista = N'dbo.vw_Reporte_Poblacion_CursoVida_Unificado';
    ELSE IF @Clave = N'quinquenios' AND OBJECT_ID(N'dbo.vw_Reporte_Poblacion_Quinquenios_Unificado', N'V') IS NOT NULL
        SET @vista = N'dbo.vw_Reporte_Poblacion_Quinquenios_Unificado';

    IF @vista IS NULL
    BEGIN
        SELECT CAST(0 AS bigint) AS TotalFilas;
        SELECT TOP (0) * FROM (SELECT CAST(NULL AS int) AS dummy) d;
        RETURN;
    END;

    SET @Pagina = CASE WHEN @Pagina < 1 THEN 1 ELSE @Pagina END;
    SET @TamanoPagina = CASE WHEN @TamanoPagina < 1 THEN 1 WHEN @TamanoPagina > 200 THEN 200 ELSE @TamanoPagina END;

    DECLARE @where nvarchar(max) = N' WHERE 1=1';
    DECLARE @pTerritorio nvarchar(400) = NULLIF(LTRIM(RTRIM(@Territorio)), N'');
    DECLARE @pRegional nvarchar(200) = NULLIF(LTRIM(RTRIM(@Regional)), N'');
    DECLARE @pArea nvarchar(200) = NULLIF(LTRIM(RTRIM(@Area)), N'');
    DECLARE @pSexo nvarchar(200) = NULLIF(LTRIM(RTRIM(@Sexo)), N'');
    DECLARE @pCodDep nvarchar(10) = NULLIF(LTRIM(RTRIM(@CodigoDepartamento)), N'');
    DECLARE @pCodMun nvarchar(10) = NULLIF(LTRIM(RTRIM(@CodigoMunicipio)), N'');

    IF @pTerritorio IS NOT NULL
    BEGIN
        IF CHARINDEX(N'%', @pTerritorio) > 0 OR CHARINDEX(N'_', @pTerritorio) > 0
            SET @where += N' AND [Territorio] LIKE @pTerritorio';
        ELSE
            SET @where += N' AND [Territorio] = @pTerritorio';
    END;

    IF @pRegional IS NOT NULL SET @where += N' AND [Regional] = @pRegional';
    IF @pArea IS NOT NULL SET @where += N' AND [Área] = @pArea';
    IF @pSexo IS NOT NULL SET @where += N' AND [Sexo] = @pSexo';
    IF @Anio IS NOT NULL SET @where += N' AND [Año] = @pAnio';

    IF @pCodDep IS NOT NULL
    BEGIN
        DECLARE @unionDep nvarchar(max) = N'';
        IF OBJECT_ID(N'dbo.dim_municipio', N'U') IS NOT NULL
        BEGIN
            DECLARE @cd1 sysname = NULL;
            IF COL_LENGTH(N'dbo.dim_municipio', N'cod_departamento') IS NOT NULL SET @cd1 = N'cod_departamento';
            ELSE IF COL_LENGTH(N'dbo.dim_municipio', N'codigo_departamento') IS NOT NULL SET @cd1 = N'codigo_departamento';
            IF @cd1 IS NOT NULL
                SET @unionDep += CASE WHEN LEN(@unionDep) > 0 THEN N' UNION ' ELSE N'' END +
                    N'SELECT LTRIM(RTRIM(CAST(m.nombre_municipio AS nvarchar(300)))) FROM dbo.dim_municipio m WHERE LTRIM(RTRIM(CAST(m.' + QUOTENAME(@cd1) + N' AS nvarchar(10)))) = @pCodDep';
        END;
        IF OBJECT_ID(N'dbo.dim_municipios', N'U') IS NOT NULL
        BEGIN
            DECLARE @cd2 sysname = NULL;
            IF COL_LENGTH(N'dbo.dim_municipios', N'cod_departamento') IS NOT NULL SET @cd2 = N'cod_departamento';
            ELSE IF COL_LENGTH(N'dbo.dim_municipios', N'codigo_departamento') IS NOT NULL SET @cd2 = N'codigo_departamento';
            IF @cd2 IS NOT NULL
                SET @unionDep += CASE WHEN LEN(@unionDep) > 0 THEN N' UNION ' ELSE N'' END +
                    N'SELECT LTRIM(RTRIM(CAST(m.nombre_municipio AS nvarchar(300)))) FROM dbo.dim_municipios m WHERE LTRIM(RTRIM(CAST(m.' + QUOTENAME(@cd2) + N' AS nvarchar(10)))) = @pCodDep';
        END;
        IF OBJECT_ID(N'dbo.dim_departamento', N'U') IS NOT NULL
            SET @unionDep += CASE WHEN LEN(@unionDep) > 0 THEN N' UNION ' ELSE N'' END +
                N'SELECT LTRIM(RTRIM(CAST(d.nombre_departamento AS nvarchar(300)))) FROM dbo.dim_departamento d WHERE LTRIM(RTRIM(CAST(d.cod_departamento AS nvarchar(10)))) = @pCodDep';
        IF OBJECT_ID(N'dbo.dim_departamentos', N'U') IS NOT NULL
            SET @unionDep += CASE WHEN LEN(@unionDep) > 0 THEN N' UNION ' ELSE N'' END +
                N'SELECT LTRIM(RTRIM(CAST(d.nombre_departamento AS nvarchar(300)))) FROM dbo.dim_departamentos d WHERE LTRIM(RTRIM(CAST(d.codigo_departamento AS nvarchar(10)))) = @pCodDep';
        IF LEN(@unionDep) > 0
            SET @where += N' AND [Territorio] IN (' + @unionDep + N')';
    END;

    IF @pCodMun IS NOT NULL
    BEGIN
        DECLARE @unionMun nvarchar(max) = N'';
        IF OBJECT_ID(N'dbo.dim_municipio', N'U') IS NOT NULL
        BEGIN
            DECLARE @cm1 sysname = NULL;
            IF COL_LENGTH(N'dbo.dim_municipio', N'codigo_dane') IS NOT NULL SET @cm1 = N'codigo_dane';
            ELSE IF COL_LENGTH(N'dbo.dim_municipio', N'codigo_municipio') IS NOT NULL SET @cm1 = N'codigo_municipio';
            ELSE IF COL_LENGTH(N'dbo.dim_municipio', N'cod_municipio') IS NOT NULL SET @cm1 = N'cod_municipio';
            IF @cm1 IS NOT NULL
                SET @unionMun += CASE WHEN LEN(@unionMun) > 0 THEN N' UNION ' ELSE N'' END +
                    N'SELECT LTRIM(RTRIM(CAST(m.nombre_municipio AS nvarchar(300)))) FROM dbo.dim_municipio m WHERE LTRIM(RTRIM(CAST(m.' + QUOTENAME(@cm1) + N' AS nvarchar(10)))) = @pCodMun';
        END;
        IF OBJECT_ID(N'dbo.dim_municipios', N'U') IS NOT NULL
        BEGIN
            DECLARE @cm2 sysname = NULL;
            IF COL_LENGTH(N'dbo.dim_municipios', N'codigo_dane') IS NOT NULL SET @cm2 = N'codigo_dane';
            ELSE IF COL_LENGTH(N'dbo.dim_municipios', N'codigo_municipio') IS NOT NULL SET @cm2 = N'codigo_municipio';
            ELSE IF COL_LENGTH(N'dbo.dim_municipios', N'cod_municipio') IS NOT NULL SET @cm2 = N'cod_municipio';
            IF @cm2 IS NOT NULL
                SET @unionMun += CASE WHEN LEN(@unionMun) > 0 THEN N' UNION ' ELSE N'' END +
                    N'SELECT LTRIM(RTRIM(CAST(m.nombre_municipio AS nvarchar(300)))) FROM dbo.dim_municipios m WHERE LTRIM(RTRIM(CAST(m.' + QUOTENAME(@cm2) + N' AS nvarchar(10)))) = @pCodMun';
        END;
        IF LEN(@unionMun) > 0
            SET @where += N' AND [Territorio] IN (' + @unionMun + N')';
    END;

    DECLARE @offset int = (@Pagina - 1) * @TamanoPagina;
    DECLARE @paramList nvarchar(500) = N'@pTerritorio nvarchar(400), @pRegional nvarchar(200), @pArea nvarchar(200), @pSexo nvarchar(200), @pAnio int, @pCodDep nvarchar(10), @pCodMun nvarchar(10)';
    DECLARE @sqlCount nvarchar(max) = N'SELECT COUNT_BIG(*) FROM ' + @vista + @where;
    DECLARE @sqlData nvarchar(max) = N'
SELECT * FROM ' + @vista + @where + N'
ORDER BY (SELECT NULL)
OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;';

    CREATE TABLE #t (TotalFilas bigint);
    INSERT INTO #t
    EXEC sp_executesql
        @sqlCount,
        @paramList,
        @pTerritorio = @pTerritorio,
        @pRegional = @pRegional,
        @pArea = @pArea,
        @pSexo = @pSexo,
        @pAnio = @Anio,
        @pCodDep = @pCodDep,
        @pCodMun = @pCodMun;

    SELECT TotalFilas FROM #t;

    EXEC sp_executesql
        @sqlData,
        N'@pTerritorio nvarchar(400), @pRegional nvarchar(200), @pArea nvarchar(200), @pSexo nvarchar(200), @pAnio int, @pCodDep nvarchar(10), @pCodMun nvarchar(10), @offset int, @take int',
        @pTerritorio = @pTerritorio,
        @pRegional = @pRegional,
        @pArea = @pArea,
        @pSexo = @pSexo,
        @pAnio = @Anio,
        @pCodDep = @pCodDep,
        @pCodMun = @pCodMun,
        @offset = @offset,
        @take = @TamanoPagina;
END;
GO
