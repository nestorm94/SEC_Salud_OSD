/*
================================================================================
 13_comparacion_detallada_poblacion.sql
================================================================================
 PROPÓSITO:
   Comparación detallada fact_poblacion_proyeccion vs tablas fuente DANE
   (departamental Casanare, municipal y nacional) por año y drill-down puntual.

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test (exclusivamente).

 DEPENDENCIAS (ejecutar antes):
   - 07_fact_poblacion_proyeccion.sql
   - 08/09/10/11 (carga del fact)
   - 12_validacion_fact_poblacion.sql (opcional, sanity check previo)

 ORDEN DE EJECUCIÓN:
   12 -> 13 (este script) -> 14_proyeccion_dane -> 15_comparacion_vistas...

 NOTA:
   Ejecutar el archivo COMPLETO (no solo un SELECT suelto).

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\13_comparacion_detallada_poblacion.sql
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

PRINT N'=== COMPARACION DETALLADA POBLACION ===';
PRINT N'';

/* --- A. Departamental Casanare 85 --- */
PRINT N'A. Departamental Casanare (suma total, sin area Total en fuente):';
SELECT origen, pob FROM (
    SELECT N'fuente' AS origen, SUM(CAST(Total_Mujeres + Total_Hombres AS bigint)) AS pob
    FROM dbo.Poblacion_por_Departamento
    WHERE CODIGO_DANE = N'85' AND AREA_GEOGRAFICA <> N'Total'
    UNION ALL
    SELECT N'fact', SUM(poblacion)
    FROM dbo.fact_poblacion_proyeccion
    WHERE fuente_tabla = N'Poblacion_por_Departamento' AND cod_departamento = N'85'
) AS cmp_dept;

PRINT N'';
PRINT N'A2. Departamental Casanare por anio (fuente vs fact):';
/* --- JOIN FULL OUTER: fuente departamental vs fact por año (solo diferencias) --- */
;WITH cte_fuente_dept AS (
    SELECT
        CAST(ano AS int) AS anio,
        SUM(CAST(Total_Mujeres + Total_Hombres AS bigint)) AS pob
    FROM dbo.Poblacion_por_Departamento
    WHERE CODIGO_DANE = N'85' AND AREA_GEOGRAFICA <> N'Total'
    GROUP BY ano
),
cte_fact_dept AS (
    SELECT anio, SUM(poblacion) AS pob
    FROM dbo.fact_poblacion_proyeccion
    WHERE fuente_tabla = N'Poblacion_por_Departamento'
      AND cod_departamento = N'85'
      AND tipo_registro = N'TOTAL_SEXO'
    GROUP BY anio
)
SELECT
    COALESCE(f.anio, d.anio) AS anio,
    f.pob AS pob_fuente,
    d.pob AS pob_fact,
    f.pob - d.pob AS diferencia
FROM cte_fuente_dept AS f
FULL OUTER JOIN cte_fact_dept AS d ON d.anio = f.anio
WHERE ISNULL(f.pob, 0) <> ISNULL(d.pob, 0)
ORDER BY anio;

IF @@ROWCOUNT = 0
    PRINT N'OK: todos los anios departamentales Casanare coinciden.';

PRINT N'';
/* --- B. Municipal Casanare --- */
DECLARE @colAreaMun sysname = (
    SELECT name FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.[PPED-AreaSexoEdadMun-2018-2042_VP]') AND column_id = 6
);

DECLARE @sqlMun nvarchar(max) = N'
PRINT N''B. Municipal Casanare (suma total, sin area Total en fuente):'';
SELECT origen, pob FROM (
    SELECT N''fuente'' AS origen, SUM(CAST(Total_Hombres + Total_Mujeres AS bigint)) AS pob
    FROM dbo.[PPED-AreaSexoEdadMun-2018-2042_VP]
    WHERE DP = 85
      AND UPPER(LTRIM(RTRIM(CAST(' + QUOTENAME(@colAreaMun) + N' AS nvarchar(300))))) <> N''TOTAL''
    UNION ALL
    SELECT N''fact'', SUM(poblacion)
    FROM dbo.fact_poblacion_proyeccion
    WHERE fuente_tabla = N''PPED-AreaSexoEdadMun-2018-2042_VP''
) AS cmp_mun;';
EXEC sys.sp_executesql @sqlMun;

PRINT N'';
PRINT N'B2. Ejemplo drill-down Yopal 85001, 2018, edad 0 hombres:';

SET @sqlMun = N'
SELECT N''fuente'' AS origen, CAST([Hombres_0] AS bigint) AS pob
FROM dbo.[PPED-AreaSexoEdadMun-2018-2042_VP]
WHERE DP = 85 AND MPIO = 85001 AND ANO = 2018
  AND UPPER(LTRIM(RTRIM(CAST(' + QUOTENAME(@colAreaMun) + N' AS nvarchar(300))))) = N''CABECERA MUNICIPAL''
UNION ALL
SELECT N''fact'', poblacion
FROM dbo.fact_poblacion_proyeccion
WHERE fuente_tabla = N''PPED-AreaSexoEdadMun-2018-2042_VP''
  AND codigo_dane = N''85001'' AND anio = 2018 AND id_sexo = 6 AND edad_simple = 0
  AND id_area = 20;';
EXEC sys.sp_executesql @sqlMun;

PRINT N'';
/* --- C. Nacional --- */
DECLARE @colAreaNac sysname = (
    SELECT name FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.PPED_AreaSexoEdadNac_1950_2070') AND column_id = 4
);
DECLARE @colAnoNac sysname = (
    SELECT name FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.PPED_AreaSexoEdadNac_1950_2070') AND column_id = 3
);

DECLARE @sqlNac nvarchar(max) = N'
PRINT N''C. Nacional por anio (solo filas con diferencia):'';
;WITH cte_fuente_nac AS (
    SELECT CAST(' + QUOTENAME(@colAnoNac) + N' AS int) AS anio,
           SUM(CAST(Total_Hombres + Total_Mujeres AS bigint)) AS pob
    FROM dbo.PPED_AreaSexoEdadNac_1950_2070
    WHERE UPPER(LTRIM(RTRIM(CAST(' + QUOTENAME(@colAreaNac) + N' AS nvarchar(300))))) <> N''TOTAL''
    GROUP BY ' + QUOTENAME(@colAnoNac) + N'
),
cte_fact_nac AS (
    SELECT anio, SUM(poblacion) AS pob
    FROM dbo.fact_poblacion_proyeccion
    WHERE fuente_tabla = N''PPED_AreaSexoEdadNac_1950_2070''
      AND tipo_registro = N''EDAD_SIMPLE''
    GROUP BY anio
)
SELECT COALESCE(f.anio, d.anio) AS anio, f.pob AS pob_fuente, d.pob AS pob_fact, f.pob - d.pob AS diferencia
FROM cte_fuente_nac AS f
FULL OUTER JOIN cte_fact_nac AS d ON d.anio = f.anio
WHERE ISNULL(f.pob, 0) <> ISNULL(d.pob, 0)
ORDER BY anio;';
EXEC sys.sp_executesql @sqlNac;

PRINT N'';
PRINT N'=== FIN COMPARACION ===';
GO
