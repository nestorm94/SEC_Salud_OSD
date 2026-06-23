/*
Vistas ASIS nacimientos (paralelas a mortalidad).

  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\22_vistas_asis_nacimientos.sql
*/
SET NOCOUNT ON;
GO

IF DB_NAME() NOT IN (N'ObservatorioDB_ASIS_Test', N'ObservatorioDB')
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Ejecutar en ObservatorioDB u ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

PRINT N'=== 22 - Vistas ASIS nacimientos ===';
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_Total
AS
SELECT
    f.codigo_departamento,
    de.nombre_departamento,
    f.anio AS vigencia,
    SUM(f.numero_nacimientos) AS nacimientos,
    N'fact_nacimientos_casanare_normalizada' AS fuente_datos,
    N'Casanare codigo_departamento=85' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_Municipio
AS
SELECT
    f.codigo_departamento,
    de.nombre_departamento,
    f.codigo_municipio,
    COALESCE(mu.nombre_municipio, N'SIN MUNICIPIO DANE') AS nombre_municipio,
    CAST(NULL AS nvarchar(100)) AS regional,
    f.anio AS vigencia,
    SUM(f.numero_nacimientos) AS nacimientos,
    N'fact_nacimientos_casanare_normalizada' AS fuente_datos,
    N'Agregado por codigo_municipio' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_Sexo
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    ds.id_sexo, ds.sexo AS sexo_dim, f.anio AS vigencia,
    SUM(f.numero_nacimientos) AS nacimientos,
    N'fact_nacimientos_casanare_normalizada + dim_sexo' AS fuente_datos,
    N'FK id_sexo (sexo del nacido vivo)' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio, ds.id_sexo, ds.sexo, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_Area
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    da.id_area, da.area_normalizada, f.anio AS vigencia,
    SUM(f.numero_nacimientos) AS nacimientos,
    N'fact_nacimientos_casanare_normalizada + dim_area_residencia' AS fuente_datos,
    N'FK id_area' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio, da.id_area, da.area_normalizada, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_GrupoEdad
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    gm.id_grupo_edad_madre, gm.codigo AS codigo_grupo_edad_madre, gm.etiqueta_rango AS grupo_edad_madre,
    f.anio AS vigencia,
    SUM(f.numero_nacimientos) AS nacimientos,
    N'fact_nacimientos_casanare_normalizada + dim_grupo_edad_madre' AS fuente_datos,
    N'Quinquenios DANE edad de la madre' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_grupo_edad_madre AS gm ON gm.id_grupo_edad_madre = f.id_grupo_edad_madre
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    gm.id_grupo_edad_madre, gm.codigo, gm.etiqueta_rango, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_Detalle
AS
SELECT
    f.codigo_departamento,
    de.nombre_departamento,
    f.codigo_municipio,
    COALESCE(mu.nombre_municipio, N'SIN MUNICIPIO DANE') AS nombre_municipio,
    f.anio AS vigencia,
    gm.etiqueta_rango AS grupo_etareo_quinquenios_dane,
    ne.etiqueta_dane AS nivel_educativo,
    pe.etiqueta_dane AS pertenencia_etnica,
    ds.sexo,
    da.area_normalizada AS area_residencia,
    p.etiqueta_rango AS peso_al_nacer,
    sg.etiqueta_rango AS semanas_gestacion,
    f.numero_nacimientos AS nacimientos,
    gm.id_grupo_edad_madre,
    ne.id_nivel_educativo,
    pe.id_pertenencia_etnica,
    ds.id_sexo,
    p.id_peso_al_nacer,
    sg.id_semanas_gestacion,
    N'fact_nacimientos_casanare_normalizada' AS fuente_datos,
    N'Detalle por dimensiones sociodemograficas y perinatales' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_grupo_edad_madre AS gm ON gm.id_grupo_edad_madre = f.id_grupo_edad_madre
LEFT JOIN dbo.dim_nivel_educativo AS ne ON ne.id_nivel_educativo = f.id_nivel_educativo
LEFT JOIN dbo.dim_pertenencia_etnica AS pe ON pe.id_pertenencia_etnica = f.id_pertenencia_etnica
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
INNER JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
LEFT JOIN dbo.dim_peso_al_nacer AS p ON p.id_peso_al_nacer = f.id_peso_al_nacer
LEFT JOIN dbo.dim_semanas_gestacion AS sg ON sg.id_semanas_gestacion = f.id_semanas_gestacion
WHERE f.codigo_departamento = N'85';
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_NivelEducativo
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    ne.id_nivel_educativo, ne.codigo AS codigo_nivel_educativo, ne.etiqueta_dane AS nivel_educativo,
    f.anio AS vigencia, SUM(f.numero_nacimientos) AS nacimientos,
    N'fact_nacimientos_casanare_normalizada + dim_nivel_educativo' AS fuente_datos,
    N'Nivel educativo de la madre' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_nivel_educativo AS ne ON ne.id_nivel_educativo = f.id_nivel_educativo
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    ne.id_nivel_educativo, ne.codigo, ne.etiqueta_dane, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_PertenenciaEtnica
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    pe.id_pertenencia_etnica, pe.codigo AS codigo_pertenencia_etnica, pe.etiqueta_dane AS pertenencia_etnica,
    f.anio AS vigencia, SUM(f.numero_nacimientos) AS nacimientos,
    N'fact_nacimientos_casanare_normalizada + dim_pertenencia_etnica' AS fuente_datos,
    N'Pertenencia etnica de la madre' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_pertenencia_etnica AS pe ON pe.id_pertenencia_etnica = f.id_pertenencia_etnica
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    pe.id_pertenencia_etnica, pe.codigo, pe.etiqueta_dane, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_PesoAlNacer
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    p.id_peso_al_nacer, p.codigo AS codigo_peso_al_nacer, p.etiqueta_rango AS peso_al_nacer, p.categoria_normalizada,
    f.anio AS vigencia, SUM(f.numero_nacimientos) AS nacimientos,
    N'fact_nacimientos_casanare_normalizada + dim_peso_al_nacer' AS fuente_datos,
    N'Peso al nacer' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_peso_al_nacer AS p ON p.id_peso_al_nacer = f.id_peso_al_nacer
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    p.id_peso_al_nacer, p.codigo, p.etiqueta_rango, p.categoria_normalizada, f.anio;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Nacimientos_SemanasGestacion
AS
SELECT
    f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    sg.id_semanas_gestacion, sg.codigo AS codigo_semanas_gestacion, sg.etiqueta_rango AS semanas_gestacion, sg.categoria_normalizada,
    f.anio AS vigencia, SUM(f.numero_nacimientos) AS nacimientos,
    N'fact_nacimientos_casanare_normalizada + dim_semanas_gestacion' AS fuente_datos,
    N'Semanas de gestacion' AS criterio_agregacion
FROM dbo.fact_nacimientos_casanare_normalizada AS f
INNER JOIN dbo.dim_departamento AS de ON de.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_semanas_gestacion AS sg ON sg.id_semanas_gestacion = f.id_semanas_gestacion
LEFT JOIN dbo.dim_municipio AS mu ON mu.codigo_dane = f.codigo_municipio AND mu.cod_departamento = f.codigo_departamento
WHERE f.codigo_departamento = N'85'
GROUP BY f.codigo_departamento, de.nombre_departamento, f.codigo_municipio, mu.nombre_municipio,
    sg.id_semanas_gestacion, sg.codigo, sg.etiqueta_rango, sg.categoria_normalizada, f.anio;
GO

PRINT N'=== FIN 22_vistas_asis_nacimientos ===';
GO
