using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class LineaTematicaRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task<IReadOnlyList<LineaTematicaRow>> ListarAsync(bool soloActivas = true, CancellationToken ct = default)
    {
        var sql = """
SELECT Id, Codigo, Nombre, Descripcion, Activo, CreadoEn
FROM dbo.LineaTematica
""";
        if (soloActivas) sql += " WHERE Activo = 1";
        sql += " ORDER BY Nombre;";

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<LineaTematicaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new LineaTematicaRow(
                r.GetInt32(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.GetBoolean(4), r.GetDateTime(5)));
        }
        return list;
    }

    public async Task<LineaTematicaRow?> GetAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
SELECT Id, Codigo, Nombre, Descripcion, Activo, CreadoEn
FROM dbo.LineaTematica WHERE Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new LineaTematicaRow(
            r.GetInt32(0), r.GetString(1), r.GetString(2),
            r.IsDBNull(3) ? null : r.GetString(3),
            r.GetBoolean(4), r.GetDateTime(5));
    }

    public async Task<int> CrearAsync(string codigo, string nombre, string? descripcion, CancellationToken ct = default)
    {
        const string sql = """
INSERT INTO dbo.LineaTematica (Codigo, Nombre, Descripcion)
OUTPUT INSERTED.Id
VALUES (@Codigo, @Nombre, @Descripcion);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());
        cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion?.Trim() ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarAsync(int id, string codigo, string nombre, string? descripcion, bool activo, CancellationToken ct = default)
    {
        const string sql = """
UPDATE dbo.LineaTematica
SET Codigo = @Codigo, Nombre = @Nombre, Descripcion = @Descripcion, Activo = @Activo
WHERE Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());
        cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Activo", activo);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> ContarIndicadoresAsync(int lineaTematicaId, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(1) FROM dbo.Indicador WHERE LineaTematicaId = @Id;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", lineaTematicaId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}

public sealed record LineaTematicaRow(
    int Id, string Codigo, string Nombre, string? Descripcion, bool Activo, DateTime CreadoEn);
