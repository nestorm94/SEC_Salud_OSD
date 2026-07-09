using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

/// <summary>
/// Bootstrap al arranque: crea la BD si falta, aplica <c>scripts/schema-bootstrap.sql</c>
/// y seed mínimo. El usuario <c>admin</c> usa <c>usp_*</c> cuando existen (fase 2+).
/// </summary>
public sealed class ObservatorioDbSchema(
    IConfiguration config,
    DependenciasRepository dependencias,
    UsuariosRepository usuarios)
{
    private const string AdminUser = "admin";
    private const string AdminEmail = "admin@observatorio.gov.co";
    private const string AdminPassword = "Admin123*";
    private static readonly string[] AdminRoles = ["ADMIN", "Administrador"];

    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    /// <summary>
    /// Aplica scripts de esquema y seed mínimo; garantiza usuario administrador inicial.
    /// </summary>
    public async Task EnsureAllAsync(CancellationToken ct = default)
    {
        if (config.GetValue("Observatorio:SkipSchemaBootstrap", false))
            return;

        var b = new SqlConnectionStringBuilder(_cs);
        var dbName = b.InitialCatalog
            ?? throw new InvalidOperationException("La cadena de conexión debe incluir Initial Catalog.");
        await EnsureDatabaseExistsAsync(b, dbName, ct);

        var bootstrap = ResolveScriptPath("schema-bootstrap.sql");
        var seedMinimo = ResolveScriptPath("schema-seed-minimo.sql");

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        await SqlScriptRunner.ExecuteFileAsync(con, bootstrap, ct: ct);
        await SqlScriptRunner.ExecuteFileAsync(con, seedMinimo, ct: ct);
        await SeedAdminAsync(ct);
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        if (await StoredProcedureExistsAsync("dbo.usp_Usuario_Crear", ct))
            await SeedAdminViaSpAsync(ct);
        else
            await SeedAdminViaSqlAsync(ct);
    }

    private async Task SeedAdminViaSpAsync(CancellationToken ct)
    {
        var depId = await dependencias.ObtenerOCrearPorCodigoAsync(
            "CAS-SALUD", "Secretaría de Salud — Casanare", ct);

        var admin = await usuarios.GetByNombreUsuarioAsync(AdminUser, ct);
        if (admin is null)
        {
            await usuarios.CrearAsync(new CrearUsuarioRequest(
                AdminUser, AdminPassword, AdminEmail, depId, null, AdminRoles), ct);
            return;
        }

        await usuarios.ActualizarAsync(admin.Id, new ActualizarUsuarioRequest(
            AdminEmail, depId, admin.LineaTematicaId, AdminPassword), ct);
        await usuarios.SetActivoAsync(admin.Id, true, ct);
        await usuarios.ActualizarRolesAsync(admin.Id, AdminRoles, ct);
    }

    private async Task SeedAdminViaSqlAsync(CancellationToken ct)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(AdminPassword);
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        const string sql = """
DECLARE @depId INT = (SELECT TOP 1 Id FROM dbo.Dependencias WHERE Codigo = N'CAS-SALUD');
IF @depId IS NULL SET @depId = (SELECT TOP 1 Id FROM dbo.Dependencias ORDER BY Id);
DECLARE @uid INT = (SELECT TOP 1 Id FROM dbo.Usuarios WHERE NombreUsuario = @User OR Email = @Email);
IF @uid IS NULL
BEGIN
  INSERT INTO dbo.Usuarios (DependenciaId, NombreUsuario, Email, PasswordHash, Activo)
  VALUES (@depId, @User, @Email, @Hash, 1);
  SET @uid = SCOPE_IDENTITY();
END
ELSE
  UPDATE dbo.Usuarios SET Email = @Email, PasswordHash = @Hash, Activo = 1, DependenciaId = @depId WHERE Id = @uid;
INSERT INTO dbo.UsuarioRol (UsuarioId, RolId)
SELECT @uid, r.Id FROM dbo.Roles r
WHERE r.Nombre IN (N'ADMIN', N'Administrador')
  AND NOT EXISTS (SELECT 1 FROM dbo.UsuarioRol ur WHERE ur.UsuarioId = @uid AND ur.RolId = r.Id);
""";
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@User", AdminUser);
        cmd.Parameters.AddWithValue("@Email", AdminEmail);
        cmd.Parameters.AddWithValue("@Hash", hash);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<bool> StoredProcedureExistsAsync(string fullName, CancellationToken ct)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(@Name, N'P') IS NOT NULL THEN 1 ELSE 0 END", con);
        cmd.Parameters.AddWithValue("@Name", fullName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
    }

    private static string ResolveScriptPath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Sql", fileName),
            Path.Combine(AppContext.BaseDirectory, "scripts", fileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", fileName))
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException(
            $"No se encontró {fileName}. Rutas probadas: {string.Join("; ", candidates)}");
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
}
