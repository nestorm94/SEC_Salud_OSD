/*
================================================================================
 12_validacion_fact_poblacion.sql
================================================================================
 PROPÓSITO:
   Valida fact_poblacion_proyeccion: conteos por dimensión, duplicados en grano
   lógico, resolución de FKs y comparación de sumas vs tablas fuente DANE.

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test (exclusivamente).

 DEPENDENCIAS (ejecutar antes):
   - 07_fact_poblacion_proyeccion.sql (tabla hecho y función fn_ASIS_Resolver_IdArea)
   - 08/09/10/11 (SPs de normalización por nivel territorial)
   - 14_proyeccion_dane_versionamiento.sql (dim_proyeccion_dane, id_proyeccion_dane)

 ORDEN DE EJECUCIÓN:
   07 -> 08/09/10 -> 11 -> 12 (este script) -> 13_comparacion_detallada...

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\12_validacion_fact_poblacion.sql
================================================================================
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

PRINT N'=== VALIDACION fact_poblacion_proyeccion ===';

DECLARE @total bigint;
SELECT @total = COUNT(*) FROM dbo.fact_poblacion_proyeccion;
PRINT N'1. Total registros: ' + CAST(@total AS nvarchar(20));

PRINT N'';
PRINT N'1b. Por proyeccion DANE:';
/* --- JOIN fact ↔ dim_proyeccion_dane: agregados por versión DANE --- */
SELECT d.id_proyeccion_dane, d.nombre_proyeccion, d.anio_publicacion, COUNT(*) AS registros, SUM(f.poblacion) AS suma_poblacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS d ON d.id_proyeccion_dane = f.id_proyeccion_dane
GROUP BY d.id_proyeccion_dane, d.nombre_proyeccion, d.anio_publicacion
ORDER BY d.anio_publicacion, d.id_proyeccion_dane;

PRINT N'';
PRINT N'2. Por fuente_tabla:';
SELECT fuente_tabla, COUNT(*) AS registros, SUM(poblacion) AS suma_poblacion
FROM dbo.fact_poblacion_proyeccion
GROUP BY fuente_tabla
ORDER BY fuente_tabla;

PRINT N'';
PRINT N'3. Por nivel_territorial:';
SELECT nivel_territorial, COUNT(*) AS registros, SUM(poblacion) AS suma_poblacion
FROM dbo.fact_poblacion_proyeccion
GROUP BY nivel_territorial
ORDER BY nivel_territorial;

PRINT N'';
PRINT N'4. Por tipo_registro:';
SELECT tipo_registro, COUNT(*) AS registros, SUM(poblacion) AS suma_poblacion
FROM dbo.fact_poblacion_proyeccion
GROUP BY tipo_registro
ORDER BY tipo_registro;

PRINT N'';
PRINT N'5. Duplicados (grano logico):';
SELECT nivel_territorial, tipo_registro, fuente_tabla, uq_cod_dep, uq_cod_mun, uq_cod_dane,
       anio, uq_id_area, id_sexo, uq_edad, id_proyeccion_dane, COUNT(*) AS n
FROM dbo.fact_poblacion_proyeccion
GROUP BY nivel_territorial, tipo_registro, fuente_tabla, uq_cod_dep, uq_cod_mun, uq_cod_dane,
         anio, uq_id_area, id_sexo, uq_edad, id_proyeccion_dane
HAVING COUNT(*) > 1;

IF @@ROWCOUNT = 0 PRINT N'OK: sin duplicados';

PRINT N'';
PRINT N'6. Areas sin homologar (id_area NULL con tipo EDAD_SIMPLE o TOTAL_SEXO):';
SELECT fuente_tabla, COUNT(*) AS n
FROM dbo.fact_poblacion_proyeccion
WHERE id_area IS NULL
GROUP BY fuente_tabla;
IF @@ROWCOUNT = 0 PRINT N'OK: sin filas con id_area NULL';

PRINT N'';
PRINT N'7. Departamentos sin id_departamento (excluye NACION):';
SELECT cod_departamento, COUNT(*) AS n
FROM dbo.fact_poblacion_proyeccion
WHERE nivel_territorial <> N'NACION' AND id_departamento IS NULL
GROUP BY cod_departamento;
IF @@ROWCOUNT = 0 PRINT N'OK: departamentos resueltos';

PRINT N'';
PRINT N'8. Municipios Casanare sin id_municipio:';
SELECT codigo_dane, COUNT(*) AS n
FROM dbo.fact_poblacion_proyeccion
WHERE nivel_territorial = N'MUNICIPIO' AND cod_departamento = N'85' AND id_municipio IS NULL
GROUP BY codigo_dane;
IF @@ROWCOUNT = 0 PRINT N'OK: municipios Casanare resueltos';

PRINT N'';
PRINT N'9. Comparacion sumas vs fuente DEPARTAMENTAL (Casanare 85, sin Total area):';
SELECT N'fuente' AS origen, SUM(CAST(Total_Mujeres + Total_Hombres AS bigint)) AS pob
FROM dbo.Poblacion_por_Departamento
WHERE CODIGO_DANE = N'85' AND AREA_GEOGRAFICA <> N'Total'
UNION ALL
SELECT N'fact', SUM(poblacion)
FROM dbo.fact_poblacion_proyeccion
WHERE fuente_tabla = N'Poblacion_por_Departamento' AND cod_departamento = N'85';

PRINT N'';
PRINT N'9b. Comparacion MUNICIPAL Casanare (Total_H+M sin area Total vs fact):';
DECLARE @colAreaMun sysname = (
    SELECT name FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.[PPED-AreaSexoEdadMun-2018-2042_VP]') AND column_id = 6
);
DECLARE @sqlCmp nvarchar(max) = N'
SELECT N''fuente_sin_total_area'' AS origen, SUM(CAST(Total_Hombres + Total_Mujeres AS bigint)) AS pob
FROM dbo.[PPED-AreaSexoEdadMun-2018-2042_VP]
WHERE DP = 85
  AND UPPER(LTRIM(RTRIM(CAST(' + QUOTENAME(@colAreaMun) + N' AS nvarchar(300))))) <> N''TOTAL''
UNION ALL
SELECT N''fact_suma'', SUM(poblacion)
FROM dbo.fact_poblacion_proyeccion
WHERE fuente_tabla = N''PPED-AreaSexoEdadMun-2018-2042_VP'';';
EXEC sys.sp_executesql @sqlCmp;

PRINT N'';
PRINT N'=== FIN VALIDACION ===';
GO
