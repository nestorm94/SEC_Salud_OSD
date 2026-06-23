/*
FASE 2 nacimientos — dim_grupo_edad_madre, staging y fact_nacimientos_casanare_normalizada.

  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\20_fact_nacimientos_estructura.sql
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

PRINT N'=== 20 - Estructura fact nacimientos ===';
GO

BEGIN TRANSACTION;

/* Quinquenios DANE edad de la madre (nacimientos) */
IF OBJECT_ID(N'dbo.dim_grupo_edad_madre', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.dim_grupo_edad_madre (
        id_grupo_edad_madre  int           IDENTITY(1, 1) NOT NULL,
        codigo               varchar(20)   NOT NULL,
        etiqueta_rango       nvarchar(150) NOT NULL,
        edad_minima          int           NULL,
        edad_maxima          int           NULL,
        es_madre_adolescente bit           NOT NULL CONSTRAINT DF_dim_gem_adolescente DEFAULT (0),
        orden_visualizacion  int           NOT NULL,
        estado               bit           NOT NULL CONSTRAINT DF_dim_gem_estado DEFAULT (1),
        fecha_creacion       datetime2     NOT NULL CONSTRAINT DF_dim_gem_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_dim_grupo_edad_madre PRIMARY KEY CLUSTERED (id_grupo_edad_madre),
        CONSTRAINT UQ_dim_gem_codigo UNIQUE (codigo),
        CONSTRAINT UQ_dim_gem_etiqueta UNIQUE (etiqueta_rango)
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.dim_grupo_edad_madre)
BEGIN
    SET IDENTITY_INSERT dbo.dim_grupo_edad_madre ON;
    INSERT dbo.dim_grupo_edad_madre (id_grupo_edad_madre, codigo, etiqueta_rango, edad_minima, edad_maxima, es_madre_adolescente, orden_visualizacion) VALUES
    ( 1, N'QM01', N'De 10 a 14 años',  10, 14, 1,  1),
    ( 2, N'QM02', N'De 15 a 19 años',  15, 19, 1,  2),
    ( 3, N'QM03', N'De 20 a 24 años',  20, 24, 0,  3),
    ( 4, N'QM04', N'De 25 a 29 años',  25, 29, 0,  4),
    ( 5, N'QM05', N'De 30 a 34 años',  30, 34, 0,  5),
    ( 6, N'QM06', N'De 35 a 39 años',  35, 39, 0,  6),
    ( 7, N'QM07', N'De 40 a 44 años',  40, 44, 0,  7),
    ( 8, N'QM08', N'De 45 a 49 años',  45, 49, 0,  8),
    ( 9, N'QM09', N'De 50 a 54 años',  50, 54, 0,  9),
    (10, N'QM10', N'De 55 a 59 años',  55, 59, 0, 10),
    (11, N'QM11', N'60 años y más',    60, NULL, 0, 11),
    (12, N'QM98', N'No reportado',    NULL, NULL, 0, 98),
    (13, N'QM99', N'Sin información', NULL, NULL, 0, 99);
    SET IDENTITY_INSERT dbo.dim_grupo_edad_madre OFF;
    PRINT N'dim_grupo_edad_madre: 13 categorias.';
END

IF OBJECT_ID(N'dbo.map_grupo_edad_madre_fuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.map_grupo_edad_madre_fuente (
        id_mapeo              int           IDENTITY(1, 1) NOT NULL,
        fuente_tabla          varchar(150)  NOT NULL,
        columna_origen        varchar(150)  NOT NULL,
        valor_origen          nvarchar(300) NOT NULL,
        id_grupo_edad_madre   int           NOT NULL,
        vigente               bit           NOT NULL CONSTRAINT DF_map_gem_vigente DEFAULT (1),
        fecha_creacion        datetime2     NOT NULL CONSTRAINT DF_map_gem_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_map_grupo_edad_madre_fuente PRIMARY KEY CLUSTERED (id_mapeo),
        CONSTRAINT FK_map_gem_dim FOREIGN KEY (id_grupo_edad_madre) REFERENCES dbo.dim_grupo_edad_madre (id_grupo_edad_madre)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_map_gem_fuente_valor
        ON dbo.map_grupo_edad_madre_fuente (fuente_tabla, columna_origen, valor_origen);
END
ELSE
    DELETE FROM dbo.map_grupo_edad_madre_fuente;

INSERT dbo.map_grupo_edad_madre_fuente (fuente_tabla, columna_origen, valor_origen, id_grupo_edad_madre)
SELECT N'nacimientos_casanare', N'grupo_etareo_quinquenios_dane', g.etiqueta_rango, g.id_grupo_edad_madre
FROM dbo.dim_grupo_edad_madre AS g;

INSERT dbo.map_grupo_edad_madre_fuente (fuente_tabla, columna_origen, valor_origen, id_grupo_edad_madre)
SELECT N'nacimientos_casanare', N'grupo_etareo_quinquenios_dane', N'SIN INFORMACION', g.id_grupo_edad_madre
FROM dbo.dim_grupo_edad_madre AS g WHERE g.codigo = N'QM99';

INSERT dbo.map_grupo_edad_madre_fuente (fuente_tabla, columna_origen, valor_origen, id_grupo_edad_madre)
SELECT N'nacimientos_casanare', N'grupo_etareo_quinquenios_dane', N'No Reportado', g.id_grupo_edad_madre
FROM dbo.dim_grupo_edad_madre AS g WHERE g.codigo = N'QM98';

INSERT dbo.map_grupo_edad_madre_fuente (fuente_tabla, columna_origen, valor_origen, id_grupo_edad_madre)
SELECT N'nacimientos_casanare', N'grupo_etareo_quinquenios_dane', N'SIN INFORMACIÓN', g.id_grupo_edad_madre
FROM dbo.dim_grupo_edad_madre AS g WHERE g.codigo = N'QM99';

/* Area nacimientos: codigo 1/2/3 y nombre sin prefijo DANE */
IF NOT EXISTS (SELECT 1 FROM dbo.map_area_residencia_fuente WHERE columna_origen = N'nombre_area_residencia' AND valor_origen = N'CABECERA')
    INSERT dbo.map_area_residencia_fuente (fuente_tabla, columna_origen, valor_origen, id_area, codigo_area, area_normalizada, vigente)
    SELECT N'nacimientos_casanare', N'nombre_area_residencia', N'CABECERA', id_area, codigo_area, area_normalizada, 1
    FROM dbo.dim_area_residencia WHERE codigo_area = N'1';

IF NOT EXISTS (SELECT 1 FROM dbo.map_area_residencia_fuente WHERE columna_origen = N'nombre_area_residencia' AND valor_origen = N'CENTRO POBLADO')
    INSERT dbo.map_area_residencia_fuente (fuente_tabla, columna_origen, valor_origen, id_area, codigo_area, area_normalizada, vigente)
    SELECT N'nacimientos_casanare', N'nombre_area_residencia', N'CENTRO POBLADO', id_area, codigo_area, area_normalizada, 1
    FROM dbo.dim_area_residencia WHERE codigo_area = N'2';

IF NOT EXISTS (SELECT 1 FROM dbo.map_area_residencia_fuente WHERE columna_origen = N'nombre_area_residencia' AND valor_origen = N'AREA RURAL DISPERSA')
    INSERT dbo.map_area_residencia_fuente (fuente_tabla, columna_origen, valor_origen, id_area, codigo_area, area_normalizada, vigente)
    SELECT N'nacimientos_casanare', N'nombre_area_residencia', N'AREA RURAL DISPERSA', id_area, codigo_area, area_normalizada, 1
    FROM dbo.dim_area_residencia WHERE codigo_area = N'3';

/* Staging CSV */
IF OBJECT_ID(N'dbo.nacimientos_casanare_staging', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.nacimientos_casanare_staging (
        codigo_departamento            varchar(10)   NULL,
        nombre_departamento            nvarchar(200) NULL,
        codigo_municipio               varchar(10)   NULL,
        nombre_municipio               nvarchar(200) NULL,
        vigencia                       int           NULL,
        codigo_area_residencia         varchar(10)   NULL,
        nombre_area_residencia         nvarchar(100) NULL,
        grupo_etareo_quinquenios_dane  nvarchar(200) NULL,
        nivel_educativo                nvarchar(200) NULL,
        pertenencia_etnica             nvarchar(200) NULL,
        sexo                           nvarchar(50)  NULL,
        peso_al_nacer                  nvarchar(200) NULL,
        semanas_gestacion              nvarchar(200) NULL,
        nacimientos                    int           NULL
    );
    PRINT N'Tabla nacimientos_casanare_staging creada.';
END

/* Fact */
IF OBJECT_ID(N'dbo.fact_nacimientos_casanare_normalizada', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.fact_nacimientos_casanare_normalizada (
        id_nacimiento            bigint        IDENTITY(1, 1) NOT NULL,
        codigo_departamento      nvarchar(10)  NOT NULL,
        codigo_municipio         nvarchar(10)  NULL,
        id_sexo                  int           NOT NULL,
        id_area                  int           NOT NULL,
        id_grupo_edad_madre      int           NOT NULL,
        id_peso_al_nacer         int           NULL,
        id_semanas_gestacion     int           NULL,
        anio                     int           NOT NULL,
        numero_nacimientos       int           NOT NULL,
        fecha_carga              datetime2     NOT NULL CONSTRAINT DF_fact_nac_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_fact_nacimientos_casanare_normalizada PRIMARY KEY CLUSTERED (id_nacimiento),
        CONSTRAINT FK_fact_nac_sexo FOREIGN KEY (id_sexo) REFERENCES dbo.dim_sexo (id_sexo),
        CONSTRAINT FK_fact_nac_area FOREIGN KEY (id_area) REFERENCES dbo.dim_area_residencia (id_area),
        CONSTRAINT FK_fact_nac_gem FOREIGN KEY (id_grupo_edad_madre) REFERENCES dbo.dim_grupo_edad_madre (id_grupo_edad_madre),
        CONSTRAINT FK_fact_nac_peso FOREIGN KEY (id_peso_al_nacer) REFERENCES dbo.dim_peso_al_nacer (id_peso_al_nacer),
        CONSTRAINT FK_fact_nac_sem FOREIGN KEY (id_semanas_gestacion) REFERENCES dbo.dim_semanas_gestacion (id_semanas_gestacion)
    );
    CREATE NONCLUSTERED INDEX IX_fact_nac_anio_muni ON dbo.fact_nacimientos_casanare_normalizada (anio, codigo_municipio);
    PRINT N'Tabla fact_nacimientos_casanare_normalizada creada.';
END

COMMIT TRANSACTION;
GO

PRINT N'=== FIN 20_fact_nacimientos_estructura ===';
GO
