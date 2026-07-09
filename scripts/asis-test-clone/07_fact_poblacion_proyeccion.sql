/*
================================================================================
 07_fact_poblacion_proyeccion.sql
================================================================================
 PROPÓSITO:
   Crea la tabla hecho fact_poblacion_proyeccion y la función auxiliar
   fn_ASIS_Resolver_IdArea para normalizar proyecciones de población DANE
   (nacional, departamental y municipal) con versionamiento por proyección.

 BASE DE DATOS DESTINO:
   ObservatorioDB_ASIS_Test (exclusivamente; el script valida DB_NAME()).

 DEPENDENCIAS (ejecutar antes):
   - 04_normalizacion_catalogos_geograficos.sql  (dim_departamento, dim_municipio)
   - 05_map_area_residencia_fuente.sql           (map_area_residencia_fuente, dim_area_residencia)
   - 14_proyeccion_dane_versionamiento.sql       (dim_proyeccion_dane, vistas poblacion)
   Tablas fuente DANE en staging: PPED_AreaSexoEdadNac_1950_2070,
   Poblacion_por_Departamento, PPED-AreaSexoEdadMun-2018-2042_VP.

 ORDEN DE EJECUCIÓN:
   07 (este script) -> 08/09/10 (SPs por nivel) -> 11 (orquestador) -> 12 (validación)
================================================================================
*/
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* --- Validación: solo se permite ejecutar en la BD de prueba ASIS --- */
IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
BEGIN
    DECLARE @db_err sysname = DB_NAME();
    RAISERROR(N'Solo ObservatorioDB_ASIS_Test. Base actual: %s', 16, 1, @db_err);
    RETURN;
END
GO

/*
  Función fn_ASIS_Resolver_IdArea
  Convierte el texto de área geográfica de las tablas DANE (ej. "1 - CABECERA")
  al id_area de dim_area_residencia mediante map_area_residencia_fuente.
  Retorna NULL para TOTAL o valores vacíos.
*/
CREATE OR ALTER FUNCTION dbo.fn_ASIS_Resolver_IdArea (@valor nvarchar(300))
RETURNS int
AS
BEGIN
    DECLARE @v nvarchar(300) = UPPER(LTRIM(RTRIM(@valor)));
    IF @v IS NULL OR @v = N'' OR @v = N'TOTAL'
        RETURN NULL;
    /* Caso especial: centro poblado codificado como "2 - CENTRO POBLADO" */
    IF @v LIKE N'2 - CENTRO POBLADO%'
        RETURN (SELECT id_area FROM dbo.dim_area_residencia WHERE codigo_area = N'2' AND estado = 1);
    DECLARE @id int;
    /* Homologación estándar vía tabla de mapeo vigente */
    SELECT TOP (1) @id = m.id_area
    FROM dbo.map_area_residencia_fuente AS m
    WHERE m.vigente = 1
      AND m.id_area IS NOT NULL
      AND UPPER(LTRIM(RTRIM(m.valor_origen))) = @v;
    RETURN @id;
END
GO

/*
  Tabla fact_poblacion_proyeccion
  Almacena población proyectada desagregada por nivel territorial (NACION,
  DEPARTAMENTO, MUNICIPIO), sexo, edad y área. Cada fila referencia una
  versión de proyección DANE (id_proyeccion_dane).
*/
IF OBJECT_ID(N'dbo.fact_poblacion_proyeccion', N'U') IS NULL
BEGIN
    IF OBJECT_ID(N'dbo.dim_proyeccion_dane', N'U') IS NULL
    BEGIN
        RAISERROR(N'Ejecute primero 14_proyeccion_dane_versionamiento.sql (dim_proyeccion_dane).', 16, 1);
        RETURN;
    END

    CREATE TABLE dbo.fact_poblacion_proyeccion (
        id_poblacion_proyeccion int           NOT NULL IDENTITY(1, 1),
        id_proyeccion_dane      int           NOT NULL,   /* FK a dim_proyeccion_dane */
        nivel_territorial       varchar(20)   NOT NULL,   /* NACION | DEPARTAMENTO | MUNICIPIO */
        tipo_registro           varchar(30)   NOT NULL,   /* EDAD_SIMPLE | TOTAL_SEXO | TOTAL_GENERAL */
        id_departamento         int           NULL,
        id_municipio            int           NULL,
        cod_departamento        char(2)       NULL,       /* DANE 2 dígitos; 00 = nacional */
        cod_municipio           char(3)       NULL,
        codigo_dane             char(5)       NULL,       /* Código DANE completo municipio */
        anio                    int           NOT NULL,
        id_area                 int           NULL,       /* FK dim_area_residencia (urbano/rural) */
        id_sexo                 int           NOT NULL,
        edad_simple             int           NULL,       /* 0-100 para EDAD_SIMPLE */
        edad_etiqueta           varchar(20)   NULL,
        id_grupo_edad           int           NULL,
        id_curso_vida           int           NULL,
        poblacion               bigint        NOT NULL,
        fuente_tabla            varchar(150)  NOT NULL,   /* Nombre tabla origen DANE */
        fecha_cargue            datetime      NOT NULL CONSTRAINT DF_fact_pob_fecha DEFAULT (GETDATE()),
        /* Columnas calculadas persistidas para índice único con NULLs */
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

    /* Índice único: evita duplicar el mismo grano por proyección y fuente */
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

/* Las vistas de población se crean en 14_proyeccion_dane_versionamiento.sql */
PRINT N'Vistas poblacion: ejecutar 14_proyeccion_dane_versionamiento.sql';
GO

PRINT N'07_fact_poblacion_proyeccion.sql OK';
GO
