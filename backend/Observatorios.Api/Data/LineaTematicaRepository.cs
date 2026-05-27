using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class LineaTematicaRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    public async Task<IReadOnlyList<LineaTematicaRow>> ListarAsync(bool soloActivas = true, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_LineaTematica_Listar");
        cmd.Parameters.AddWithValue("@SoloActivas", soloActivas);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerListaAsync(r, ct);
    }

    public async Task<LineaTematicaRow?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_LineaTematica_Obtener");
        cmd.Parameters.AddWithValue("@Id", id);
        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await r.ReadAsync(ct) ? LeerFila(r) : null;
    }

    public async Task<int> CrearAsync(string codigo, string nombre, string? descripcion, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_LineaTematica_Crear");
        cmd.Parameters.AddWithValue("@Codigo", codigo);
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion?.Trim() ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarAsync(int id, string codigo, string nombre, string? descripcion, bool activo, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_LineaTematica_Actualizar");
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Codigo", codigo);
        cmd.Parameters.AddWithValue("@Nombre", nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)descripcion?.Trim() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Activo", activo);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> ContarIndicadoresAsync(int lineaTematicaId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_LineaTematica_ContarIndicadores");
        cmd.Parameters.AddWithValue("@Id", lineaTematicaId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static LineaTematicaRow LeerFila(SqlDataReader r) =>
        new(r.GetInt32(0), r.GetString(1), r.GetString(2),
            r.IsDBNull(3) ? null : r.GetString(3), r.GetBoolean(4), r.GetDateTime(5));

    private static async Task<List<LineaTematicaRow>> LeerListaAsync(SqlDataReader r, CancellationToken ct)
    {
        var list = new List<LineaTematicaRow>();
        while (await r.ReadAsync(ct))
            list.Add(LeerFila(r));
        return list;
    }

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        return con;
    }

    private static SqlCommand Sp(SqlConnection con, string name) =>
        new(name, con) { CommandType = CommandType.StoredProcedure };
}

public sealed record LineaTematicaRow(
    int Id, string Codigo, string Nombre, string? Descripcion, bool Activo, DateTime CreadoEn);
