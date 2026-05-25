-- Observatorio Salud Departamental Casanare — Esquema v2 (idempotente)
-- Ejecutar en ObservatorioDB. Compatible con dbo.Archivo / dbo.Archivos existentes.

USE ObservatorioDB;
GO

/* ========== Catálogos geográficos (validación Excel) ========== */
IF OBJECT_ID(N'dbo.dim_departamentos', N'U') IS NULL
CREATE TABLE dbo.dim_departamentos (
    codigo_departamento NVARCHAR(10) NOT NULL PRIMARY KEY,
    nombre_departamento NVARCHAR(200) NOT NULL
);

IF OBJECT_ID(N'dbo.dim_municipios', N'U') IS NULL
CREATE TABLE dbo.dim_municipios (
    codigo_municipio NVARCHAR(10) NOT NULL PRIMARY KEY,
    nombre_municipio NVARCHAR(200) NOT NULL,
    codigo_departamento NVARCHAR(10) NOT NULL REFERENCES dbo.dim_departamentos(codigo_departamento)
);

IF NOT EXISTS (SELECT 1 FROM dbo.dim_departamentos WHERE codigo_departamento = N'85')
    INSERT INTO dbo.dim_departamentos VALUES (N'85', N'Casanare');

/* ========== Seguridad ========== */
IF OBJECT_ID(N'dbo.Dependencias', N'U') IS NULL
CREATE TABLE dbo.Dependencias (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Codigo NVARCHAR(30) NOT NULL UNIQUE,
    Nombre NVARCHAR(200) NOT NULL,
    Activo BIT NOT NULL DEFAULT (1),
    CreadoEn DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
CREATE TABLE dbo.Roles (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Nombre NVARCHAR(50) NOT NULL UNIQUE,
    Descripcion NVARCHAR(300) NULL
);

IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
CREATE TABLE dbo.Usuarios (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    DependenciaId INT NULL REFERENCES dbo.Dependencias(Id),
    NombreUsuario NVARCHAR(100) NOT NULL UNIQUE,
    Email NVARCHAR(256) NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    Activo BIT NOT NULL DEFAULT (1),
    CreadoEn DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.UsuarioRol', N'U') IS NULL
CREATE TABLE dbo.UsuarioRol (
    UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id) ON DELETE CASCADE,
    RolId INT NOT NULL REFERENCES dbo.Roles(Id) ON DELETE CASCADE,
    CONSTRAINT PK_UsuarioRol PRIMARY KEY (UsuarioId, RolId)
);

/* ========== Áreas temáticas ========== */
IF OBJECT_ID(N'dbo.AreaTematica', N'U') IS NULL
CREATE TABLE dbo.AreaTematica (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    DependenciaId INT NOT NULL REFERENCES dbo.Dependencias(Id),
    Codigo NVARCHAR(50) NOT NULL,
    Nombre NVARCHAR(300) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Activo BIT NOT NULL DEFAULT (1),
    CreadoEn DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_AreaTematica_Dep_Codigo UNIQUE (DependenciaId, Codigo)
);

IF OBJECT_ID(N'dbo.UsuarioAreaTematica', N'U') IS NULL
CREATE TABLE dbo.UsuarioAreaTematica (
    UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id) ON DELETE CASCADE,
    AreaTematicaId INT NOT NULL REFERENCES dbo.AreaTematica(Id) ON DELETE CASCADE,
    CONSTRAINT PK_UsuarioAreaTematica PRIMARY KEY (UsuarioId, AreaTematicaId)
);

IF OBJECT_ID(N'dbo.ResponsableTematico', N'U') IS NULL
CREATE TABLE dbo.ResponsableTematico (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AreaTematicaId INT NOT NULL REFERENCES dbo.AreaTematica(Id),
    UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id),
    Activo BIT NOT NULL DEFAULT (1),
    AsignadoEn DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME())
);

/* ========== Plantillas ========== */
IF OBJECT_ID(N'dbo.PlantillaCarga', N'U') IS NULL
CREATE TABLE dbo.PlantillaCarga (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AreaTematicaId INT NOT NULL REFERENCES dbo.AreaTematica(Id),
    Codigo NVARCHAR(50) NOT NULL,
    Nombre NVARCHAR(200) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Version NVARCHAR(20) NULL,
    Activo BIT NOT NULL DEFAULT (1),
    CreadoEn DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_PlantillaCarga_Area_Codigo UNIQUE (AreaTematicaId, Codigo)
);

IF OBJECT_ID(N'dbo.PlantillaCampo', N'U') IS NULL
CREATE TABLE dbo.PlantillaCampo (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PlantillaCargaId INT NOT NULL REFERENCES dbo.PlantillaCarga(Id) ON DELETE CASCADE,
    NombreCampo NVARCHAR(200) NOT NULL,
    TipoDato NVARCHAR(50) NOT NULL,
    Obligatorio BIT NOT NULL DEFAULT (0),
    Descripcion NVARCHAR(500) NULL,
    Longitud INT NULL,
    Formato NVARCHAR(100) NULL,
    ValoresPermitidos NVARCHAR(2000) NULL,
    TablaReferencia NVARCHAR(100) NULL,
    CampoReferencia NVARCHAR(100) NULL,
    Orden INT NOT NULL DEFAULT (0)
);

/* ========== Archivos (legacy + nuevo) ========== */
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Archivos (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DependenciaId INT NOT NULL REFERENCES dbo.Dependencias(Id),
        NombreOriginal NVARCHAR(260) NOT NULL,
        NombreAlmacenado NVARCHAR(260) NOT NULL,
        RutaRelativa NVARCHAR(400) NOT NULL,
        TipoMime NVARCHAR(200) NULL,
        TamanoBytes BIGINT NULL,
        SubidoPorUsuarioId INT NULL REFERENCES dbo.Usuarios(Id),
        CreadoEn DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME())
    );
    IF OBJECT_ID(N'dbo.Archivo', N'U') IS NOT NULL
        INSERT INTO dbo.Archivos (DependenciaId, NombreOriginal, NombreAlmacenado, RutaRelativa, TipoMime, TamanoBytes, CreadoEn)
        SELECT COALESCE((SELECT TOP 1 Id FROM dbo.Dependencias ORDER BY Id), 1),
               NombreOriginal, NombreAlmacenado, RutaRelativa, TipoMime, TamanoBytes, CreadoEn FROM dbo.Archivo;
END

IF OBJECT_ID(N'dbo.ArchivoCarga', N'U') IS NULL
CREATE TABLE dbo.ArchivoCarga (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ArchivoId INT NOT NULL REFERENCES dbo.Archivos(Id),
    UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id),
    DependenciaId INT NOT NULL REFERENCES dbo.Dependencias(Id),
    AreaTematicaId INT NOT NULL REFERENCES dbo.AreaTematica(Id),
    PlantillaCargaId INT NULL REFERENCES dbo.PlantillaCarga(Id),
    Estado NVARCHAR(50) NOT NULL,
    Observaciones NVARCHAR(1000) NULL,
    FechaRecepcion DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    FechaFin DATETIME2(0) NULL
);

/* Migrar CargasArchivo -> ArchivoCarga si existe tabla antigua */
IF OBJECT_ID(N'dbo.CargasArchivo', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.ArchivoCarga', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.ArchivoCarga (ArchivoId, UsuarioId, DependenciaId, AreaTematicaId, PlantillaCargaId, Estado, Observaciones, FechaRecepcion, FechaFin)
    SELECT c.ArchivoId, c.UsuarioId, c.DependenciaId,
           COALESCE((SELECT TOP 1 at.Id FROM dbo.AreaTematica at WHERE at.DependenciaId = c.DependenciaId), 1),
           NULL, c.Estado, c.Observaciones, c.FechaInicio, c.FechaFin
    FROM dbo.CargasArchivo c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ArchivoCarga ac WHERE ac.ArchivoId = c.ArchivoId AND ac.FechaRecepcion = c.FechaInicio);
END

IF OBJECT_ID(N'dbo.ValidacionArchivo', N'U') IS NULL
CREATE TABLE dbo.ValidacionArchivo (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ArchivoCargaId INT NOT NULL UNIQUE REFERENCES dbo.ArchivoCarga(Id) ON DELETE CASCADE,
    TotalFilas INT NOT NULL DEFAULT (0),
    TotalErrores INT NOT NULL DEFAULT (0),
    EsValido BIT NOT NULL DEFAULT (0),
    ValidadoEn DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.ErrorValidacion', N'U') IS NULL
CREATE TABLE dbo.ErrorValidacion (
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ArchivoCargaId INT NOT NULL REFERENCES dbo.ArchivoCarga(Id) ON DELETE CASCADE,
    NumeroFila INT NULL,
    NombreColumna NVARCHAR(200) NULL,
    Mensaje NVARCHAR(1000) NOT NULL,
    TipoError NVARCHAR(50) NULL
);

IF OBJECT_ID(N'dbo.HistorialCarga', N'U') IS NULL
CREATE TABLE dbo.HistorialCarga (
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ArchivoCargaId INT NOT NULL REFERENCES dbo.ArchivoCarga(Id) ON DELETE CASCADE,
    UsuarioId INT NULL REFERENCES dbo.Usuarios(Id),
    Accion NVARCHAR(100) NOT NULL,
    Detalle NVARCHAR(MAX) NULL,
    Fecha DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.DatosCargados', N'U') IS NULL
CREATE TABLE dbo.DatosCargados (
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ArchivoCargaId INT NOT NULL REFERENCES dbo.ArchivoCarga(Id) ON DELETE CASCADE,
    NumeroFila INT NOT NULL,
    DatosJson NVARCHAR(MAX) NOT NULL
);

IF OBJECT_ID(N'dbo.AuditoriaSistema', N'U') IS NULL
CREATE TABLE dbo.AuditoriaSistema (
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UsuarioId INT NULL REFERENCES dbo.Usuarios(Id),
    Accion NVARCHAR(100) NOT NULL,
    Entidad NVARCHAR(100) NULL,
    EntidadId NVARCHAR(50) NULL,
    Detalle NVARCHAR(MAX) NULL,
    IpOrigen NVARCHAR(50) NULL,
    Fecha DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME())
);

/* ========== Roles iniciales ========== */
MERGE dbo.Roles AS t
USING (VALUES
 (N'ADMIN', N'Acceso total al observatorio'),
 (N'COORDINADOR_DEPENDENCIA', N'Gestión de su dependencia'),
 (N'RESPONSABLE_TEMATICO', N'Carga en áreas asignadas'),
 (N'VALIDADOR', N'Revisión y aprobación de cargues'),
 (N'CONSULTA', N'Solo lectura'),
 (N'AUDITOR', N'Trazabilidad y auditoría')
) AS s(Nombre, Descripcion) ON t.Nombre = s.Nombre
WHEN NOT MATCHED THEN INSERT (Nombre, Descripcion) VALUES (s.Nombre, s.Descripcion);

/* Compatibilidad nombres antiguos */
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Nombre = N'Administrador')
    INSERT INTO dbo.Roles (Nombre, Descripcion) VALUES (N'Administrador', N'Alias de ADMIN');

PRINT 'Esquema v2 aplicado. Importe áreas desde Excel con la API o coloque el archivo en data/Areas tematicas OSC V.2.xlsx';
GO
