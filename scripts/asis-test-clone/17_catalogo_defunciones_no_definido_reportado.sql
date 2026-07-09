/*
================================================================================
 17_catalogo_defunciones_no_definido_reportado.sql
================================================================================
 PROPÓSITO:
   Añade categorías "No definido" / "No reportado" en dim_curso_vida y
   dim_grupo_edad, y crea map_defunciones_edad_fuente para homologar valores
   de la fuente [Defunciones Casanare] antes de cargar al fact.

 BASE DE DATOS DESTINO:
   ObservatorioDB u ObservatorioDB_ASIS_Test.

 DEPENDENCIAS (ejecutar antes):
   - 04_normalizacion_catalogos_geograficos.sql (dims geográficas y área)
   - Tablas dim_curso_vida, dim_grupo_edad existentes

 ORDEN DE EJECUCIÓN:
   17 (este script) -> 18_carga_defunciones_no_homologadas -> 25_vistas_asis_mortalidad...

 EJECUCIÓN:
   sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\17_catalogo_defunciones_no_definido_reportado.sql
================================================================================
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

PRINT N'=== 17 - Catalogo No Definido / No Reportado (defunciones) ===';
GO

BEGIN TRANSACTION;

/* --- INSERT dim_curso_vida: CV07 No definido, CV08 No reportado --- */
IF NOT EXISTS (SELECT 1 FROM dbo.dim_curso_vida WHERE codigo = N'CV07')
BEGIN
    SET IDENTITY_INSERT dbo.dim_curso_vida ON;
    INSERT dbo.dim_curso_vida (id_curso_vida, codigo, nombre_curso_vida, descripcion, edad_minima, edad_maxima, orden_visualizacion, estado, fecha_creacion)
    VALUES (7, N'CV07', N'No definido', N'Curso de vida no definido en fuente DANE', -1, -1, 97, 1, SYSDATETIME());
    SET IDENTITY_INSERT dbo.dim_curso_vida OFF;
    PRINT N'dim_curso_vida: CV07 No definido insertado.';
END
ELSE
    PRINT N'dim_curso_vida: CV07 ya existe.';

IF NOT EXISTS (SELECT 1 FROM dbo.dim_curso_vida WHERE codigo = N'CV08')
BEGIN
    SET IDENTITY_INSERT dbo.dim_curso_vida ON;
    INSERT dbo.dim_curso_vida (id_curso_vida, codigo, nombre_curso_vida, descripcion, edad_minima, edad_maxima, orden_visualizacion, estado, fecha_creacion)
    VALUES (8, N'CV08', N'No reportado', N'Curso de vida no reportado en fuente DANE', -1, -1, 98, 1, SYSDATETIME());
    SET IDENTITY_INSERT dbo.dim_curso_vida OFF;
    PRINT N'dim_curso_vida: CV08 No reportado insertado.';
END
ELSE
    PRINT N'dim_curso_vida: CV08 ya existe.';

DECLARE @idCvNoDef int = (SELECT id_curso_vida FROM dbo.dim_curso_vida WHERE codigo = N'CV07');
DECLARE @idCvNoRep int = (SELECT id_curso_vida FROM dbo.dim_curso_vida WHERE codigo = N'CV08');

/* --- INSERT dim_grupo_edad: GE09/GE10 vinculados a curso de vida padre --- */
IF NOT EXISTS (SELECT 1 FROM dbo.dim_grupo_edad WHERE codigo = N'GE09')
BEGIN
    SET IDENTITY_INSERT dbo.dim_grupo_edad ON;
    INSERT dbo.dim_grupo_edad (id_grupo_edad, id_curso_vida, codigo, nombre_grupo_edad, etiqueta_rango, edad_minima, edad_maxima, orden_visualizacion, estado, fecha_creacion)
    VALUES (9, @idCvNoDef, N'GE09', N'No definido', N'No definido', -1, -1, 97, 1, SYSDATETIME());
    SET IDENTITY_INSERT dbo.dim_grupo_edad OFF;
    PRINT N'dim_grupo_edad: GE09 No definido insertado.';
END
ELSE
    PRINT N'dim_grupo_edad: GE09 ya existe.';

IF NOT EXISTS (SELECT 1 FROM dbo.dim_grupo_edad WHERE codigo = N'GE10')
BEGIN
    SET IDENTITY_INSERT dbo.dim_grupo_edad ON;
    INSERT dbo.dim_grupo_edad (id_grupo_edad, id_curso_vida, codigo, nombre_grupo_edad, etiqueta_rango, edad_minima, edad_maxima, orden_visualizacion, estado, fecha_creacion)
    VALUES (10, @idCvNoRep, N'GE10', N'No reportado', N'No reportado', -1, -1, 98, 1, SYSDATETIME());
    SET IDENTITY_INSERT dbo.dim_grupo_edad OFF;
    PRINT N'dim_grupo_edad: GE10 No reportado insertado.';
END
ELSE
    PRINT N'dim_grupo_edad: GE10 ya existe.';

/* --- CREATE TABLE map_defunciones_edad_fuente: FK a dim_grupo_edad y dim_curso_vida --- */
IF OBJECT_ID(N'dbo.map_defunciones_edad_fuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.map_defunciones_edad_fuente (
        id_mapeo           int           IDENTITY(1, 1) NOT NULL,
        columna_origen     varchar(80)   NOT NULL,
        valor_origen       nvarchar(300) NOT NULL,
        id_grupo_edad      int           NOT NULL,
        id_curso_vida      int           NOT NULL,
        vigente            bit           NOT NULL CONSTRAINT DF_map_def_edad_vigente DEFAULT (1),
        fecha_creacion     datetime2     NOT NULL CONSTRAINT DF_map_def_edad_fecha DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_map_defunciones_edad_fuente PRIMARY KEY CLUSTERED (id_mapeo),
        CONSTRAINT FK_map_def_edad_grupo FOREIGN KEY (id_grupo_edad) REFERENCES dbo.dim_grupo_edad (id_grupo_edad),
        CONSTRAINT FK_map_def_edad_curso FOREIGN KEY (id_curso_vida) REFERENCES dbo.dim_curso_vida (id_curso_vida)
    );
    CREATE UNIQUE NONCLUSTERED INDEX UQ_map_def_edad_valor
        ON dbo.map_defunciones_edad_fuente (columna_origen, valor_origen);
    PRINT N'Tabla map_defunciones_edad_fuente creada.';
END

DECLARE @idGeNoDef int = (SELECT id_grupo_edad FROM dbo.dim_grupo_edad WHERE codigo = N'GE09');
DECLARE @idGeNoRep int = (SELECT id_grupo_edad FROM dbo.dim_grupo_edad WHERE codigo = N'GE10');

/* --- MERGE: equivalencias texto fuente → id_grupo_edad / id_curso_vida --- */
MERGE dbo.map_defunciones_edad_fuente AS t
USING (VALUES
    (N'Grupo_Etareo_Quinquenios_DANE', N'No Definido', @idGeNoDef, @idCvNoDef),
    (N'Grupo_Etareo_Quinquenios_DANE', N'No Reportado', @idGeNoRep, @idCvNoRep),
    (N'Grupo_Etareo_Curso_Vida', N'No Definido', @idGeNoDef, @idCvNoDef),
    (N'Grupo_Etareo_Curso_Vida', N'No Reportado', @idGeNoRep, @idCvNoRep)
) AS s (columna_origen, valor_origen, id_grupo_edad, id_curso_vida)
ON t.columna_origen = s.columna_origen AND t.valor_origen = s.valor_origen
WHEN NOT MATCHED BY TARGET THEN
    INSERT (columna_origen, valor_origen, id_grupo_edad, id_curso_vida)
    VALUES (s.columna_origen, s.valor_origen, s.id_grupo_edad, s.id_curso_vida);

PRINT N'Mapeos edad insertados/confirmados: ' + CAST(@@ROWCOUNT AS nvarchar(20));

COMMIT TRANSACTION;
GO

PRINT N'--- Resumen catálogos ---';
SELECT id_curso_vida, codigo, nombre_curso_vida FROM dbo.dim_curso_vida WHERE codigo IN (N'CV07', N'CV08');
SELECT id_grupo_edad, id_curso_vida, codigo, nombre_grupo_edad FROM dbo.dim_grupo_edad WHERE codigo IN (N'GE09', N'GE10');
SELECT * FROM dbo.map_defunciones_edad_fuente ORDER BY id_mapeo;
GO

PRINT N'=== FIN 17_catalogo_defunciones_no_definido_reportado ===';
GO
