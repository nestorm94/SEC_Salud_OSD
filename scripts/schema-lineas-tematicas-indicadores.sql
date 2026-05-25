-- Líneas temáticas, indicadores y vínculo con archivos cargados
-- Observatorio de Salud Departamental Casanare

IF OBJECT_ID(N'dbo.LineaTematica', N'U') IS NULL
CREATE TABLE dbo.LineaTematica (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Codigo NVARCHAR(50) NOT NULL UNIQUE,
    Nombre NVARCHAR(300) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Activo BIT NOT NULL CONSTRAINT DF_LineaTematica_Activo DEFAULT (1),
    CreadoEn DATETIME2(0) NOT NULL CONSTRAINT DF_LineaTematica_CreadoEn DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'dbo.Indicador', N'U') IS NULL
CREATE TABLE dbo.Indicador (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    LineaTematicaId INT NOT NULL REFERENCES dbo.LineaTematica(Id),
    Codigo NVARCHAR(80) NOT NULL,
    Nombre NVARCHAR(300) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Activo BIT NOT NULL CONSTRAINT DF_Indicador_Activo DEFAULT (1),
    CreadoEn DATETIME2(0) NOT NULL CONSTRAINT DF_Indicador_CreadoEn DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Indicador_Linea_Codigo UNIQUE (LineaTematicaId, Codigo)
);

IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Archivos', N'LineaTematicaId') IS NULL
        ALTER TABLE dbo.Archivos ADD LineaTematicaId INT NULL REFERENCES dbo.LineaTematica(Id);
    IF COL_LENGTH(N'dbo.Archivos', N'IndicadorId') IS NULL
        ALTER TABLE dbo.Archivos ADD IndicadorId INT NULL REFERENCES dbo.Indicador(Id);
    IF COL_LENGTH(N'dbo.Archivos', N'Observaciones') IS NULL
        ALTER TABLE dbo.Archivos ADD Observaciones NVARCHAR(1000) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Archivos_Linea_Indicador' AND object_id = OBJECT_ID(N'dbo.Archivos'))
    CREATE INDEX IX_Archivos_Linea_Indicador ON dbo.Archivos(LineaTematicaId, IndicadorId, CreadoEn DESC);
