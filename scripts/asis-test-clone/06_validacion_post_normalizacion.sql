/*
================================================================================
 06_validacion_post_normalizacion.sql
================================================================================
 PROPÓSITO:
   ETAPA 4: validación de solo lectura tras 04 y 05. Reporta conteos,
   duplicados, consistencia codigo_dane y mapeos de área sin id_area.

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test (exclusivamente).

 DEPENDENCIAS (ejecutar antes):
   - 04_normalizacion_catalogos_geograficos.sql
   - 05_map_area_residencia_fuente.sql

 ORDEN DE EJECUCIÓN:
   04 -> 05 -> 06 (este script) -> 07_fact_poblacion...

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\06_validacion_post_normalizacion.sql
================================================================================
*/
SET NOCOUNT ON;
GO

IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
BEGIN
    DECLARE @db_err2 sysname = DB_NAME();
    RAISERROR(N'Ejecutar unicamente en ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err2);
    RETURN;
END
GO

/* --- Conteos iniciales de dimensiones normalizadas --- */
DECLARE @nDep int, @nMun int, @nAreaAct int, @nAreaTot int, @nMap int;
SELECT @nDep = COUNT(*) FROM dbo.dim_departamento;
SELECT @nMun = COUNT(*) FROM dbo.dim_municipio;
SELECT @nAreaTot = COUNT(*) FROM dbo.dim_area_residencia;
SELECT @nAreaAct = COUNT(*) FROM dbo.dim_area_residencia WHERE estado = 1;

PRINT N'=== VALIDACION POST-NORMALIZACION ===';
PRINT N'Base: ' + DB_NAME();
PRINT N'';

PRINT N'--- Conteos ---';
PRINT N'dim_departamento: ' + CAST(@nDep AS nvarchar(20));
PRINT N'dim_municipio: ' + CAST(@nMun AS nvarchar(20));
PRINT N'dim_area_residencia activas (estado=1): ' + CAST(@nAreaAct AS nvarchar(20));
PRINT N'dim_area_residencia total: ' + CAST(@nAreaTot AS nvarchar(20));

IF OBJECT_ID(N'dbo.map_area_residencia_fuente', N'U') IS NOT NULL
BEGIN
    SELECT @nMap = COUNT(*) FROM dbo.map_area_residencia_fuente;
    PRINT N'map_area_residencia_fuente: ' + CAST(@nMap AS nvarchar(20));
END

PRINT N'';
PRINT N'--- 4. Duplicados cod_departamento ---';
SELECT cod_departamento, COUNT(*) AS n
FROM dbo.dim_departamento
GROUP BY cod_departamento
HAVING COUNT(*) > 1;

IF @@ROWCOUNT = 0 PRINT N'OK: sin duplicados cod_departamento';

PRINT N'';
PRINT N'--- 5. Duplicados codigo_dane ---';
SELECT codigo_dane, COUNT(*) AS n
FROM dbo.dim_municipio
GROUP BY codigo_dane
HAVING COUNT(*) > 1;

IF @@ROWCOUNT = 0 PRINT N'OK: sin duplicados codigo_dane';

PRINT N'';
PRINT N'--- 6. codigo_dane inconsistente ---';
SELECT id_municipio, cod_departamento, cod_municipio, codigo_dane,
       cod_departamento + cod_municipio AS esperado
FROM dbo.dim_municipio
WHERE codigo_dane <> cod_departamento + cod_municipio;

IF @@ROWCOUNT = 0 PRINT N'OK: codigo_dane = cod_departamento + cod_municipio';

PRINT N'';
PRINT N'--- 7. Mapeos de area sin id_area (esperado solo para Total) ---';
IF OBJECT_ID(N'dbo.map_area_residencia_fuente', N'U') IS NOT NULL
BEGIN
    SELECT id_mapeo, fuente_tabla, columna_origen, valor_origen, area_normalizada
    FROM dbo.map_area_residencia_fuente
    WHERE id_area IS NULL
    ORDER BY valor_origen;

    SELECT id_mapeo, fuente_tabla, columna_origen, valor_origen
    FROM dbo.map_area_residencia_fuente
    WHERE id_area IS NULL AND UPPER(LTRIM(RTRIM(valor_origen))) <> N'TOTAL';
END

PRINT N'';
PRINT N'--- dim_departamento (cod 2 dígitos) ---';
SELECT id_departamento, cod_departamento, nombre_departamento, estado
FROM dbo.dim_departamento
ORDER BY cod_departamento;

PRINT N'';
PRINT N'--- dim_area_residencia (estado) ---';
SELECT id_area, codigo_area, area_original, area_normalizada, estado
FROM dbo.dim_area_residencia
ORDER BY id_area;

PRINT N'';
PRINT N'--- Restricciones únicas ---';
SELECT i.name AS indice, OBJECT_NAME(i.object_id) AS tabla
FROM sys.indexes AS i
WHERE i.is_unique = 1
  AND i.name IN (
      N'UQ_dim_departamento_cod_departamento',
      N'UQ_dim_municipio_codigo_dane',
      N'UQ_dim_municipio_cod_departamento_cod_municipio'
  )
ORDER BY tabla, indice;

PRINT N'';
PRINT N'=== FIN VALIDACION ===';
GO
