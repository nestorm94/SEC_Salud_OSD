/*
Vistas ASIS en paralelo: legacy (API) + fact, sin DROP de objetos existentes.
Restaura vw_ASIS_Poblacion_CursoVida y GrupoEdad al contrato legacy (fase7).
Publica equivalentes _Fact para comparacion.

SOLO ObservatorioDB_ASIS_Test.

Ejecutar:
  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\16_vistas_paralelas_legacy_fact.sql
*/
SET NOCOUNT ON;
GO

IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Solo ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

PRINT N'=== VISTAS PARALELAS LEGACY / FACT ===';
GO

/* --- Respaldo y restauracion CursoVida / GrupoEdad (contrato API fase7) --- */
/* Nombres de columna con acentos resueltos en runtime desde sys.columns */

DECLARE @oidQ int = OBJECT_ID(N'dbo.vw_Reporte_Poblacion_Quinquenios_Unificado');
DECLARE @oidC int = OBJECT_ID(N'dbo.vw_Reporte_Poblacion_CursoVida_Unificado');
DECLARE @qCod sysname, @qSexo sysname, @qArea sysname, @qAno sysname, @qQuin sysname, @qPob sysname;
DECLARE @cCod sysname, @cSexo sysname, @cArea sysname, @cAno sysname, @cCurso sysname, @cPob sysname;
DECLARE @sql nvarchar(max);

SELECT @qCod = MAX(CASE WHEN column_id = 1 THEN name END),
       @qSexo = MAX(CASE WHEN column_id = 6 THEN name END),
       @qArea = MAX(CASE WHEN column_id = 5 THEN name END),
       @qAno = MAX(CASE WHEN column_id = 7 THEN name END),
       @qQuin = MAX(CASE WHEN column_id = 8 THEN name END),
       @qPob = MAX(CASE WHEN column_id = 9 THEN name END)
FROM sys.columns WHERE object_id = @oidQ;

SELECT @cCod = MAX(CASE WHEN column_id = 1 THEN name END),
       @cSexo = MAX(CASE WHEN column_id = 6 THEN name END),
       @cArea = MAX(CASE WHEN column_id = 5 THEN name END),
       @cAno = MAX(CASE WHEN column_id = 7 THEN name END),
       @cCurso = MAX(CASE WHEN column_id = 8 THEN name END),
       @cPob = MAX(CASE WHEN column_id = 10 THEN name END)
FROM sys.columns WHERE object_id = @oidC;

SET @sql = N'
CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_GrupoEdad_Legacy
AS
SELECT
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    LTRIM(RTRIM(CAST(q.' + QUOTENAME(@qCod) + N' AS nvarchar(10)))) AS codigo_territorio_dane,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(q.' + QUOTENAME(@qCod) + N' AS nvarchar(10))))) = 5
         THEN mu.codigo_dane ELSE NULL END AS codigo_municipio,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(q.' + QUOTENAME(@qCod) + N' AS nvarchar(10))))) = 5
         THEN mu.nombre_municipio ELSE de.nombre_departamento END AS nombre_territorio,
    q.' + QUOTENAME(@qQuin) + N' AS grupo_quinquenal,
    dg.id_grupo_edad,
    dg.codigo AS codigo_grupo_edad_dim,
    dg.nombre_grupo_edad,
    CAST(q.' + QUOTENAME(@qAno) + N' AS int) AS vigencia,
    CAST(SUM(CAST(q.' + QUOTENAME(@qPob) + N' AS decimal(18, 2))) AS bigint) AS poblacion,
    N''vw_Reporte_Poblacion_Quinquenios_Unificado'' AS fuente_datos,
    N''Quinquenios DANE; Sexo=Total; Area=Total'' AS criterio_agregacion
FROM dbo.vw_Reporte_Poblacion_Quinquenios_Unificado AS q
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N''85''
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = LTRIM(RTRIM(CAST(q.' + QUOTENAME(@qCod) + N' AS nvarchar(10))))
   AND mu.cod_departamento = N''85''
LEFT JOIN dbo.dim_grupo_edad AS dg
    ON q.' + QUOTENAME(@qQuin) + N' LIKE N''%'' + dg.etiqueta_rango + N''%'' COLLATE Latin1_General_CI_AI
    OR q.' + QUOTENAME(@qQuin) + N' LIKE N''%'' + dg.nombre_grupo_edad + N''%'' COLLATE Latin1_General_CI_AI
WHERE (LTRIM(RTRIM(CAST(q.' + QUOTENAME(@qCod) + N' AS nvarchar(10)))) = N''85''
    OR (LEN(LTRIM(RTRIM(CAST(q.' + QUOTENAME(@qCod) + N' AS nvarchar(10))))) = 5
        AND LEFT(LTRIM(RTRIM(CAST(q.' + QUOTENAME(@qCod) + N' AS nvarchar(10)))), 2) = N''85''))
  AND q.' + QUOTENAME(@qSexo) + N' = N''Total''
  AND q.' + QUOTENAME(@qArea) + N' = N''Total''
GROUP BY de.cod_departamento, de.nombre_departamento,
    LTRIM(RTRIM(CAST(q.' + QUOTENAME(@qCod) + N' AS nvarchar(10)))),
    mu.codigo_dane, mu.nombre_municipio, q.' + QUOTENAME(@qQuin) + N', dg.id_grupo_edad, dg.codigo, dg.nombre_grupo_edad, CAST(q.' + QUOTENAME(@qAno) + N' AS int);';
EXEC sys.sp_executesql @sql;

SET @sql = N'
CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_CursoVida_Legacy
AS
SELECT
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    LTRIM(RTRIM(CAST(c.' + QUOTENAME(@cCod) + N' AS nvarchar(10)))) AS codigo_territorio_dane,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(c.' + QUOTENAME(@cCod) + N' AS nvarchar(10))))) = 5
         THEN mu.codigo_dane ELSE NULL END AS codigo_municipio,
    CASE WHEN LEN(LTRIM(RTRIM(CAST(c.' + QUOTENAME(@cCod) + N' AS nvarchar(10))))) = 5
         THEN mu.nombre_municipio ELSE de.nombre_departamento END AS nombre_territorio,
    c.' + QUOTENAME(@cCurso) + N' AS curso_vida_proyeccion,
    dc.id_curso_vida,
    dc.codigo AS codigo_curso_vida_dim,
    dc.nombre_curso_vida,
    CAST(c.' + QUOTENAME(@cAno) + N' AS int) AS vigencia,
    CAST(SUM(CAST(c.' + QUOTENAME(@cPob) + N' AS decimal(18, 2))) AS bigint) AS poblacion,
    N''vw_Reporte_Poblacion_CursoVida_Unificado'' AS fuente_datos,
    N''Curso de vida; Sexo=Total; Area=Total'' AS criterio_agregacion
FROM dbo.vw_Reporte_Poblacion_CursoVida_Unificado AS c
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N''85''
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = LTRIM(RTRIM(CAST(c.' + QUOTENAME(@cCod) + N' AS nvarchar(10))))
   AND mu.cod_departamento = N''85''
LEFT JOIN dbo.dim_curso_vida AS dc
    ON c.' + QUOTENAME(@cCurso) + N' LIKE N''%'' + dc.nombre_curso_vida + N''%'' COLLATE Latin1_General_CI_AI
WHERE (LTRIM(RTRIM(CAST(c.' + QUOTENAME(@cCod) + N' AS nvarchar(10)))) = N''85''
    OR (LEN(LTRIM(RTRIM(CAST(c.' + QUOTENAME(@cCod) + N' AS nvarchar(10))))) = 5
        AND LEFT(LTRIM(RTRIM(CAST(c.' + QUOTENAME(@cCod) + N' AS nvarchar(10)))), 2) = N''85''))
  AND c.' + QUOTENAME(@cSexo) + N' = N''Total''
  AND c.' + QUOTENAME(@cArea) + N' = N''Total''
GROUP BY de.cod_departamento, de.nombre_departamento,
    LTRIM(RTRIM(CAST(c.' + QUOTENAME(@cCod) + N' AS nvarchar(10)))),
    mu.codigo_dane, mu.nombre_municipio, c.' + QUOTENAME(@cCurso) + N', dc.id_curso_vida, dc.codigo, dc.nombre_curso_vida, CAST(c.' + QUOTENAME(@cAno) + N' AS int);';
EXEC sys.sp_executesql @sql;
GO

/* Restaurar nombres API a legacy */
CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_GrupoEdad
AS
SELECT * FROM dbo.vw_ASIS_Poblacion_GrupoEdad_Legacy;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_CursoVida
AS
SELECT * FROM dbo.vw_ASIS_Poblacion_CursoVida_Legacy;
GO

/* --- Vistas FACT con forma API (incluye id_proyeccion_dane para versionamiento) --- */

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Total_Fact
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    f.anio AS vigencia,
    CAST(SUM(f.poblacion) AS bigint) AS poblacion,
    N'fact_poblacion_proyeccion' AS fuente_datos,
    N'Dept 85; TOTAL_SEXO; Urbano+Rural; M+F' AS criterio_agregacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
    AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
    AND da.area_normalizada IN (N'Urbano', N'Rural')
WHERE f.cod_departamento = N'85'
  AND f.nivel_territorial = N'DEPARTAMENTO'
  AND f.tipo_registro = N'TOTAL_SEXO'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, de.cod_departamento, de.nombre_departamento, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Municipio_Fact
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    mu.codigo_dane AS codigo_municipio,
    mu.nombre_municipio,
    CAST(NULL AS nvarchar(100)) AS regional,
    f.anio AS vigencia,
    CAST(SUM(f.poblacion) AS bigint) AS poblacion,
    N'fact_poblacion_proyeccion' AS fuente_datos,
    N'Municipio; EDAD_SIMPLE; Urbano+Rural; M+F' AS criterio_agregacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
INNER JOIN dbo.dim_municipio AS mu ON mu.id_municipio = f.id_municipio
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = mu.cod_departamento
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
    AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
    AND da.area_normalizada IN (N'Urbano', N'Rural')
WHERE f.nivel_territorial = N'MUNICIPIO'
  AND f.tipo_registro = N'EDAD_SIMPLE'
  AND f.cod_departamento = N'85'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, de.cod_departamento, de.nombre_departamento,
         mu.codigo_dane, mu.nombre_municipio, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_CursoVida_Fact
AS
WITH base AS (
    SELECT
        f.id_proyeccion_dane,
        pd.nombre_proyeccion,
        f.anio,
        f.nivel_territorial,
        f.cod_departamento,
        f.codigo_dane,
        f.id_municipio,
        f.edad_simple,
        f.poblacion
    FROM dbo.fact_poblacion_proyeccion AS f
    INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
    INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
        AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
    INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
        AND da.area_normalizada IN (N'Urbano', N'Rural')
    WHERE f.tipo_registro = N'EDAD_SIMPLE'
      AND (f.cod_departamento = N'85' OR f.codigo_dane LIKE N'85%')
),
territorio AS (
    SELECT
        b.id_proyeccion_dane,
        b.nombre_proyeccion,
        b.anio,
        N'85' AS codigo_territorio_dane,
        CAST(NULL AS nvarchar(10)) AS codigo_municipio,
        b.edad_simple,
        b.poblacion
    FROM base AS b
    WHERE b.nivel_territorial = N'MUNICIPIO'

    UNION ALL

    SELECT
        b.id_proyeccion_dane,
        b.nombre_proyeccion,
        b.anio,
        b.codigo_dane AS codigo_territorio_dane,
        b.codigo_dane AS codigo_municipio,
        b.edad_simple,
        b.poblacion
    FROM base AS b
    WHERE b.nivel_territorial = N'MUNICIPIO'
)
SELECT
    t.id_proyeccion_dane,
    t.nombre_proyeccion,
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    t.codigo_territorio_dane,
    mu.codigo_dane AS codigo_municipio,
    CASE WHEN t.codigo_municipio IS NULL THEN de.nombre_departamento ELSE mu.nombre_municipio END AS nombre_territorio,
    dc.nombre_curso_vida AS curso_vida_proyeccion,
    dc.id_curso_vida,
    dc.codigo AS codigo_curso_vida_dim,
    dc.nombre_curso_vida,
    t.anio AS vigencia,
    CAST(SUM(t.poblacion) AS bigint) AS poblacion,
    N'fact_poblacion_proyeccion' AS fuente_datos,
    N'Curso de vida; sum M+F Urbano+Rural desde EDAD_SIMPLE' AS criterio_agregacion
FROM territorio AS t
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = t.codigo_municipio AND mu.cod_departamento = N'85'
INNER JOIN dbo.dim_curso_vida AS dc
    ON t.edad_simple IS NOT NULL
   AND t.edad_simple BETWEEN dc.edad_minima AND dc.edad_maxima
GROUP BY t.id_proyeccion_dane, t.nombre_proyeccion, de.cod_departamento, de.nombre_departamento,
         t.codigo_territorio_dane, mu.codigo_dane, t.codigo_municipio, mu.nombre_municipio,
         dc.nombre_curso_vida, dc.id_curso_vida, dc.codigo, t.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_GrupoEdad_Fact
AS
WITH base AS (
    SELECT
        f.id_proyeccion_dane,
        pd.nombre_proyeccion,
        f.anio,
        f.nivel_territorial,
        f.cod_departamento,
        f.codigo_dane,
        f.edad_simple,
        f.poblacion
    FROM dbo.fact_poblacion_proyeccion AS f
    INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
    INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
        AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
    INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
        AND da.area_normalizada IN (N'Urbano', N'Rural')
    WHERE f.tipo_registro = N'EDAD_SIMPLE'
      AND (f.cod_departamento = N'85' OR f.codigo_dane LIKE N'85%')
),
territorio AS (
    SELECT b.id_proyeccion_dane, b.nombre_proyeccion, b.anio,
           N'85' AS codigo_territorio_dane, CAST(NULL AS nvarchar(10)) AS codigo_municipio,
           b.edad_simple, b.poblacion
    FROM base AS b WHERE b.nivel_territorial = N'MUNICIPIO'
    UNION ALL
    SELECT b.id_proyeccion_dane, b.nombre_proyeccion, b.anio,
           b.codigo_dane, b.codigo_dane, b.edad_simple, b.poblacion
    FROM base AS b WHERE b.nivel_territorial = N'MUNICIPIO'
)
SELECT
    t.id_proyeccion_dane,
    t.nombre_proyeccion,
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    t.codigo_territorio_dane,
    mu.codigo_dane AS codigo_municipio,
    CASE WHEN t.codigo_municipio IS NULL THEN de.nombre_departamento ELSE mu.nombre_municipio END AS nombre_territorio,
    dg.etiqueta_rango AS grupo_quinquenal,
    dg.id_grupo_edad,
    dg.codigo AS codigo_grupo_edad_dim,
    dg.nombre_grupo_edad,
    t.anio AS vigencia,
    CAST(SUM(t.poblacion) AS bigint) AS poblacion,
    N'fact_poblacion_proyeccion' AS fuente_datos,
    N'Quinquenios; sum M+F Urbano+Rural desde EDAD_SIMPLE' AS criterio_agregacion
FROM territorio AS t
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = t.codigo_municipio AND mu.cod_departamento = N'85'
INNER JOIN dbo.dim_grupo_edad AS dg
    ON t.edad_simple IS NOT NULL
   AND t.edad_simple BETWEEN dg.edad_minima AND dg.edad_maxima
GROUP BY t.id_proyeccion_dane, t.nombre_proyeccion, de.cod_departamento, de.nombre_departamento,
         t.codigo_territorio_dane, mu.codigo_dane, t.codigo_municipio, mu.nombre_municipio,
         dg.etiqueta_rango, dg.id_grupo_edad, dg.codigo, dg.nombre_grupo_edad, t.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Sexo_Fact
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    N'85' AS codigo_territorio_dane,
    CAST(NULL AS nvarchar(10)) AS codigo_municipio,
    de.nombre_departamento AS nombre_territorio,
    ds.id_sexo,
    ds.sexo AS sexo_dim,
    CASE ds.sexo WHEN N'MASCULINO' THEN N'Hombres' WHEN N'FEMENINO' THEN N'Mujeres' END AS sexo_proyeccion,
    f.anio AS vigencia,
    CAST(SUM(f.poblacion) AS bigint) AS poblacion,
    N'fact_poblacion_proyeccion' AS fuente_datos,
    N'Dept 85; TOTAL_SEXO; Urbano+Rural' AS criterio_agregacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
    AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
    AND da.area_normalizada IN (N'Urbano', N'Rural')
WHERE f.cod_departamento = N'85'
  AND f.nivel_territorial = N'DEPARTAMENTO'
  AND f.tipo_registro = N'TOTAL_SEXO'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, de.cod_departamento, de.nombre_departamento,
         ds.id_sexo, ds.sexo, f.anio

UNION ALL

SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    mu.codigo_dane AS codigo_territorio_dane,
    mu.codigo_dane AS codigo_municipio,
    mu.nombre_municipio AS nombre_territorio,
    ds.id_sexo,
    ds.sexo AS sexo_dim,
    CASE ds.sexo WHEN N'MASCULINO' THEN N'Hombres' WHEN N'FEMENINO' THEN N'Mujeres' END AS sexo_proyeccion,
    f.anio AS vigencia,
    CAST(SUM(f.poblacion) AS bigint) AS poblacion,
    N'fact_poblacion_proyeccion' AS fuente_datos,
    N'Municipio; EDAD_SIMPLE; Urbano+Rural; M+F' AS criterio_agregacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
INNER JOIN dbo.dim_municipio AS mu ON mu.id_municipio = f.id_municipio
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = mu.cod_departamento
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
    AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
    AND da.area_normalizada IN (N'Urbano', N'Rural')
WHERE f.nivel_territorial = N'MUNICIPIO'
  AND f.tipo_registro = N'EDAD_SIMPLE'
  AND f.cod_departamento = N'85'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, de.cod_departamento, de.nombre_departamento,
         mu.codigo_dane, mu.nombre_municipio, ds.id_sexo, ds.sexo, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Area_Fact
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    N'85' AS codigo_territorio_dane,
    CAST(NULL AS nvarchar(10)) AS codigo_municipio,
    de.nombre_departamento AS nombre_territorio,
    da.id_area,
    da.area_normalizada,
    da.area_normalizada AS area_proyeccion,
    f.anio AS vigencia,
    CAST(SUM(f.poblacion) AS bigint) AS poblacion,
    N'fact_poblacion_proyeccion' AS fuente_datos,
    N'Dept 85; TOTAL_SEXO; Sexo=Total (M+F)' AS criterio_agregacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = N'85'
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
    AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
    AND da.area_normalizada IN (N'Urbano', N'Rural')
WHERE f.cod_departamento = N'85'
  AND f.nivel_territorial = N'DEPARTAMENTO'
  AND f.tipo_registro = N'TOTAL_SEXO'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, de.cod_departamento, de.nombre_departamento,
         da.id_area, da.area_normalizada, f.anio

UNION ALL

SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    de.cod_departamento AS codigo_departamento,
    de.nombre_departamento,
    mu.codigo_dane AS codigo_territorio_dane,
    mu.codigo_dane AS codigo_municipio,
    mu.nombre_municipio AS nombre_territorio,
    da.id_area,
    da.area_normalizada,
    da.area_normalizada AS area_proyeccion,
    f.anio AS vigencia,
    CAST(SUM(f.poblacion) AS bigint) AS poblacion,
    N'fact_poblacion_proyeccion' AS fuente_datos,
    N'Municipio; EDAD_SIMPLE; Urbano+Rural; M+F' AS criterio_agregacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
INNER JOIN dbo.dim_municipio AS mu ON mu.id_municipio = f.id_municipio
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = mu.cod_departamento
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
    AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
    AND da.area_normalizada IN (N'Urbano', N'Rural')
WHERE f.nivel_territorial = N'MUNICIPIO'
  AND f.tipo_registro = N'EDAD_SIMPLE'
  AND f.cod_departamento = N'85'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, de.cod_departamento, de.nombre_departamento,
         mu.codigo_dane, mu.nombre_municipio, da.id_area, da.area_normalizada, f.anio;
GO

/* Detalle fact (script 14) renombrado explicitamente */
CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_CursoVida_Detalle_Fact
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    pd.anio_publicacion,
    f.anio,
    f.nivel_territorial,
    f.cod_departamento,
    f.codigo_dane,
    da.area_normalizada,
    ds.sexo,
    dc.nombre_curso_vida,
    f.edad_simple,
    SUM(f.poblacion) AS poblacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
LEFT JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
LEFT JOIN dbo.dim_curso_vida AS dc
    ON f.edad_simple IS NOT NULL
   AND f.edad_simple BETWEEN dc.edad_minima AND dc.edad_maxima
WHERE f.tipo_registro = N'EDAD_SIMPLE'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, pd.anio_publicacion, f.anio,
         f.nivel_territorial, f.cod_departamento, f.codigo_dane,
         da.area_normalizada, ds.sexo, dc.nombre_curso_vida, f.edad_simple;
GO

PRINT N'16_vistas_paralelas_legacy_fact.sql OK';
PRINT N'Vistas creadas: *_Legacy, *_Fact; API CursoVida/GrupoEdad restauradas a legacy.';
GO
