/*
FASE 0 — Versionamiento proyecciones DANE.
dim_proyeccion_dane + id_proyeccion_dane en fact + vistas + usp_ASIS_CrearProyeccionDANE.
SOLO ObservatorioDB_ASIS_Test.

Ejecutar:
  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\14_proyeccion_dane_versionamiento.sql
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

/* --- dim_proyeccion_dane --- */
IF OBJECT_ID(N'dbo.dim_proyeccion_dane', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.dim_proyeccion_dane (
        id_proyeccion_dane  int           NOT NULL IDENTITY(1, 1),
        nombre_proyeccion   varchar(150)  NOT NULL,
        anio_publicacion    int           NOT NULL,
        fuente              varchar(200)  NULL,
        descripcion         varchar(500)  NULL,
        fecha_cargue        datetime      NOT NULL CONSTRAINT DF_dim_proy_dane_fecha DEFAULT (GETDATE()),
        estado              bit           NOT NULL CONSTRAINT DF_dim_proy_dane_estado DEFAULT (1),
        CONSTRAINT PK_dim_proyeccion_dane PRIMARY KEY CLUSTERED (id_proyeccion_dane),
        CONSTRAINT UQ_dim_proyeccion_dane_nombre_anio UNIQUE (nombre_proyeccion, anio_publicacion)
    );
    PRINT N'Tabla dim_proyeccion_dane creada.';
END
ELSE
    PRINT N'dim_proyeccion_dane ya existe.';
GO

/* --- Migrar fact existente --- */
IF NOT EXISTS (
    SELECT 1 FROM dbo.dim_proyeccion_dane
    WHERE nombre_proyeccion = N'Proyeccion DANE carga inicial'
      AND anio_publicacion = 2025
)
    INSERT dbo.dim_proyeccion_dane (nombre_proyeccion, anio_publicacion, fuente, descripcion)
    VALUES (
        N'Proyeccion DANE carga inicial',
        2025,
        N'Migracion FASE0 versionamiento',
        N'Datos normalizados antes de id_proyeccion_dane'
    );
GO

IF OBJECT_ID(N'dbo.fact_poblacion_proyeccion', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.fact_poblacion_proyeccion', N'id_proyeccion_dane') IS NULL
BEGIN
    ALTER TABLE dbo.fact_poblacion_proyeccion ADD id_proyeccion_dane int NULL;
    PRINT N'Columna id_proyeccion_dane agregada (nullable).';
END
GO

IF COL_LENGTH(N'dbo.fact_poblacion_proyeccion', N'id_proyeccion_dane') IS NOT NULL
BEGIN
    UPDATE f
    SET f.id_proyeccion_dane = d.id_proyeccion_dane
    FROM dbo.fact_poblacion_proyeccion AS f
    CROSS JOIN dbo.dim_proyeccion_dane AS d
    WHERE d.nombre_proyeccion = N'Proyeccion DANE carga inicial'
      AND d.anio_publicacion = 2025
      AND f.id_proyeccion_dane IS NULL;

    PRINT N'Filas actualizadas con id_proyeccion_dane: ' + CAST(@@ROWCOUNT AS nvarchar(20));
END
GO

IF COL_LENGTH(N'dbo.fact_poblacion_proyeccion', N'id_proyeccion_dane') IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.fact_poblacion_proyeccion WHERE id_proyeccion_dane IS NULL)
BEGIN
    RAISERROR(N'Quedaron filas sin id_proyeccion_dane. Revisar migracion.', 16, 1);
END
GO

IF COL_LENGTH(N'dbo.fact_poblacion_proyeccion', N'id_proyeccion_dane') IS NOT NULL
BEGIN
    ALTER TABLE dbo.fact_poblacion_proyeccion
    ALTER COLUMN id_proyeccion_dane int NOT NULL;
    PRINT N'id_proyeccion_dane definido NOT NULL.';
END
GO

IF OBJECT_ID(N'dbo.fact_poblacion_proyeccion', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.foreign_keys
       WHERE name = N'FK_fact_pob_proyeccion_dane'
         AND parent_object_id = OBJECT_ID(N'dbo.fact_poblacion_proyeccion')
   )
BEGIN
    ALTER TABLE dbo.fact_poblacion_proyeccion
    ADD CONSTRAINT FK_fact_pob_proyeccion_dane
        FOREIGN KEY (id_proyeccion_dane) REFERENCES dbo.dim_proyeccion_dane (id_proyeccion_dane);
    PRINT N'FK_fact_pob_proyeccion_dane creada.';
END
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_fact_pob_grano'
      AND object_id = OBJECT_ID(N'dbo.fact_poblacion_proyeccion')
)
BEGIN
    DROP INDEX UQ_fact_pob_grano ON dbo.fact_poblacion_proyeccion;
    PRINT N'Indice UQ_fact_pob_grano anterior eliminado.';
END
GO

IF OBJECT_ID(N'dbo.fact_poblacion_proyeccion', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.fact_poblacion_proyeccion', N'id_proyeccion_dane') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.indexes
       WHERE name = N'UQ_fact_pob_grano'
         AND object_id = OBJECT_ID(N'dbo.fact_poblacion_proyeccion')
   )
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_fact_pob_grano
        ON dbo.fact_poblacion_proyeccion (
            id_proyeccion_dane,
            nivel_territorial,
            tipo_registro,
            uq_cod_dep,
            uq_cod_mun,
            uq_cod_dane,
            anio,
            uq_id_area,
            id_sexo,
            uq_edad,
            fuente_tabla
        );
    PRINT N'Indice UQ_fact_pob_grano recreado con id_proyeccion_dane.';
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_ASIS_CrearProyeccionDANE
    @nombre_proyeccion varchar(150),
    @anio_publicacion int,
    @fuente           varchar(200) = NULL,
    @descripcion      varchar(500) = NULL,
    @id_proyeccion_dane int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF DB_NAME() <> N'ObservatorioDB_ASIS_Test'
    BEGIN
        RAISERROR(N'Solo ObservatorioDB_ASIS_Test.', 16, 1);
        RETURN;
    END

    IF @nombre_proyeccion IS NULL OR LTRIM(RTRIM(@nombre_proyeccion)) = N''
       OR @anio_publicacion IS NULL
    BEGIN
        RAISERROR(N'nombre_proyeccion y anio_publicacion son obligatorios.', 16, 1);
        RETURN;
    END

    SET @nombre_proyeccion = LTRIM(RTRIM(@nombre_proyeccion));

    SELECT @id_proyeccion_dane = d.id_proyeccion_dane
    FROM dbo.dim_proyeccion_dane AS d
    WHERE d.nombre_proyeccion = @nombre_proyeccion
      AND d.anio_publicacion = @anio_publicacion;

    IF @id_proyeccion_dane IS NOT NULL
    BEGIN
        PRINT N'Proyeccion existente id=' + CAST(@id_proyeccion_dane AS nvarchar(20))
            + N' (' + @nombre_proyeccion + N' / ' + CAST(@anio_publicacion AS nvarchar(10)) + N')';
        RETURN;
    END

    INSERT dbo.dim_proyeccion_dane (nombre_proyeccion, anio_publicacion, fuente, descripcion)
    VALUES (@nombre_proyeccion, @anio_publicacion, @fuente, @descripcion);

    SET @id_proyeccion_dane = SCOPE_IDENTITY();
    PRINT N'Proyeccion creada id=' + CAST(@id_proyeccion_dane AS nvarchar(20));
END
GO

/* --- Vistas con versionamiento --- */
CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Nacional
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    pd.anio_publicacion,
    f.anio,
    f.id_poblacion_proyeccion,
    f.cod_departamento,
    f.codigo_dane,
    da.area_normalizada,
    ds.sexo,
    f.edad_simple,
    f.edad_etiqueta,
    f.poblacion,
    f.fuente_tabla
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
LEFT JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
WHERE f.nivel_territorial = N'NACION' AND f.tipo_registro = N'EDAD_SIMPLE';
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Departamental
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    pd.anio_publicacion,
    f.anio,
    f.id_poblacion_proyeccion,
    f.cod_departamento,
    de.nombre_departamento,
    da.area_normalizada,
    ds.sexo,
    f.poblacion,
    f.fuente_tabla
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
LEFT JOIN dbo.dim_departamento AS de ON de.id_departamento = f.id_departamento
LEFT JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
WHERE f.nivel_territorial = N'DEPARTAMENTO' AND f.tipo_registro = N'TOTAL_SEXO';
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Municipal
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    pd.anio_publicacion,
    f.anio,
    f.id_poblacion_proyeccion,
    f.cod_departamento,
    f.codigo_dane,
    mu.nombre_municipio,
    da.area_normalizada,
    ds.sexo,
    f.edad_simple,
    f.edad_etiqueta,
    f.poblacion,
    f.fuente_tabla
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
LEFT JOIN dbo.dim_municipio AS mu ON mu.id_municipio = f.id_municipio
LEFT JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
WHERE f.nivel_territorial = N'MUNICIPIO' AND f.tipo_registro = N'EDAD_SIMPLE';
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Piramide
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    pd.anio_publicacion,
    f.anio,
    f.cod_departamento,
    f.codigo_dane,
    mu.nombre_municipio,
    da.area_normalizada,
    ds.sexo,
    f.edad_simple,
    f.edad_etiqueta,
    SUM(f.poblacion) AS poblacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
LEFT JOIN dbo.dim_municipio AS mu ON mu.id_municipio = f.id_municipio
LEFT JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
WHERE f.nivel_territorial = N'MUNICIPIO'
  AND f.tipo_registro = N'EDAD_SIMPLE'
  AND f.cod_departamento = N'85'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, pd.anio_publicacion, f.anio,
         f.cod_departamento, f.codigo_dane, mu.nombre_municipio,
         da.area_normalizada, ds.sexo, f.edad_simple, f.edad_etiqueta;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_CursoVida
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    pd.anio_publicacion,
    f.anio,
    f.nivel_territorial,
    f.cod_departamento,
    f.codigo_dane,
    da.area_normalizada,
    ds.sexo,
    dc.nombre_curso_vida,
    f.edad_simple,
    SUM(f.poblacion) AS poblacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
LEFT JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
LEFT JOIN dbo.dim_curso_vida AS dc
    ON f.edad_simple IS NOT NULL
   AND f.edad_simple BETWEEN dc.edad_minima AND dc.edad_maxima
WHERE f.tipo_registro = N'EDAD_SIMPLE'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, pd.anio_publicacion, f.anio,
         f.nivel_territorial, f.cod_departamento, f.codigo_dane,
         da.area_normalizada, ds.sexo, dc.nombre_curso_vida, f.edad_simple;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_GrupoEdad
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    pd.anio_publicacion,
    f.anio,
    f.nivel_territorial,
    f.cod_departamento,
    f.codigo_dane,
    da.area_normalizada,
    ds.sexo,
    dg.nombre_grupo_edad,
    f.edad_simple,
    SUM(f.poblacion) AS poblacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
LEFT JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
LEFT JOIN dbo.dim_grupo_edad AS dg
    ON f.edad_simple IS NOT NULL
   AND f.edad_simple BETWEEN dg.edad_minima AND dg.edad_maxima
WHERE f.tipo_registro = N'EDAD_SIMPLE'
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, pd.anio_publicacion, f.anio,
         f.nivel_territorial, f.cod_departamento, f.codigo_dane,
         da.area_normalizada, ds.sexo, dg.nombre_grupo_edad, f.edad_simple;
GO

CREATE OR ALTER VIEW dbo.vw_ASIS_Poblacion_Total_Sexo_Area
AS
SELECT
    f.id_proyeccion_dane,
    pd.nombre_proyeccion,
    pd.anio_publicacion,
    f.anio,
    f.nivel_territorial,
    f.tipo_registro,
    f.cod_departamento,
    f.codigo_dane,
    da.area_normalizada,
    ds.sexo,
    SUM(f.poblacion) AS poblacion
FROM dbo.fact_poblacion_proyeccion AS f
INNER JOIN dbo.dim_proyeccion_dane AS pd ON pd.id_proyeccion_dane = f.id_proyeccion_dane
LEFT JOIN dbo.dim_area_residencia AS da ON da.id_area = f.id_area
INNER JOIN dbo.dim_sexo AS ds ON ds.id_sexo = f.id_sexo
WHERE f.tipo_registro IN (N'TOTAL_SEXO', N'EDAD_SIMPLE')
GROUP BY f.id_proyeccion_dane, pd.nombre_proyeccion, pd.anio_publicacion, f.anio,
         f.nivel_territorial, f.tipo_registro, f.cod_departamento, f.codigo_dane,
         da.area_normalizada, ds.sexo;
GO

PRINT N'14_proyeccion_dane_versionamiento.sql OK';
GO
