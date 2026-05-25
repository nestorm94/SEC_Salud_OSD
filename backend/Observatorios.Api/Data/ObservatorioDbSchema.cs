using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

/// <summary>
/// Crea y migra el esquema de seguridad, archivos y cargas Excel del observatorio.
/// </summary>
public sealed class ObservatorioDbSchema(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task EnsureAllAsync(CancellationToken ct = default)
    {
        var b = new SqlConnectionStringBuilder(_cs);
        var dbName = b.InitialCatalog
            ?? throw new InvalidOperationException("La cadena de conexión debe incluir Initial Catalog.");
        await EnsureDatabaseExistsAsync(b, dbName, ct);

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        foreach (var batch in SchemaBatches)
        {
            await using var cmd = new SqlCommand(batch, con) { CommandTimeout = 120 };
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (SqlException ex) when (ex.Number is 2714 or 1913 or 2705)
            {
                // Objeto ya existe (reinicio o migración parcial): continuar
            }
        }

        await SeedRolesAndAdminAsync(con, ct);
    }

    private static async Task EnsureDatabaseExistsAsync(SqlConnectionStringBuilder b, string dbName, CancellationToken ct)
    {
        var master = new SqlConnectionStringBuilder(b.ConnectionString) { InitialCatalog = "master" };
        const string sql = """
IF DB_ID(@DbName) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE [' + REPLACE(@DbName, ']', ']]') + N']';
    EXEC(@sql);
END
""";
        await using var con = new SqlConnection(master.ConnectionString);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@DbName", dbName);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task SeedRolesAndAdminAsync(SqlConnection con, CancellationToken ct)
    {
        const string rolesSql = """
MERGE dbo.Roles AS t USING (VALUES
 (N'ADMIN', N'Acceso total al observatorio'),
 (N'COORDINADOR_DEPENDENCIA', N'Gestión de su dependencia'),
 (N'RESPONSABLE_TEMATICO', N'Carga en áreas asignadas'),
 (N'VALIDADOR', N'Revisión de cargues'),
 (N'CONSULTA', N'Solo lectura'),
 (N'AUDITOR', N'Trazabilidad y auditoría'),
 (N'Administrador', N'Alias ADMIN'),
 (N'Operador', N'Alias operador legacy')
) AS s(Nombre, Descripcion) ON t.Nombre = s.Nombre
WHEN NOT MATCHED THEN INSERT (Nombre, Descripcion) VALUES (s.Nombre, s.Descripcion);
IF NOT EXISTS (SELECT 1 FROM dbo.Dependencias WHERE Codigo = N'CAS-SALUD')
    INSERT INTO dbo.Dependencias (Codigo, Nombre) VALUES (N'CAS-SALUD', N'Secretaría de Salud — Casanare');
""";
        await using (var cmd = new SqlCommand(rolesSql, con))
            await cmd.ExecuteNonQueryAsync(ct);

        var hash = BCrypt.Net.BCrypt.HashPassword("Admin123*");
        const string upsertAdmin = """
DECLARE @depId INT = (SELECT TOP 1 Id FROM dbo.Dependencias ORDER BY Id);
DECLARE @uid INT = (SELECT TOP 1 Id FROM dbo.Usuarios WHERE NombreUsuario = N'admin' OR Email = N'admin@observatorio.gov.co');
IF @uid IS NULL
BEGIN
  INSERT INTO dbo.Usuarios (DependenciaId, NombreUsuario, Email, PasswordHash, Activo)
  VALUES (@depId, N'admin', N'admin@observatorio.gov.co', @Hash, 1);
  SET @uid = SCOPE_IDENTITY();
END
ELSE
BEGIN
  UPDATE dbo.Usuarios SET Email = N'admin@observatorio.gov.co', PasswordHash = @Hash, Activo = 1 WHERE Id = @uid;
END
INSERT INTO dbo.UsuarioRol (UsuarioId, RolId)
SELECT @uid, r.Id FROM dbo.Roles r
WHERE r.Nombre IN (N'ADMIN', N'Administrador')
  AND NOT EXISTS (SELECT 1 FROM dbo.UsuarioRol ur WHERE ur.UsuarioId = @uid AND ur.RolId = r.Id);
""";
        await using var ins = new SqlCommand(upsertAdmin, con);
        ins.Parameters.AddWithValue("@Hash", hash);
        await ins.ExecuteNonQueryAsync(ct);
    }

    private static readonly string[] SchemaBatches =
    [
        """
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NULL
   AND EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Archivos_CreadoEn')
BEGIN
    DECLARE @parent INT = (SELECT TOP 1 parent_object_id FROM sys.default_constraints WHERE name = N'DF_Archivos_CreadoEn');
    IF @parent IS NOT NULL
    BEGIN
        DECLARE @drop NVARCHAR(MAX) = N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(@parent)) + N'.' + QUOTENAME(OBJECT_NAME(@parent)) + N' DROP CONSTRAINT DF_Archivos_CreadoEn';
        EXEC sp_executesql @drop;
    END
END
""",
        """
IF OBJECT_ID(N'dbo.Dependencias', N'U') IS NULL
CREATE TABLE dbo.Dependencias (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Codigo NVARCHAR(30) NOT NULL UNIQUE,
    Nombre NVARCHAR(200) NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_Dependencias_Activo DEFAULT (1),
    CreadoEn DATETIME2(0) NOT NULL CONSTRAINT DF_Dependencias_CreadoEn DEFAULT (SYSUTCDATETIME())
);
""",
        """
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
CREATE TABLE dbo.Roles (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Nombre NVARCHAR(50) NOT NULL UNIQUE,
    Descripcion NVARCHAR(300) NULL
);
""",
        """
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
CREATE TABLE dbo.Usuarios (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    DependenciaId INT NULL REFERENCES dbo.Dependencias(Id),
    NombreUsuario NVARCHAR(100) NOT NULL UNIQUE,
    Email NVARCHAR(256) NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT (1),
    CreadoEn DATETIME2(0) NOT NULL CONSTRAINT DF_Usuarios_CreadoEn DEFAULT (SYSUTCDATETIME())
);
""",
        """
IF OBJECT_ID(N'dbo.UsuarioRol', N'U') IS NULL
CREATE TABLE dbo.UsuarioRol (
    UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id) ON DELETE CASCADE,
    RolId INT NOT NULL REFERENCES dbo.Roles(Id) ON DELETE CASCADE,
    CONSTRAINT PK_UsuarioRol PRIMARY KEY (UsuarioId, RolId)
);
""",
        """
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
    BEGIN
        INSERT INTO dbo.Archivos (DependenciaId, NombreOriginal, NombreAlmacenado, RutaRelativa, TipoMime, TamanoBytes, CreadoEn)
        SELECT COALESCE((SELECT TOP 1 Id FROM dbo.Dependencias ORDER BY Id), 1),
               NombreOriginal, NombreAlmacenado, RutaRelativa, TipoMime, TamanoBytes, CreadoEn
        FROM dbo.Archivo;
    END
END
""",
        """
IF OBJECT_ID(N'dbo.CargasArchivo', N'U') IS NULL
CREATE TABLE dbo.CargasArchivo (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ArchivoId INT NOT NULL REFERENCES dbo.Archivos(Id),
    DependenciaId INT NOT NULL REFERENCES dbo.Dependencias(Id),
    UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id),
    Estado NVARCHAR(50) NOT NULL,
    Observaciones NVARCHAR(1000) NULL,
    FechaInicio DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    FechaFin DATETIME2(0) NULL
);
IF OBJECT_ID(N'dbo.CargasArchivo', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CargasArchivo_Dependencia' AND object_id = OBJECT_ID(N'dbo.CargasArchivo'))
CREATE INDEX IX_CargasArchivo_Dependencia ON dbo.CargasArchivo(DependenciaId, FechaInicio DESC);
""",
        """
IF OBJECT_ID(N'dbo.DiccionarioArchivo', N'U') IS NULL
CREATE TABLE dbo.DiccionarioArchivo (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CargaArchivoId INT NOT NULL UNIQUE REFERENCES dbo.CargasArchivo(Id) ON DELETE CASCADE,
    NombreHoja NVARCHAR(100) NOT NULL CONSTRAINT DF_DiccionarioArchivo_Hoja DEFAULT (N'Diccionario_Datos'),
    CreadoEn DATETIME2(0) NOT NULL CONSTRAINT DF_DiccionarioArchivo_CreadoEn DEFAULT (SYSUTCDATETIME())
);
""",
        """
IF OBJECT_ID(N'dbo.CamposDiccionario', N'U') IS NULL
CREATE TABLE dbo.CamposDiccionario (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    DiccionarioArchivoId INT NOT NULL REFERENCES dbo.DiccionarioArchivo(Id) ON DELETE CASCADE,
    NombreCampo NVARCHAR(200) NOT NULL,
    TipoDato NVARCHAR(50) NOT NULL,
    Obligatorio BIT NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Longitud INT NULL,
    Formato NVARCHAR(100) NULL,
    ValoresPermitidos NVARCHAR(2000) NULL,
    Orden INT NOT NULL CONSTRAINT DF_CamposDiccionario_Orden DEFAULT (0)
);
""",
        """
IF OBJECT_ID(N'dbo.DatosCargados', N'U') IS NULL
CREATE TABLE dbo.DatosCargados (
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CargaArchivoId INT NOT NULL REFERENCES dbo.CargasArchivo(Id) ON DELETE CASCADE,
    NumeroFila INT NOT NULL,
    DatosJson NVARCHAR(MAX) NOT NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DatosCargados_Carga' AND object_id = OBJECT_ID(N'dbo.DatosCargados'))
CREATE INDEX IX_DatosCargados_Carga ON dbo.DatosCargados(CargaArchivoId, NumeroFila);
""",
        """
IF OBJECT_ID(N'dbo.ErroresValidacion', N'U') IS NULL
CREATE TABLE dbo.ErroresValidacion (
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CargaArchivoId INT NOT NULL REFERENCES dbo.CargasArchivo(Id) ON DELETE CASCADE,
    NumeroFila INT NULL,
    NombreColumna NVARCHAR(200) NULL,
    Mensaje NVARCHAR(1000) NOT NULL,
    TipoError NVARCHAR(50) NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ErroresValidacion_Carga' AND object_id = OBJECT_ID(N'dbo.ErroresValidacion'))
CREATE INDEX IX_ErroresValidacion_Carga ON dbo.ErroresValidacion(CargaArchivoId);
""",
        """
IF OBJECT_ID(N'dbo.HistorialCarga', N'U') IS NULL
CREATE TABLE dbo.HistorialCarga (
    Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CargaArchivoId INT NOT NULL REFERENCES dbo.CargasArchivo(Id) ON DELETE CASCADE,
    UsuarioId INT NULL REFERENCES dbo.Usuarios(Id),
    Accion NVARCHAR(100) NOT NULL,
    Detalle NVARCHAR(MAX) NULL,
    Fecha DATETIME2(0) NOT NULL CONSTRAINT DF_HistorialCarga_Fecha DEFAULT (SYSUTCDATETIME())
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HistorialCarga_Carga' AND object_id = OBJECT_ID(N'dbo.HistorialCarga'))
CREATE INDEX IX_HistorialCarga_Carga ON dbo.HistorialCarga(CargaArchivoId, Fecha DESC);
""",
        """
IF OBJECT_ID(N'dbo.PlantillasCarga', N'U') IS NULL
CREATE TABLE dbo.PlantillasCarga (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Codigo NVARCHAR(50) NOT NULL UNIQUE,
    Nombre NVARCHAR(200) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    DependenciaId INT NULL REFERENCES dbo.Dependencias(Id),
    Activo BIT NOT NULL DEFAULT (1),
    CreadoEn DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME())
);
""",
        """
IF OBJECT_ID(N'dbo.CamposPlantilla', N'U') IS NULL
CREATE TABLE dbo.CamposPlantilla (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PlantillaId INT NOT NULL REFERENCES dbo.PlantillasCarga(Id) ON DELETE CASCADE,
    NombreCampo NVARCHAR(200) NOT NULL,
    TipoDato NVARCHAR(50) NOT NULL,
    Obligatorio BIT NOT NULL DEFAULT (0),
    Descripcion NVARCHAR(500) NULL,
    Longitud INT NULL,
    Formato NVARCHAR(100) NULL,
    ValoresPermitidos NVARCHAR(2000) NULL,
    Orden INT NOT NULL DEFAULT (0)
);
""",
        """
IF OBJECT_ID(N'dbo.dim_departamentos', N'U') IS NULL
CREATE TABLE dbo.dim_departamentos (codigo_departamento NVARCHAR(10) NOT NULL PRIMARY KEY, nombre_departamento NVARCHAR(200) NOT NULL);
IF OBJECT_ID(N'dbo.dim_municipios', N'U') IS NULL
CREATE TABLE dbo.dim_municipios (codigo_municipio NVARCHAR(10) NOT NULL PRIMARY KEY, nombre_municipio NVARCHAR(200) NOT NULL, codigo_departamento NVARCHAR(10) NOT NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.dim_departamentos WHERE codigo_departamento=N'85') INSERT INTO dbo.dim_departamentos VALUES (N'85',N'Casanare');
""",
        """
IF OBJECT_ID(N'dbo.AreaTematica', N'U') IS NULL
CREATE TABLE dbo.AreaTematica (Id INT IDENTITY(1,1) PRIMARY KEY, DependenciaId INT NOT NULL REFERENCES dbo.Dependencias(Id), Codigo NVARCHAR(50) NOT NULL, Nombre NVARCHAR(300) NOT NULL, Descripcion NVARCHAR(500) NULL, Activo BIT NOT NULL DEFAULT(1), CreadoEn DATETIME2(0) NOT NULL DEFAULT(SYSUTCDATETIME()), CONSTRAINT UQ_AreaTematica UNIQUE(DependenciaId,Codigo));
IF OBJECT_ID(N'dbo.UsuarioAreaTematica', N'U') IS NULL
CREATE TABLE dbo.UsuarioAreaTematica (UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id) ON DELETE CASCADE, AreaTematicaId INT NOT NULL REFERENCES dbo.AreaTematica(Id) ON DELETE CASCADE, CONSTRAINT PK_UsuarioAreaTematica PRIMARY KEY(UsuarioId,AreaTematicaId));
IF OBJECT_ID(N'dbo.ResponsableTematico', N'U') IS NULL
CREATE TABLE dbo.ResponsableTematico (Id INT IDENTITY(1,1) PRIMARY KEY, AreaTematicaId INT NOT NULL REFERENCES dbo.AreaTematica(Id), UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id), Activo BIT NOT NULL DEFAULT(1), AsignadoEn DATETIME2(0) NOT NULL DEFAULT(SYSUTCDATETIME()));
""",
        """
IF OBJECT_ID(N'dbo.PlantillaCarga', N'U') IS NULL
CREATE TABLE dbo.PlantillaCarga (Id INT IDENTITY(1,1) PRIMARY KEY, AreaTematicaId INT NOT NULL REFERENCES dbo.AreaTematica(Id), Codigo NVARCHAR(50) NOT NULL, Nombre NVARCHAR(200) NOT NULL, Descripcion NVARCHAR(500) NULL, Version NVARCHAR(20) NULL, Activo BIT NOT NULL DEFAULT(1), CreadoEn DATETIME2(0) NOT NULL DEFAULT(SYSUTCDATETIME()), CONSTRAINT UQ_PlantillaCarga UNIQUE(AreaTematicaId,Codigo));
IF OBJECT_ID(N'dbo.PlantillaCampo', N'U') IS NULL
CREATE TABLE dbo.PlantillaCampo (Id INT IDENTITY(1,1) PRIMARY KEY, PlantillaCargaId INT NOT NULL REFERENCES dbo.PlantillaCarga(Id) ON DELETE CASCADE, NombreCampo NVARCHAR(200) NOT NULL, TipoDato NVARCHAR(50) NOT NULL, Obligatorio BIT NOT NULL DEFAULT(0), Descripcion NVARCHAR(500) NULL, Longitud INT NULL, Formato NVARCHAR(100) NULL, ValoresPermitidos NVARCHAR(2000) NULL, TablaReferencia NVARCHAR(100) NULL, CampoReferencia NVARCHAR(100) NULL, Orden INT NOT NULL DEFAULT(0));
""",
        """
IF OBJECT_ID(N'dbo.ArchivoCarga', N'U') IS NULL
CREATE TABLE dbo.ArchivoCarga (Id INT IDENTITY(1,1) PRIMARY KEY, ArchivoId INT NOT NULL REFERENCES dbo.Archivos(Id), UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id), DependenciaId INT NOT NULL REFERENCES dbo.Dependencias(Id), AreaTematicaId INT NOT NULL REFERENCES dbo.AreaTematica(Id), PlantillaCargaId INT NULL REFERENCES dbo.PlantillaCarga(Id), Estado NVARCHAR(50) NOT NULL, Observaciones NVARCHAR(1000) NULL, FechaRecepcion DATETIME2(0) NOT NULL DEFAULT(SYSUTCDATETIME()), FechaFin DATETIME2(0) NULL);
IF OBJECT_ID(N'dbo.ValidacionArchivo', N'U') IS NULL
CREATE TABLE dbo.ValidacionArchivo (Id INT IDENTITY(1,1) PRIMARY KEY, ArchivoCargaId INT NOT NULL UNIQUE REFERENCES dbo.ArchivoCarga(Id) ON DELETE CASCADE, TotalFilas INT NOT NULL DEFAULT(0), TotalErrores INT NOT NULL DEFAULT(0), EsValido BIT NOT NULL DEFAULT(0), ValidadoEn DATETIME2(0) NOT NULL DEFAULT(SYSUTCDATETIME()));
IF OBJECT_ID(N'dbo.AuditoriaSistema', N'U') IS NULL
CREATE TABLE dbo.AuditoriaSistema (Id BIGINT IDENTITY(1,1) PRIMARY KEY, UsuarioId INT NULL REFERENCES dbo.Usuarios(Id), Accion NVARCHAR(100) NOT NULL, Entidad NVARCHAR(100) NULL, EntidadId NVARCHAR(50) NULL, Detalle NVARCHAR(MAX) NULL, IpOrigen NVARCHAR(50) NULL, Fecha DATETIME2(0) NOT NULL DEFAULT(SYSUTCDATETIME()));
IF NOT EXISTS (SELECT 1 FROM dbo.AreaTematica)
BEGIN
  DECLARE @d INT = (SELECT TOP 1 Id FROM dbo.Dependencias ORDER BY Id);
  IF @d IS NOT NULL INSERT INTO dbo.AreaTematica (DependenciaId,Codigo,Nombre) VALUES (@d,N'OSC-GENERAL',N'Área temática general (importe Excel para detalle)');
END
""",
        """
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
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Archivos', N'LineaTematicaId') IS NULL
    ALTER TABLE dbo.Archivos ADD LineaTematicaId INT NULL REFERENCES dbo.LineaTematica(Id);
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Archivos', N'IndicadorId') IS NULL
    ALTER TABLE dbo.Archivos ADD IndicadorId INT NULL REFERENCES dbo.Indicador(Id);
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Archivos', N'Observaciones') IS NULL
    ALTER TABLE dbo.Archivos ADD Observaciones NVARCHAR(1000) NULL;
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Archivos_Linea_Indicador' AND object_id = OBJECT_ID(N'dbo.Archivos'))
    CREATE INDEX IX_Archivos_Linea_Indicador ON dbo.Archivos(LineaTematicaId, IndicadorId, CreadoEn DESC);
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Usuarios', N'LineaTematicaId') IS NULL
    ALTER TABLE dbo.Usuarios ADD LineaTematicaId INT NULL REFERENCES dbo.LineaTematica(Id);
IF OBJECT_ID(N'dbo.Indicador', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Indicador', N'ColumnasObligatoriasJson') IS NULL
    ALTER TABLE dbo.Indicador ADD ColumnasObligatoriasJson NVARCHAR(MAX) NULL;
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Archivos', N'Estado') IS NULL
    ALTER TABLE dbo.Archivos ADD Estado NVARCHAR(30) NOT NULL CONSTRAINT DF_Archivos_Estado DEFAULT (N'PendienteValidacion');
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Archivos', N'FechaValidacion') IS NULL
    ALTER TABLE dbo.Archivos ADD FechaValidacion DATETIME2(0) NULL;
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Archivos', N'FechaEnvio') IS NULL
    ALTER TABLE dbo.Archivos ADD FechaEnvio DATETIME2(0) NULL;
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Archivos', N'ErroresValidacionJson') IS NULL
    ALTER TABLE dbo.Archivos ADD ErroresValidacionJson NVARCHAR(MAX) NULL;
""",
        """
IF OBJECT_ID(N'dbo.Archivos', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Archivos', N'Estado') IS NOT NULL
    UPDATE dbo.Archivos SET Estado = N'Enviado', FechaEnvio = COALESCE(FechaEnvio, CreadoEn)
    WHERE Estado = N'PendienteValidacion' AND FechaValidacion IS NULL;
IF OBJECT_ID(N'dbo.Indicador', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Indicador', N'ColumnasObligatoriasJson') IS NOT NULL
    UPDATE dbo.Indicador
    SET ColumnasObligatoriasJson = N'["anio","periodo","valor","departamento","municipio"]'
    WHERE ColumnasObligatoriasJson IS NULL OR LTRIM(RTRIM(ColumnasObligatoriasJson)) = N'';
"""
    ];
}
