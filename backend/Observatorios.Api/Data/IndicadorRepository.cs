using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class IndicadorRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task<IReadOnlyList<IndicadorRow>> ListarAsync(int? lineaTematicaId, bool soloActivas = true, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Indicador_Listar");
        cmd.Parameters.AddWithValue("@LineaTematicaId", (object?)lineaTematicaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SoloActivas", soloActivas);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<IndicadorRow>();
        while (await r.ReadAsync(ct))
            list.Add(LeerFila(r));
        return list;
    }

    public async Task<IndicadorRow?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Indicador_Obtener");
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerFila(r) : null;
    }

    public async Task<string?> GetColumnasObligatoriasJsonAsync(int indicadorId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Indicador_ObtenerColumnasObligatoriasJson");
        cmd.Parameters.AddWithValue("@Id", indicadorId);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is DBNull or null ? null : Convert.ToString(scalar);
    }

    public async Task<bool> PerteneceALineaAsync(int indicadorId, int lineaTematicaId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Indicador_PerteneceALinea");
        cmd.Parameters.AddWithValue("@IndicadorId", indicadorId);
        cmd.Parameters.AddWithValue("@LineaTematicaId", lineaTematicaId);
        return Convert.ToBoolean(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<int> CrearAsync(int lineaTematicaId, string codigo, string nombre, string? descripcion, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Indicador_Crear");
        cmd.Parameters.AddWithValue("@LineaTematicaId", lineaTematicaId);
        cmd.Parameters.AddWithValue("@Codigo", codigo);
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion?.Trim() ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarAsync(int id, int lineaTematicaId, string codigo, string nombre, string? descripcion, bool activo, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Indicador_Actualizar");
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@LineaTematicaId", lineaTematicaId);
        cmd.Parameters.AddWithValue("@Codigo", codigo);
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Activo", activo);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static IndicadorRow LeerFila(SqlDataReader r) =>
        new(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetString(4),
            r.IsDBNull(5) ? null : r.GetString(5), r.GetBoolean(6), r.GetDateTime(7));

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        return con;
    }

    private static SqlCommand Sp(SqlConnection con, string name) =>
        new(name, con) { CommandType = CommandType.StoredProcedure };
}

public sealed record IndicadorRow(
    int Id, int LineaTematicaId, string LineaTematicaNombre,
    string Codigo, string Nombre, string? Descripcion, bool Activo, DateTime CreadoEn);
