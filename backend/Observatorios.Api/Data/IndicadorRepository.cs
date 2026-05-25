using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class IndicadorRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task<IReadOnlyList<IndicadorRow>> ListarAsync(int? lineaTematicaId, bool soloActivas = true, CancellationToken ct = default)
    {
        var sql = """
SELECT i.Id, i.LineaTematicaId, lt.Nombre AS LineaTematicaNombre,
       i.Codigo, i.Nombre, i.Descripcion, i.Activo, i.CreadoEn
FROM dbo.Indicador i
INNER JOIN dbo.LineaTematica lt ON lt.Id = i.LineaTematicaId
WHERE 1=1
""";
        if (soloActivas) sql += " AND i.Activo = 1 AND lt.Activo = 1";
        if (lineaTematicaId.HasValue) sql += " AND i.LineaTematicaId = @LineaId";
        sql += " ORDER BY lt.Nombre, i.Nombre;";

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        if (lineaTematicaId.HasValue)
            cmd.Parameters.AddWithValue("@LineaId", lineaTematicaId.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<IndicadorRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new IndicadorRow(
                r.GetInt32(0), r.GetInt32(1), r.GetString(2),
                r.GetString(3), r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.GetBoolean(6), r.GetDateTime(7)));
        }
        return list;
    }

    public async Task<IndicadorRow?> GetAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
SELECT i.Id, i.LineaTematicaId, lt.Nombre, i.Codigo, i.Nombre, i.Descripcion, i.Activo, i.CreadoEn
FROM dbo.Indicador i
INNER JOIN dbo.LineaTematica lt ON lt.Id = i.LineaTematicaId
WHERE i.Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new IndicadorRow(
            r.GetInt32(0), r.GetInt32(1), r.GetString(2),
            r.GetString(3), r.GetString(4),
            r.IsDBNull(5) ? null : r.GetString(5),
            r.GetBoolean(6), r.GetDateTime(7));
    }

    public async Task<string?> GetColumnasObligatoriasJsonAsync(int indicadorId, CancellationToken ct = default)
    {
        const string sql = "SELECT ColumnasObligatoriasJson FROM dbo.Indicador WHERE Id = @Id;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", indicadorId);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is DBNull or null ? null : Convert.ToString(scalar);
    }

    public async Task<bool> PerteneceALineaAsync(int indicadorId, int lineaTematicaId, CancellationToken ct = default)
    {
        const string sql = """
SELECT 1 FROM dbo.Indicador
WHERE Id = @IndId AND LineaTematicaId = @LineaId AND Activo = 1;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@IndId", indicadorId);
        cmd.Parameters.AddWithValue("@LineaId", lineaTematicaId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<int> CrearAsync(int lineaTematicaId, string codigo, string nombre, string? descripcion, CancellationToken ct = default)
    {
        const string sql = """
INSERT INTO dbo.Indicador (LineaTematicaId, Codigo, Nombre, Descripcion)
OUTPUT INSERTED.Id
VALUES (@LineaId, @Codigo, @Nombre, @Descripcion);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@LineaId", lineaTematicaId);
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());
        cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion?.Trim() ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarAsync(int id, int lineaTematicaId, string codigo, string nombre, string? descripcion, bool activo, CancellationToken ct = default)
    {
        const string sql = """
UPDATE dbo.Indicador
SET LineaTematicaId = @LineaId, Codigo = @Codigo, Nombre = @Nombre,
    Descripcion = @Descripcion, Activo = @Activo
WHERE Id = @Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@LineaId", lineaTematicaId);
        cmd.Parameters.AddWithValue("@Codigo", codigo.Trim());
        cmd.Parameters.AddWithValue("@Nombre", nombre.Trim());
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Activo", activo);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed record IndicadorRow(
    int Id, int LineaTematicaId, string LineaTematicaNombre,
    string Codigo, string Nombre, string? Descripcion, bool Activo, DateTime CreadoEn);
