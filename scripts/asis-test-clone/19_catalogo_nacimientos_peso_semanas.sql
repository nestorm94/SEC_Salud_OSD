/*
Catalogos parametrizados: peso al nacer y semanas de gestacion (nacimientos DANE).
Incluye categorias normalizadas para ASIS (bajo peso, prematuro, etc.) y map_*_fuente.

Ejecutar:
  sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\19_catalogo_nacimientos_peso_semanas.sql
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

PRINT N'=== 19 - Catalogos peso al nacer y semanas gestacion ===';
GO

BEGIN TRANSACTION;

/* --- dim_peso_al_nacer --- */
IF OBJECT_ID(N'dbo.dim_peso_al_nacer', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.dim_peso_al_nacer (
        id_peso_al_nacer       int           IDENTITY(1, 1) NOT NULL,
        codigo                 varchar(20)   NOT NULL,
        codigo_dane            varchar(20)   NOT NULL,
        nombre_categoria       varchar(150)  NOT NULL,
        etiqueta_rango         varchar(200)  NOT NULL,
        gramos_minimo          int           NULL,
        gramos_maximo          int           NULL,
        categoria_normalizada  varchar(80)   NOT NULL,
        es_bajo_peso           bit           NOT NULL CONSTRAINT DF_dim_peso_bajo DEFAULT (0),
        es_muy_bajo_peso       bit           NOT NULL CONSTRAINT DF_dim_peso_muy_bajo DEFAULT (0),
        orden_visualizacion    int           NOT NULL,
        estado                 bit           NOT NULL CONSTRAINT DF_dim_peso_estado DEFAULT (1),
        fecha_creacion         datetime2     NOT NULL CONSTRAINT DF_dim_peso_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_dim_peso_al_nacer PRIMARY KEY CLUSTERED (id_peso_al_nacer),
        CONSTRAINT UQ_dim_peso_codigo UNIQUE (codigo),
        CONSTRAINT UQ_dim_peso_codigo_dane UNIQUE (codigo_dane)
    );
    PRINT N'Tabla dim_peso_al_nacer creada.';
END

IF NOT EXISTS (SELECT 1 FROM dbo.dim_peso_al_nacer)
BEGIN
    SET IDENTITY_INSERT dbo.dim_peso_al_nacer ON;
    INSERT dbo.dim_peso_al_nacer (
        id_peso_al_nacer, codigo, codigo_dane, nombre_categoria, etiqueta_rango,
        gramos_minimo, gramos_maximo, categoria_normalizada, es_bajo_peso, es_muy_bajo_peso, orden_visualizacion
    ) VALUES
    (1, N'P01', N'1', N'Menor a 1000 gramos',           N'1 - MENOR A 1000 GRAMOS',           0,     999,  N'Muy bajo peso',           1, 1,  1),
    (2, N'P02', N'2', N'Entre 1000 y 1499 gramos',      N'2 - ENTRE 1000 Y 1499 GRAMOS',   1000,  1499,  N'Muy bajo peso',           1, 1,  2),
    (3, N'P03', N'3', N'Entre 1500 y 2499 gramos',      N'3 - ENTRE 1500 Y 2499 GRAMOS',   1500,  2499,  N'Bajo peso',                1, 0,  3),
    (4, N'P04', N'4', N'Entre 2500 y 3999 gramos',      N'4 - ENTRE 2500 Y 3999 GRAMOS',   2500,  3999,  N'Adecuado',                 0, 0,  4),
    (5, N'P05', N'5', N'Mayor o igual a 4000 gramos',   N'5 - MAYOR O IGUAL A 4000 GRAMOS', 4000,  NULL, N'Macrosomia',               0, 0,  5),
    (6, N'P99', N'SIN', N'Sin informacion',             N'SIN INFORMACION',                 NULL,  NULL, N'Sin informacion',          0, 0, 99);
    SET IDENTITY_INSERT dbo.dim_peso_al_nacer OFF;
    PRINT N'dim_peso_al_nacer: 6 categorias insertadas.';
END
ELSE
    PRINT N'dim_peso_al_nacer: ya tiene datos.';

/* --- dim_semanas_gestacion --- */
IF OBJECT_ID(N'dbo.dim_semanas_gestacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.dim_semanas_gestacion (
        id_semanas_gestacion   int           IDENTITY(1, 1) NOT NULL,
        codigo                 varchar(20)   NOT NULL,
        codigo_dane            varchar(20)   NOT NULL,
        nombre_categoria       varchar(150)  NOT NULL,
        etiqueta_rango         varchar(200)  NOT NULL,
        semanas_minima         int           NULL,
        semanas_maxima         int           NULL,
        categoria_normalizada  varchar(80)   NOT NULL,
        es_prematuro           bit           NOT NULL CONSTRAINT DF_dim_sem_prematuro DEFAULT (0),
        es_termino             bit           NOT NULL CONSTRAINT DF_dim_sem_termino DEFAULT (0),
        orden_visualizacion    int           NOT NULL,
        estado                 bit           NOT NULL CONSTRAINT DF_dim_sem_estado DEFAULT (1),
        fecha_creacion         datetime2     NOT NULL CONSTRAINT DF_dim_sem_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_dim_semanas_gestacion PRIMARY KEY CLUSTERED (id_semanas_gestacion),
        CONSTRAINT UQ_dim_sem_codigo UNIQUE (codigo),
        CONSTRAINT UQ_dim_sem_codigo_dane UNIQUE (codigo_dane)
    );
    PRINT N'Tabla dim_semanas_gestacion creada.';
END

IF NOT EXISTS (SELECT 1 FROM dbo.dim_semanas_gestacion)
BEGIN
    SET IDENTITY_INSERT dbo.dim_semanas_gestacion ON;
    INSERT dbo.dim_semanas_gestacion (
        id_semanas_gestacion, codigo, codigo_dane, nombre_categoria, etiqueta_rango,
        semanas_minima, semanas_maxima, categoria_normalizada, es_prematuro, es_termino, orden_visualizacion
    ) VALUES
    (1, N'SG01', N'1', N'Menos de 22 semanas',        N'1 - MENOS DE 22 SEMANAS',     0,  21, N'Prematuro extremo',  1, 0,  1),
    (2, N'SG02', N'2', N'De 22 a 27 semanas',         N'2 - DE 22 A 27 SEMANAS',     22,  27, N'Prematuro muy',      1, 0,  2),
    (3, N'SG03', N'3', N'De 28 a 36 semanas',         N'3 - DE 28 A 36 SEMANAS',     28,  36, N'Prematuro',          1, 0,  3),
    (4, N'SG04', N'4', N'De 37 a 41 semanas',         N'4 - DE 37 A 41 SEMANAS',     37,  41, N'A termino',          0, 1,  4),
    (5, N'SG05', N'5', N'De 42 o mas semanas',        N'5 - DE 42 O MAS SEMANAS',    42,  NULL, N'Postermino',       0, 0,  5),
    (6, N'SG98', N'NA', N'No aplica',                  N'NO APLICA',                  NULL, NULL, N'No aplica',       0, 0, 98),
    (7, N'SG99', N'SIN', N'Sin informacion',           N'SIN INFORMACION',            NULL, NULL, N'Sin informacion',   0, 0, 99);
    SET IDENTITY_INSERT dbo.dim_semanas_gestacion OFF;
    PRINT N'dim_semanas_gestacion: 7 categorias insertadas.';
END
ELSE
    PRINT N'dim_semanas_gestacion: ya tiene datos.';

/* --- map_peso_al_nacer_fuente --- */
IF OBJECT_ID(N'dbo.map_peso_al_nacer_fuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.map_peso_al_nacer_fuente (
        id_mapeo           int           IDENTITY(1, 1) NOT NULL,
        fuente_tabla       varchar(150)  NOT NULL,
        columna_origen     varchar(150)  NOT NULL,
        valor_origen       nvarchar(300) NOT NULL,
        id_peso_al_nacer   int           NOT NULL,
        vigente            bit           NOT NULL CONSTRAINT DF_map_peso_vigente DEFAULT (1),
        fecha_creacion     datetime2     NOT NULL CONSTRAINT DF_map_peso_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_map_peso_al_nacer_fuente PRIMARY KEY CLUSTERED (id_mapeo),
        CONSTRAINT FK_map_peso_dim FOREIGN KEY (id_peso_al_nacer) REFERENCES dbo.dim_peso_al_nacer (id_peso_al_nacer)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_map_peso_fuente_valor
        ON dbo.map_peso_al_nacer_fuente (fuente_tabla, columna_origen, valor_origen);
END
ELSE
    DELETE FROM dbo.map_peso_al_nacer_fuente;

DECLARE @P1 int = (SELECT id_peso_al_nacer FROM dbo.dim_peso_al_nacer WHERE codigo = N'P01');
DECLARE @P2 int = (SELECT id_peso_al_nacer FROM dbo.dim_peso_al_nacer WHERE codigo = N'P02');
DECLARE @P3 int = (SELECT id_peso_al_nacer FROM dbo.dim_peso_al_nacer WHERE codigo = N'P03');
DECLARE @P4 int = (SELECT id_peso_al_nacer FROM dbo.dim_peso_al_nacer WHERE codigo = N'P04');
DECLARE @P5 int = (SELECT id_peso_al_nacer FROM dbo.dim_peso_al_nacer WHERE codigo = N'P05');
DECLARE @P99 int = (SELECT id_peso_al_nacer FROM dbo.dim_peso_al_nacer WHERE codigo = N'P99');

INSERT dbo.map_peso_al_nacer_fuente (fuente_tabla, columna_origen, valor_origen, id_peso_al_nacer) VALUES
(N'nacimientos_casanare', N'peso_al_nacer', N'1 - MENOR A 1000 GRAMOS', @P1),
(N'nacimientos_casanare', N'peso_al_nacer', N'2 - ENTRE 1000 Y 1499 GRAMOS', @P2),
(N'nacimientos_casanare', N'peso_al_nacer', N'3 - ENTRE 1500 Y 2499 GRAMOS', @P3),
(N'nacimientos_casanare', N'peso_al_nacer', N'4 - ENTRE 2500 Y 3999 GRAMOS', @P4),
(N'nacimientos_casanare', N'peso_al_nacer', N'5 - MAYOR O IGUAL A 4000 GRAMOS', @P5),
(N'nacimientos_casanare', N'peso_al_nacer', N'SIN INFORMACION', @P99),
(N'nacimientos_casanare', N'peso_al_nacer', N'SIN INFORMACIÓN', @P99);

PRINT N'map_peso_al_nacer_fuente: ' + CAST(@@ROWCOUNT AS nvarchar(20)) + N' filas.';

/* --- map_semanas_gestacion_fuente --- */
IF OBJECT_ID(N'dbo.map_semanas_gestacion_fuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.map_semanas_gestacion_fuente (
        id_mapeo               int           IDENTITY(1, 1) NOT NULL,
        fuente_tabla           varchar(150)  NOT NULL,
        columna_origen         varchar(150)  NOT NULL,
        valor_origen           nvarchar(300) NOT NULL,
        id_semanas_gestacion   int           NOT NULL,
        vigente                bit           NOT NULL CONSTRAINT DF_map_sem_vigente DEFAULT (1),
        fecha_creacion         datetime2     NOT NULL CONSTRAINT DF_map_sem_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_map_semanas_gestacion_fuente PRIMARY KEY CLUSTERED (id_mapeo),
        CONSTRAINT FK_map_sem_dim FOREIGN KEY (id_semanas_gestacion) REFERENCES dbo.dim_semanas_gestacion (id_semanas_gestacion)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_map_sem_fuente_valor
        ON dbo.map_semanas_gestacion_fuente (fuente_tabla, columna_origen, valor_origen);
END
ELSE
    DELETE FROM dbo.map_semanas_gestacion_fuente;

DECLARE @SG1 int = (SELECT id_semanas_gestacion FROM dbo.dim_semanas_gestacion WHERE codigo = N'SG01');
DECLARE @SG2 int = (SELECT id_semanas_gestacion FROM dbo.dim_semanas_gestacion WHERE codigo = N'SG02');
DECLARE @SG3 int = (SELECT id_semanas_gestacion FROM dbo.dim_semanas_gestacion WHERE codigo = N'SG03');
DECLARE @SG4 int = (SELECT id_semanas_gestacion FROM dbo.dim_semanas_gestacion WHERE codigo = N'SG04');
DECLARE @SG5 int = (SELECT id_semanas_gestacion FROM dbo.dim_semanas_gestacion WHERE codigo = N'SG05');
DECLARE @SG98 int = (SELECT id_semanas_gestacion FROM dbo.dim_semanas_gestacion WHERE codigo = N'SG98');
DECLARE @SG99 int = (SELECT id_semanas_gestacion FROM dbo.dim_semanas_gestacion WHERE codigo = N'SG99');

INSERT dbo.map_semanas_gestacion_fuente (fuente_tabla, columna_origen, valor_origen, id_semanas_gestacion) VALUES
(N'nacimientos_casanare', N'semanas_gestacion', N'1 - MENOS DE 22 SEMANAS', @SG1),
(N'nacimientos_casanare', N'semanas_gestacion', N'2 - DE 22 A 27 SEMANAS', @SG2),
(N'nacimientos_casanare', N'semanas_gestacion', N'3 - DE 28 A 36 SEMANAS', @SG3),
(N'nacimientos_casanare', N'semanas_gestacion', N'4 - DE 37 A 41 SEMANAS', @SG4),
(N'nacimientos_casanare', N'semanas_gestacion', N'5 - DE 42 O MAS SEMANAS', @SG5),
(N'nacimientos_casanare', N'semanas_gestacion', N'5 - DE 42 O MÁS SEMANAS', @SG5),
(N'nacimientos_casanare', N'semanas_gestacion', N'NO APLICA', @SG98),
(N'nacimientos_casanare', N'semanas_gestacion', N'SIN INFORMACION', @SG99),
(N'nacimientos_casanare', N'semanas_gestacion', N'SIN INFORMACIÓN', @SG99);

PRINT N'map_semanas_gestacion_fuente: ' + CAST(@@ROWCOUNT AS nvarchar(20)) + N' filas.';

COMMIT TRANSACTION;
GO

PRINT N'--- dim_peso_al_nacer ---';
SELECT codigo, codigo_dane, categoria_normalizada, es_bajo_peso, es_muy_bajo_peso, orden_visualizacion
FROM dbo.dim_peso_al_nacer ORDER BY orden_visualizacion;

PRINT N'--- dim_semanas_gestacion ---';
SELECT codigo, codigo_dane, categoria_normalizada, es_prematuro, es_termino, orden_visualizacion
FROM dbo.dim_semanas_gestacion ORDER BY orden_visualizacion;

PRINT N'--- Mapeos CSV nacimientos ---';
SELECT columna_origen, valor_origen, p.codigo AS codigo_peso
FROM dbo.map_peso_al_nacer_fuente AS m
INNER JOIN dbo.dim_peso_al_nacer AS p ON p.id_peso_al_nacer = m.id_peso_al_nacer
ORDER BY p.orden_visualizacion, valor_origen;

SELECT columna_origen, valor_origen, s.codigo AS codigo_semanas
FROM dbo.map_semanas_gestacion_fuente AS m
INNER JOIN dbo.dim_semanas_gestacion AS s ON s.id_semanas_gestacion = m.id_semanas_gestacion
ORDER BY s.orden_visualizacion, valor_origen;
GO

PRINT N'=== FIN 19_catalogo_nacimientos_peso_semanas ===';
GO
