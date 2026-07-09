/*
================================================================================
 25_vistas_asis_mortalidad_detalle.sql
================================================================================
 PROPÓSITO:
   Crea la vista vw_ASIS_Mortalidad_Detalle: una fila por combinación de
   territorio, vigencia, sexo, área, grupo etario y curso de vida (Casanare).

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test u ObservatorioDB (sin validación explícita de BD).

 DEPENDENCIAS:
   - fact_defunciones_casanare_normalizada (carga previa de defunciones)
   - dim_departamento, dim_municipio, dim_sexo, dim_area_residencia,
     dim_grupo_edad, dim_curso_vida

 ORDEN DE EJECUCIÓN:
   Después de normalización de defunciones. Complementa vistas agregadas en fase7.

   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -f 65001 -i scripts\asis-test-clone\25_vistas_asis_mortalidad_detalle.sql
================================================================================
*/
SET NOCOUNT ON;
GO

/* Vista detalle: defunciones con etiquetas de todas las dimensiones */
CREATE OR ALTER VIEW dbo.vw_ASIS_Mortalidad_Detalle
AS
SELECT
    f.codigo_departamento,
    de.nombre_departamento,
    f.codigo_municipio,
    COALESCE(mu.nombre_municipio, N'SIN MUNICIPIO DANE') AS nombre_municipio,
    f.anio AS vigencia,
    ds.sexo,
    da.area_normalizada AS area_residencia,
    ge.etiqueta_rango AS grupo_etareo_quinquenios_dane,
    dc.nombre_curso_vida,
    f.numero_defunciones AS defunciones,
    ds.id_sexo,
    da.id_area,
    ge.id_grupo_edad,
    dc.id_curso_vida,
    ge.codigo AS codigo_grupo_edad,
    dc.codigo AS codigo_curso_vida_dim,
    N'fact_defunciones_casanare_normalizada' AS fuente_datos,
    N'Detalle por sexo, area, grupo etareo y curso de vida' AS criterio_agregacion
FROM dbo.fact_defunciones_casanare_normalizada AS f
/* JOIN departamento: nombre territorial */
INNER JOIN dbo.dim_departamento AS de
    ON de.cod_departamento = f.codigo_departamento
/* JOIN municipio: código DANE dentro del departamento */
LEFT JOIN dbo.dim_municipio AS mu
    ON mu.codigo_dane = f.codigo_municipio
   AND mu.cod_departamento = f.codigo_departamento
INNER JOIN dbo.dim_sexo AS ds
    ON ds.id_sexo = f.id_sexo
INNER JOIN dbo.dim_area_residencia AS da
    ON da.id_area = f.id_area
INNER JOIN dbo.dim_grupo_edad AS ge
    ON ge.id_grupo_edad = f.id_grupo_edad
INNER JOIN dbo.dim_curso_vida AS dc
    ON dc.id_curso_vida = f.id_curso_vida
/* Filtro geográfico: solo Casanare (DANE 85) */
WHERE f.codigo_departamento = N'85';
GO

/* Verificación rápida post-creación */
PRINT N'--- vw_ASIS_Mortalidad_Detalle ---';
SELECT COUNT(*) AS filas FROM dbo.vw_ASIS_Mortalidad_Detalle;
SELECT TOP (5) * FROM dbo.vw_ASIS_Mortalidad_Detalle ORDER BY vigencia DESC, codigo_municipio;
GO

PRINT N'=== FIN 25_vistas_asis_mortalidad_detalle ===';
GO
