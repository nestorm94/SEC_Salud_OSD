/*
Catalogos nivel educativo y pertenencia etnica (nacimientos) + columnas en fact.

  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -f 65001 -i scripts\asis-test-clone\24_catalogos_nacimientos_educacion_etnia.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_NAME() NOT IN (N'ObservatorioDB_ASIS_Test', N'ObservatorioDB')
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Ejecutar en ObservatorioDB u ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

PRINT N'=== 24 - Catalogos educacion y etnia nacimientos ===';

IF OBJECT_ID(N'dbo.dim_nivel_educativo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.dim_nivel_educativo (
        id_nivel_educativo   int           IDENTITY(1, 1) NOT NULL,
        codigo               varchar(20)   NOT NULL,
        codigo_dane          varchar(20)   NULL,
        etiqueta_dane        nvarchar(300) NOT NULL,
        orden_visualizacion  int           NOT NULL,
        estado               bit           NOT NULL CONSTRAINT DF_dim_ne_estado DEFAULT (1),
        fecha_creacion       datetime2     NOT NULL CONSTRAINT DF_dim_ne_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_dim_nivel_educativo PRIMARY KEY CLUSTERED (id_nivel_educativo),
        CONSTRAINT UQ_dim_ne_codigo UNIQUE (codigo),
        CONSTRAINT UQ_dim_ne_etiqueta UNIQUE (etiqueta_dane)
    );
END

IF OBJECT_ID(N'dbo.dim_pertenencia_etnica', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.dim_pertenencia_etnica (
        id_pertenencia_etnica int           IDENTITY(1, 1) NOT NULL,
        codigo                varchar(20)   NOT NULL,
        codigo_dane           varchar(20)   NULL,
        etiqueta_dane         nvarchar(300) NOT NULL,
        orden_visualizacion   int           NOT NULL,
        estado                bit           NOT NULL CONSTRAINT DF_dim_pe_estado DEFAULT (1),
        fecha_creacion        datetime2     NOT NULL CONSTRAINT DF_dim_pe_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_dim_pertenencia_etnica PRIMARY KEY CLUSTERED (id_pertenencia_etnica),
        CONSTRAINT UQ_dim_pe_codigo UNIQUE (codigo),
        CONSTRAINT UQ_dim_pe_etiqueta UNIQUE (etiqueta_dane)
    );
END

IF OBJECT_ID(N'dbo.map_nivel_educativo_fuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.map_nivel_educativo_fuente (
        id_mapeo             int           IDENTITY(1, 1) NOT NULL,
        fuente_tabla         varchar(150)  NOT NULL,
        columna_origen       varchar(150)  NOT NULL,
        valor_origen         nvarchar(300) NOT NULL,
        id_nivel_educativo   int           NOT NULL,
        vigente              bit           NOT NULL CONSTRAINT DF_map_ne_vigente DEFAULT (1),
        fecha_creacion       datetime2     NOT NULL CONSTRAINT DF_map_ne_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_map_nivel_educativo_fuente PRIMARY KEY CLUSTERED (id_mapeo),
        CONSTRAINT FK_map_ne_dim FOREIGN KEY (id_nivel_educativo) REFERENCES dbo.dim_nivel_educativo (id_nivel_educativo)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_map_ne_fuente_valor
        ON dbo.map_nivel_educativo_fuente (fuente_tabla, columna_origen, valor_origen);
END

IF OBJECT_ID(N'dbo.map_pertenencia_etnica_fuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.map_pertenencia_etnica_fuente (
        id_mapeo              int           IDENTITY(1, 1) NOT NULL,
        fuente_tabla          varchar(150)  NOT NULL,
        columna_origen        varchar(150)  NOT NULL,
        valor_origen          nvarchar(300) NOT NULL,
        id_pertenencia_etnica int           NOT NULL,
        vigente               bit           NOT NULL CONSTRAINT DF_map_pe_vigente DEFAULT (1),
        fecha_creacion        datetime2     NOT NULL CONSTRAINT DF_map_pe_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_map_pertenencia_etnica_fuente PRIMARY KEY CLUSTERED (id_mapeo),
        CONSTRAINT FK_map_pe_dim FOREIGN KEY (id_pertenencia_etnica) REFERENCES dbo.dim_pertenencia_etnica (id_pertenencia_etnica)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_map_pe_fuente_valor
        ON dbo.map_pertenencia_etnica_fuente (fuente_tabla, columna_origen, valor_origen);
END

IF COL_LENGTH(N'dbo.fact_nacimientos_casanare_normalizada', N'id_nivel_educativo') IS NULL
    ALTER TABLE dbo.fact_nacimientos_casanare_normalizada ADD id_nivel_educativo int NULL;

IF COL_LENGTH(N'dbo.fact_nacimientos_casanare_normalizada', N'id_pertenencia_etnica') IS NULL
    ALTER TABLE dbo.fact_nacimientos_casanare_normalizada ADD id_pertenencia_etnica int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_fact_nac_nivel_educativo')
    ALTER TABLE dbo.fact_nacimientos_casanare_normalizada WITH NOCHECK
        ADD CONSTRAINT FK_fact_nac_nivel_educativo FOREIGN KEY (id_nivel_educativo)
            REFERENCES dbo.dim_nivel_educativo (id_nivel_educativo);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_fact_nac_pertenencia_etnica')
    ALTER TABLE dbo.fact_nacimientos_casanare_normalizada WITH NOCHECK
        ADD CONSTRAINT FK_fact_nac_pertenencia_etnica FOREIGN KEY (id_pertenencia_etnica)
            REFERENCES dbo.dim_pertenencia_etnica (id_pertenencia_etnica);
GO

CREATE OR ALTER PROCEDURE dbo.usp_sync_catalogos_nacimientos_staging
AS
BEGIN
    SET NOCOUNT ON;

    IF OBJECT_ID(N'dbo.nacimientos_casanare_staging', N'U') IS NULL RETURN;

    ;WITH ne AS (
        SELECT DISTINCT LTRIM(RTRIM(nivel_educativo)) AS etiqueta
        FROM dbo.nacimientos_casanare_staging
        WHERE nivel_educativo IS NOT NULL AND LTRIM(RTRIM(nivel_educativo)) <> N''
    )
    INSERT INTO dbo.dim_nivel_educativo (codigo, codigo_dane, etiqueta_dane, orden_visualizacion)
    SELECT
        N'NE' + RIGHT(N'00' + CAST(ROW_NUMBER() OVER (ORDER BY
            CASE WHEN ne.etiqueta LIKE N'SIN%' THEN 99
                 ELSE TRY_CAST(LEFT(ne.etiqueta, PATINDEX(N'%[^0-9]%', ne.etiqueta + N'X') - 1) AS int) END,
            ne.etiqueta) AS nvarchar(10)), 2),
        CASE WHEN ne.etiqueta LIKE N'[0-9]%' THEN LEFT(ne.etiqueta, PATINDEX(N'% -%', ne.etiqueta + N' -') - 1) ELSE N'SIN' END,
        ne.etiqueta,
        CASE WHEN ne.etiqueta LIKE N'SIN%' THEN 99
             ELSE ISNULL(TRY_CAST(LEFT(ne.etiqueta, PATINDEX(N'%[^0-9]%', ne.etiqueta + N'X') - 1) AS int), 50) END
    FROM ne
    WHERE NOT EXISTS (SELECT 1 FROM dbo.dim_nivel_educativo AS d WHERE d.etiqueta_dane = ne.etiqueta);

    ;WITH pe AS (
        SELECT DISTINCT LTRIM(RTRIM(pertenencia_etnica)) AS etiqueta
        FROM dbo.nacimientos_casanare_staging
        WHERE pertenencia_etnica IS NOT NULL AND LTRIM(RTRIM(pertenencia_etnica)) <> N''
    )
    INSERT INTO dbo.dim_pertenencia_etnica (codigo, codigo_dane, etiqueta_dane, orden_visualizacion)
    SELECT
        N'PE' + RIGHT(N'00' + CAST(ROW_NUMBER() OVER (ORDER BY
            CASE WHEN pe.etiqueta LIKE N'NO REPORTADO%' THEN 98
                 WHEN pe.etiqueta LIKE N'[0-9]%' THEN TRY_CAST(LEFT(pe.etiqueta, 1) AS int)
                 ELSE 50 END,
            pe.etiqueta) AS nvarchar(10)), 2),
        CASE WHEN pe.etiqueta LIKE N'[0-9]%' THEN LEFT(pe.etiqueta, 1) ELSE N'NR' END,
        pe.etiqueta,
        CASE WHEN pe.etiqueta LIKE N'NO REPORTADO%' THEN 98
             WHEN pe.etiqueta LIKE N'[0-9]%' THEN TRY_CAST(LEFT(pe.etiqueta, 1) AS int)
             ELSE 50 END
    FROM pe
    WHERE NOT EXISTS (SELECT 1 FROM dbo.dim_pertenencia_etnica AS d WHERE d.etiqueta_dane = pe.etiqueta);

    DELETE FROM dbo.map_nivel_educativo_fuente WHERE fuente_tabla = N'nacimientos_casanare';
    INSERT dbo.map_nivel_educativo_fuente (fuente_tabla, columna_origen, valor_origen, id_nivel_educativo)
    SELECT N'nacimientos_casanare', N'nivel_educativo', d.etiqueta_dane, d.id_nivel_educativo
    FROM dbo.dim_nivel_educativo AS d;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.map_nivel_educativo_fuente
        WHERE fuente_tabla = N'nacimientos_casanare' AND valor_origen = N'SIN INFORMACION'
    )
        INSERT dbo.map_nivel_educativo_fuente (fuente_tabla, columna_origen, valor_origen, id_nivel_educativo)
        SELECT TOP 1 N'nacimientos_casanare', N'nivel_educativo', N'SIN INFORMACION', d.id_nivel_educativo
        FROM dbo.dim_nivel_educativo AS d WHERE d.etiqueta_dane LIKE N'SIN INFORM%';

    DELETE FROM dbo.map_pertenencia_etnica_fuente WHERE fuente_tabla = N'nacimientos_casanare';
    INSERT dbo.map_pertenencia_etnica_fuente (fuente_tabla, columna_origen, valor_origen, id_pertenencia_etnica)
    SELECT N'nacimientos_casanare', N'pertenencia_etnica', d.etiqueta_dane, d.id_pertenencia_etnica
    FROM dbo.dim_pertenencia_etnica AS d;
END
GO

EXEC dbo.usp_sync_catalogos_nacimientos_staging;
GO

PRINT N'=== FIN 24_catalogos_nacimientos_educacion_etnia ===';
GO
