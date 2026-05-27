using System.Data;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class PlantillasRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<IReadOnlyList<PlantillaRow>> ListarAsync(CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Plantilla_Listar");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<PlantillaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new PlantillaRow(
                r.GetInt32(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetInt32(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.GetBoolean(6), r.GetDateTime(7), r.GetInt32(8)));
        }
        return list;
    }

    public async Task<int> CrearAsync(PlantillaUpsert req, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Plantilla_Crear");
        cmd.Parameters.AddWithValue("@Codigo", req.Codigo);
        cmd.Parameters.AddWithValue("@Nombre", req.Nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)req.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)req.DependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Activo", req.Activo);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarAsync(int id, PlantillaUpsert req, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Plantilla_Actualizar");
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Codigo", req.Codigo);
        cmd.Parameters.AddWithValue("@Nombre", req.Nombre);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)req.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DependenciaId", (object?)req.DependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Activo", req.Activo);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<CampoPlantillaRow>> ListarCamposAsync(int plantillaId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Plantilla_Campos_Listar");
        cmd.Parameters.AddWithValue("@PlantillaId", plantillaId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<CampoPlantillaRow>();
        while (await r.ReadAsync(ct))
        {
            list.Add(new CampoPlantillaRow(
                r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetBoolean(4),
                r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetInt32(6),
                r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetInt32(9)));
        }
        return list;
    }

    public async Task<int> CrearCampoAsync(int plantillaId, CampoPlantillaUpsert req, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Plantilla_Campo_Crear");
        cmd.Parameters.AddWithValue("@PlantillaId", plantillaId);
        cmd.Parameters.AddWithValue("@NombreCampo", req.NombreCampo);
        cmd.Parameters.AddWithValue("@TipoDato", req.TipoDato);
        cmd.Parameters.AddWithValue("@Obligatorio", req.Obligatorio);
        cmd.Parameters.AddWithValue("@Descripcion", (object?)req.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Longitud", (object?)req.Longitud ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Formato", (object?)req.Formato ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ValoresPermitidos", (object?)req.ValoresPermitidos ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Orden", req.Orden);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task EliminarCampoAsync(int campoId, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Plantilla_Campo_Eliminar");
        cmd.Parameters.AddWithValue("@Id", campoId);
        await cmd.ExecuteNonQueryAsync(ct);
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

public sealed record PlantillaRow(int Id, string Codigo, string Nombre, string? Descripcion,
    int? DependenciaId, string? DependenciaNombre, bool Activo, DateTime CreadoEn, int TotalCampos);

public sealed record PlantillaUpsert(string Codigo, string Nombre, string? Descripcion, int? DependenciaId, bool Activo);

public sealed record CampoPlantillaRow(int Id, int PlantillaId, string NombreCampo, string TipoDato, bool Obligatorio,
    string? Descripcion, int? Longitud, string? Formato, string? ValoresPermitidos, int Orden);

public sealed record CampoPlantillaUpsert(string NombreCampo, string TipoDato, bool Obligatorio,
    string? Descripcion, int? Longitud, string? Formato, string? ValoresPermitidos, int Orden);
