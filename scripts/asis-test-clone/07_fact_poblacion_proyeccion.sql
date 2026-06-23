/*
FASE 0 — Tabla fact_poblacion_proyeccion, función área y vistas ASIS.
SOLO ObservatorioDB_ASIS_Test.
*/
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Solo ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

CREATE OR ALTER FUNCTION dbo.fn_ASIS_Resolver_IdArea (@valor nvarchar(300))
RETURNS int
AS
BEGIN
    DECLARE @v nvarchar(300) = UPPER(LTRIM(RTRIM(@valor)));
    IF @v IS NULL OR @v = N'' OR @v = N'TOTAL'
        RETURN NULL;
    IF @v LIKE N'2 - CENTRO POBLADO%'
        RETURN (SELECT id_area FROM dbo.dim_area_residencia WHERE codigo_area = N'2' AND estado = 1);
    DECLARE @id int;
    SELECT TOP (1) @id = m.id_area
    FROM dbo.map_area_residencia_fuente AS m
    WHERE m.vigente = 1
      AND m.id_area IS NOT NULL
      AND UPPER(LTRIM(RTRIM(m.valor_origen))) = @v;
    RETURN @id;
END
GO

IF OBJECT_ID(N'dbo.fact_poblacion_proyeccion', N'U') IS NULL
BEGIN
    IF OBJECT_ID(N'dbo.dim_proyeccion_dane', N'U') IS NULL
    BEGIN
        RAISERROR(N'Ejecute primero 14_proyeccion_dane_versionamiento.sql (dim_proyeccion_dane).', 16, 1);
        RETURN;
    END

    CREATE TABLE dbo.fact_poblacion_proyeccion (
        id_poblacion_proyeccion int           NOT NULL IDENTITY(1, 1),
        id_proyeccion_dane      int           NOT NULL,
        nivel_territorial       varchar(20)   NOT NULL,
        tipo_registro           varchar(30)   NOT NULL,
        id_departamento         int           NULL,
        id_municipio            int           NULL,
        cod_departamento        char(2)       NULL,
        cod_municipio           char(3)       NULL,
        codigo_dane             char(5)       NULL,
        anio                    int           NOT NULL,
        id_area                 int           NULL,
        id_sexo                 int           NOT NULL,
        edad_simple             int           NULL,
        edad_etiqueta           varchar(20)   NULL,
        id_grupo_edad           int           NULL,
        id_curso_vida           int           NULL,
        poblacion               bigint        NOT NULL,
        fuente_tabla            varchar(150)  NOT NULL,
        fecha_cargue            datetime      NOT NULL CONSTRAINT DF_fact_pob_fecha DEFAULT (GETDATE()),
        uq_cod_dep              AS ISNULL(cod_departamento, '') PERSISTED,
        uq_cod_mun              AS ISNULL(cod_municipio, '') PERSISTED,
        uq_cod_dane             AS ISNULL(codigo_dane, '') PERSISTED,
        uq_id_area              AS ISNULL(id_area, -1) PERSISTED,
        uq_edad                 AS ISNULL(edad_simple, -1) PERSISTED,
        CONSTRAINT PK_fact_poblacion_proyeccion PRIMARY KEY CLUSTERED (id_poblacion_proyeccion),
        CONSTRAINT CK_fact_pob_nivel CHECK (nivel_territorial IN (N'NACION', N'DEPARTAMENTO', N'MUNICIPIO')),
        CONSTRAINT CK_fact_pob_tipo CHECK (tipo_registro IN (N'EDAD_SIMPLE', N'TOTAL_SEXO', N'TOTAL_GENERAL')),
        CONSTRAINT FK_fact_pob_proyeccion_dane FOREIGN KEY (id_proyeccion_dane) REFERENCES dbo.dim_proyeccion_dane (id_proyeccion_dane),
        CONSTRAINT FK_fact_pob_dep FOREIGN KEY (id_departamento) REFERENCES dbo.dim_departamento (id_departamento),
        CONSTRAINT FK_fact_pob_mun FOREIGN KEY (id_municipio) REFERENCES dbo.dim_municipio (id_municipio),
        CONSTRAINT FK_fact_pob_area FOREIGN KEY (id_area) REFERENCES dbo.dim_area_residencia (id_area),
        CONSTRAINT FK_fact_pob_sexo FOREIGN KEY (id_sexo) REFERENCES dbo.dim_sexo (id_sexo)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_fact_pob_grano
        ON dbo.fact_poblacion_proyeccion (
            id_proyeccion_dane,
            nivel_territorial, tipo_registro, fuente_tabla,
            uq_cod_dep, uq_cod_mun, uq_cod_dane, anio, uq_id_area, id_sexo, uq_edad
        );

    PRINT N'Tabla fact_poblacion_proyeccion creada.';
END
ELSE
    PRINT N'fact_poblacion_proyeccion ya existe.';
GO

PRINT N'Vistas poblacion: ejecutar 14_proyeccion_dane_versionamiento.sql';
GO

PRINT N'07_fact_poblacion_proyeccion.sql OK';
GO
