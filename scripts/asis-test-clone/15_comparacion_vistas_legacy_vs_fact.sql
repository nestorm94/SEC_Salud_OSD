/*
Comparacion lado a lado: vistas API legacy vs agregaciones fact.
SOLO ObservatorioDB_ASIS_Test. Solo lectura (no modifica objetos).

Ejecutar:
  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\15_comparacion_vistas_legacy_vs_fact.sql

Parametro opcional en sesion:
  DECLARE @id_proyeccion_dane int = 1;
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

DECLARE @id_proyeccion_dane int = 1;

IF NOT EXISTS (SELECT 1 FROM dbo.dim_proyeccion_dane WHERE id_proyeccion_dane = @id_proyeccion_dane)
BEGIN
    SELECT @id_proyeccion_dane = MIN(id_proyeccion_dane) FROM dbo.dim_proyeccion_dane;
END

PRINT N'=== COMPARACION VISTAS LEGACY vs FACT ===';
PRINT N'id_proyeccion_dane = ' + CAST(@id_proyeccion_dane AS nvarchar(20));
PRINT N'';

/* --- 1. Total departamental Casanare --- */
PRINT N'1. Poblacion total departamental (legacy vw_ASIS_Poblacion_Total vs fact):';
;WITH leg AS (
    SELECT vigencia, poblacion
    FROM dbo.vw_ASIS_Poblacion_Total
),
fact AS (
    SELECT
        f.anio AS vigencia,
        CAST(SUM(f.poblacion) AS bigint) AS poblacion
    FROM dbo.fact_poblacion_proyeccion AS f
    INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
        AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
    INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
        AND da.area_normalizada IN (N'Urbano', N'Rural')
    WHERE f.id_proyeccion_dane = @id_proyeccion_dane
      AND f.cod_departamento = N'85'
      AND f.nivel_territorial = N'DEPARTAMENTO'
      AND f.tipo_registro = N'TOTAL_SEXO'
    GROUP BY f.anio
)
SELECT
    COALESCE(l.vigencia, f.vigencia) AS vigencia,
    l.poblacion AS pob_legacy,
    f.poblacion AS pob_fact,
    ISNULL(l.poblacion, 0) - ISNULL(f.poblacion, 0) AS diferencia,
    CASE
        WHEN l.vigencia IS NULL THEN N'solo_fact'
        WHEN f.vigencia IS NULL THEN N'solo_legacy'
        WHEN l.poblacion = f.poblacion THEN N'OK'
        ELSE N'DIFF'
    END AS estado
FROM leg AS l
FULL OUTER JOIN fact AS f ON f.vigencia = l.vigencia
WHERE ISNULL(l.poblacion, 0) <> ISNULL(f.poblacion, 0)
   OR l.vigencia IS NULL OR f.vigencia IS NULL
ORDER BY vigencia;

IF @@ROWCOUNT = 0
    PRINT N'   OK: todos los anios coinciden en total departamental.';

PRINT N'';
PRINT N'1b. Resumen rangos de vigencia:';
SELECT N'legacy' AS capa, MIN(vigencia) AS anio_min, MAX(vigencia) AS anio_max, COUNT(*) AS filas
FROM dbo.vw_ASIS_Poblacion_Total
UNION ALL
SELECT N'fact', MIN(f.anio), MAX(f.anio), COUNT(DISTINCT f.anio)
FROM dbo.fact_poblacion_proyeccion AS f
WHERE f.id_proyeccion_dane = @id_proyeccion_dane
  AND f.cod_departamento = N'85'
  AND f.nivel_territorial = N'DEPARTAMENTO'
  AND f.tipo_registro = N'TOTAL_SEXO';

PRINT N'';
/* --- 2. Municipios (un anio de muestra) --- */
DECLARE @anio_muestra int = 2020;

PRINT N'2. Poblacion municipal anio ' + CAST(@anio_muestra AS nvarchar(10)) + N' (legacy vs fact):';
;WITH leg AS (
    SELECT codigo_municipio, poblacion
    FROM dbo.vw_ASIS_Poblacion_Municipio
    WHERE vigencia = @anio_muestra
),
fact AS (
    SELECT
        f.codigo_dane AS codigo_municipio,
        CAST(SUM(f.poblacion) AS bigint) AS poblacion
    FROM dbo.fact_poblacion_proyeccion AS f
    INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
        AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
    INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
        AND da.area_normalizada IN (N'Urbano', N'Rural')
    WHERE f.id_proyeccion_dane = @id_proyeccion_dane
      AND f.nivel_territorial = N'MUNICIPIO'
      AND f.tipo_registro = N'EDAD_SIMPLE'
      AND f.anio = @anio_muestra
    GROUP BY f.codigo_dane
)
SELECT TOP 20
    COALESCE(l.codigo_municipio, f.codigo_municipio) AS codigo_municipio,
    l.poblacion AS pob_legacy,
    f.poblacion AS pob_fact,
    ISNULL(l.poblacion, 0) - ISNULL(f.poblacion, 0) AS diferencia
FROM leg AS l
FULL OUTER JOIN fact AS f ON f.codigo_municipio = l.codigo_municipio
WHERE ISNULL(l.poblacion, 0) <> ISNULL(f.poblacion, 0)
ORDER BY ABS(ISNULL(l.poblacion, 0) - ISNULL(f.poblacion, 0)) DESC;

DECLARE @mun_diff int = @@ROWCOUNT;
IF @mun_diff = 0
    PRINT N'   OK: todos los municipios coinciden.';
ELSE
    PRINT N'   Municipios con diferencia: ' + CAST(@mun_diff AS nvarchar(20)) + N' (mostrando top 20).';

PRINT N'';
/* --- 3. Sexo departamento 85 --- */
PRINT N'3. Poblacion por sexo departamento 85, anio ' + CAST(@anio_muestra AS nvarchar(10)) + N':';
;WITH leg AS (
    SELECT sexo_dim, poblacion
    FROM dbo.vw_ASIS_Poblacion_Sexo
    WHERE vigencia = @anio_muestra
      AND codigo_territorio_dane = N'85'
),
fact AS (
    SELECT ds.sexo AS sexo_dim, CAST(SUM(f.poblacion) AS bigint) AS poblacion
    FROM dbo.fact_poblacion_proyeccion AS f
    INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
        AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
    INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
        AND da.area_normalizada IN (N'Urbano', N'Rural')
    WHERE f.id_proyeccion_dane = @id_proyeccion_dane
      AND f.cod_departamento = N'85'
      AND f.nivel_territorial = N'DEPARTAMENTO'
      AND f.tipo_registro = N'TOTAL_SEXO'
      AND f.anio = @anio_muestra
    GROUP BY ds.sexo
)
SELECT COALESCE(l.sexo_dim, f.sexo_dim) AS sexo, l.poblacion AS pob_legacy, f.poblacion AS pob_fact,
       ISNULL(l.poblacion, 0) - ISNULL(f.poblacion, 0) AS diferencia
FROM leg AS l
FULL OUTER JOIN fact AS f ON f.sexo_dim = l.sexo_dim
WHERE ISNULL(l.poblacion, 0) <> ISNULL(f.poblacion, 0);

IF @@ROWCOUNT = 0
    PRINT N'   OK: sexo departamental coincide.';

PRINT N'';
/* --- 4. Area departamento 85 --- */
PRINT N'4. Poblacion por area departamento 85, anio ' + CAST(@anio_muestra AS nvarchar(10)) + N':';
;WITH leg AS (
    SELECT area_normalizada, poblacion
    FROM dbo.vw_ASIS_Poblacion_Area
    WHERE vigencia = @anio_muestra
      AND codigo_territorio_dane = N'85'
),
fact AS (
    SELECT da.area_normalizada, CAST(SUM(f.poblacion) AS bigint) AS poblacion
    FROM dbo.fact_poblacion_proyeccion AS f
    INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
        AND ds.sexo IN (N'MASCULINO', N'FEMENINO')
    INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
        AND da.area_normalizada IN (N'Urbano', N'Rural')
    WHERE f.id_proyeccion_dane = @id_proyeccion_dane
      AND f.cod_departamento = N'85'
      AND f.nivel_territorial = N'DEPARTAMENTO'
      AND f.tipo_registro = N'TOTAL_SEXO'
      AND f.anio = @anio_muestra
    GROUP BY da.area_normalizada
)
SELECT COALESCE(l.area_normalizada, f.area_normalizada) AS area,
       l.poblacion AS pob_legacy, f.poblacion AS pob_fact,
       ISNULL(l.poblacion, 0) - ISNULL(f.poblacion, 0) AS diferencia
FROM leg AS l
FULL OUTER JOIN fact AS f ON f.area_normalizada = l.area_normalizada
WHERE ISNULL(l.poblacion, 0) <> ISNULL(f.poblacion, 0);

IF @@ROWCOUNT = 0
    PRINT N'   OK: area departamental coincide.';

PRINT N'';
/* --- 5. Curso de vida (solo si existe vista _Legacy o legacy aun en vw_ASIS_Poblacion_CursoVida) --- */
IF OBJECT_ID(N'dbo.vw_ASIS_Poblacion_CursoVida_Legacy', N'V') IS NOT NULL
BEGIN
    PRINT N'5. Curso de vida departamento 85 vs _Legacy:';
    ;WITH leg AS (
        SELECT curso_vida_proyeccion, CAST(SUM(poblacion) AS bigint) AS pob
        FROM dbo.vw_ASIS_Poblacion_CursoVida_Legacy
        WHERE vigencia = @anio_muestra AND codigo_territorio_dane = N'85'
        GROUP BY curso_vida_proyeccion
    ),
    fact AS (
        SELECT f.curso_vida_proyeccion, CAST(SUM(f.poblacion) AS bigint) AS pob
        FROM dbo.vw_ASIS_Poblacion_CursoVida_Fact AS f
        WHERE f.id_proyeccion_dane = @id_proyeccion_dane
          AND f.vigencia = @anio_muestra
          AND f.codigo_territorio_dane = N'85'
          AND f.codigo_municipio IS NULL
        GROUP BY f.curso_vida_proyeccion
    )
    SELECT TOP 10 * FROM (
        SELECT COALESCE(l.curso_vida_proyeccion, f.curso_vida_proyeccion) AS curso,
               l.pob AS pob_legacy, f.pob AS pob_fact
        FROM leg AS l FULL OUTER JOIN fact AS f ON f.curso_vida_proyeccion = l.curso_vida_proyeccion
        WHERE ISNULL(l.pob, 0) <> ISNULL(f.pob, 0)
    ) AS d;
END
ELSE
BEGIN
    PRINT N'5. Curso de vida: omitido (ejecute 16_vistas_paralelas_legacy_fact.sql para crear _Legacy).';
    PRINT N'   Nota: vw_ASIS_Poblacion_CursoVida actual usa esquema FACT (script 14).';
END

PRINT N'';
/* --- 6. Vistas _Fact si existen --- */
IF OBJECT_ID(N'dbo.vw_ASIS_Poblacion_Total_Fact', N'V') IS NOT NULL
BEGIN
    PRINT N'6. Validacion vw_ASIS_Poblacion_Total vs vw_ASIS_Poblacion_Total_Fact:';
    SELECT COUNT(*) AS anios_diff
    FROM dbo.vw_ASIS_Poblacion_Total AS l
    INNER JOIN dbo.vw_ASIS_Poblacion_Total_Fact AS f
        ON f.vigencia = l.vigencia AND f.id_proyeccion_dane = @id_proyeccion_dane
    WHERE l.poblacion <> f.poblacion;
END
ELSE
    PRINT N'6. Vistas _Fact no creadas aun (opcional: script 16).';

PRINT N'';
PRINT N'=== FIN COMPARACION VISTAS ===';
GO
