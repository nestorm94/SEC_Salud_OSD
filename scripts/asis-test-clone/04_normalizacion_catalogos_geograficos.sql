/*
================================================================================
 04_normalizacion_catalogos_geograficos.sql
================================================================================
 PROPÓSITO:
   ETAPA 1: normaliza códigos DANE en dim_departamento/dim_municipio (padding,
   unicidad, integridad referencial). ETAPA 2: desactiva filas de sexo
   contaminadas en dim_area_residencia (FEMENINO/MASCULINO).

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test (exclusivamente; el script valida DB_NAME()).

 DEPENDENCIAS (ejecutar antes):
   - 01_restore_observatoriodb_asis_test.sql (clon disponible)

 ORDEN DE EJECUCIÓN:
   04 (este script) -> 05_map_area -> 06_validacion -> 07_fact_poblacion...

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\04_normalizacion_catalogos_geograficos.sql
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Ejecutar unicamente en ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

PRINT N'=== ETAPA 1 — dim_departamento / dim_municipio ===';
GO

BEGIN TRANSACTION;

/* --- 1. Normalizar cod_departamento a 2 dígitos (00 = NACIONAL) --- */
UPDATE dbo.dim_departamento
SET cod_departamento = RIGHT(N'00' + LTRIM(RTRIM(cod_departamento)), 2)
WHERE LEN(LTRIM(RTRIM(cod_departamento))) < 2
   OR cod_departamento <> RIGHT(N'00' + LTRIM(RTRIM(cod_departamento)), 2);

PRINT N'dim_departamento filas actualizadas (padding): ' + CAST(@@ROWCOUNT AS nvarchar(20));

/* --- JOIN dim_municipio ↔ dim_departamento: sincronizar cod_departamento natural --- */
UPDATE m
SET m.cod_departamento = d.cod_departamento
FROM dbo.dim_municipio AS m
INNER JOIN dbo.dim_departamento AS d ON d.id_departamento = m.id_departamento
WHERE m.cod_departamento <> d.cod_departamento;

PRINT N'dim_municipio cod_departamento sincronizados: ' + CAST(@@ROWCOUNT AS nvarchar(20));

/* Legacy: dim_departamentos si tiene filas */
IF OBJECT_ID(N'dbo.dim_departamentos', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.dim_departamentos
    SET codigo_departamento = RIGHT(N'00' + LTRIM(RTRIM(codigo_departamento)), 2)
    WHERE LEN(LTRIM(RTRIM(codigo_departamento))) < 2
       OR codigo_departamento <> RIGHT(N'00' + LTRIM(RTRIM(codigo_departamento)), 2);
END

/* --- 2. Validar unicidad cod_departamento --- */
IF EXISTS (
    SELECT cod_departamento
    FROM dbo.dim_departamento
    GROUP BY cod_departamento
    HAVING COUNT(*) > 1
)
BEGIN
    RAISERROR(N'Duplicados en dim_departamento.cod_departamento después del padding.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

/* --- 3. Restricción única cod_departamento --- */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_dim_departamento_cod_departamento'
      AND object_id = OBJECT_ID(N'dbo.dim_departamento')
)
BEGIN
    ALTER TABLE dbo.dim_departamento
    ADD CONSTRAINT UQ_dim_departamento_cod_departamento UNIQUE (cod_departamento);
    PRINT N'Creada UQ_dim_departamento_cod_departamento';
END
ELSE
    PRINT N'UQ_dim_departamento_cod_departamento ya existe';

/* --- 4. Validar dim_municipio --- */
IF EXISTS (
    SELECT 1 FROM dbo.dim_municipio
    WHERE LEN(LTRIM(RTRIM(cod_departamento))) <> 2
)
BEGIN
    RAISERROR(N'dim_municipio: cod_departamento debe tener 2 dígitos.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

IF EXISTS (
    SELECT 1 FROM dbo.dim_municipio
    WHERE LEN(LTRIM(RTRIM(cod_municipio))) <> 3
)
BEGIN
    RAISERROR(N'dim_municipio: cod_municipio debe tener 3 dígitos.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

IF EXISTS (
    SELECT 1 FROM dbo.dim_municipio
    WHERE LEN(LTRIM(RTRIM(codigo_dane))) <> 5
)
BEGIN
    RAISERROR(N'dim_municipio: codigo_dane debe tener 5 dígitos.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

IF EXISTS (
    SELECT codigo_dane, cod_departamento, cod_municipio
    FROM dbo.dim_municipio
    WHERE codigo_dane <> cod_departamento + cod_municipio
)
BEGIN
    RAISERROR(N'dim_municipio: codigo_dane <> cod_departamento + cod_municipio.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

/* --- JOIN dim_municipio LEFT JOIN dim_departamento: detectar cod_departamento huérfano --- */
IF EXISTS (
    SELECT m.codigo_dane
    FROM dbo.dim_municipio AS m
    LEFT JOIN dbo.dim_departamento AS d ON d.cod_departamento = m.cod_departamento
    WHERE d.id_departamento IS NULL
)
BEGIN
    RAISERROR(N'dim_municipio: cod_departamento huérfano (no existe en dim_departamento).', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

/* --- 5. Restricciones únicas dim_municipio --- */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_dim_municipio_codigo_dane'
      AND object_id = OBJECT_ID(N'dbo.dim_municipio')
)
BEGIN
    ALTER TABLE dbo.dim_municipio
    ADD CONSTRAINT UQ_dim_municipio_codigo_dane UNIQUE (codigo_dane);
    PRINT N'Creada UQ_dim_municipio_codigo_dane';
END
ELSE
    PRINT N'UQ_dim_municipio_codigo_dane ya existe';

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_dim_municipio_cod_departamento_cod_municipio'
      AND object_id = OBJECT_ID(N'dbo.dim_municipio')
)
BEGIN
    ALTER TABLE dbo.dim_municipio
    ADD CONSTRAINT UQ_dim_municipio_cod_departamento_cod_municipio UNIQUE (cod_departamento, cod_municipio);
    PRINT N'Creada UQ_dim_municipio_cod_departamento_cod_municipio';
END
ELSE
    PRINT N'UQ_dim_municipio_cod_departamento_cod_municipio ya existe';

COMMIT TRANSACTION;
GO

PRINT N'=== ETAPA 2 — dim_area_residencia (contaminacion sexo) ===';
GO

/* --- ALTER TABLE: columna estado para marcar áreas activas/inactivas --- */
IF COL_LENGTH(N'dbo.dim_area_residencia', N'estado') IS NULL
BEGIN
    ALTER TABLE dbo.dim_area_residencia
    ADD estado bit NOT NULL CONSTRAINT DF_dim_area_residencia_estado DEFAULT (1);
    PRINT N'Columna estado agregada a dim_area_residencia (default 1 = activo).';
END
GO

BEGIN TRANSACTION;

UPDATE dbo.dim_area_residencia
SET estado = 0
WHERE UPPER(LTRIM(RTRIM(area_normalizada))) IN (N'FEMENINO', N'MASCULINO')
   OR UPPER(LTRIM(RTRIM(area_original))) IN (N'FEMENINO', N'MASCULINO');

PRINT N'dim_area_residencia filas desactivadas (FEMENINO/MASCULINO): ' + CAST(@@ROWCOUNT AS nvarchar(20));

COMMIT TRANSACTION;
GO

PRINT N'=== FIN 04_normalizacion_catalogos_geograficos ===';
GO
