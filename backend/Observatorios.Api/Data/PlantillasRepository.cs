using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Data;

public sealed class PlantillasRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    public async Task<IReadOnlyList<PlantillaRow>> ListarAsync(CancellationToken ct = default)
    {
        const string sql = """
SELECT p.Id, p.Codigo, p.Nombre, p.Descripcion, p.DependenciaId, d.Nombre, p.Activo, p.CreadoEn,
       (SELECT COUNT(1) FROM dbo.CamposPlantilla c WHERE c.PlantillaId = p.Id) AS TotalCampos
FROM dbo.PlantillasCarga p
LEFT JOIN dbo.Dependencias d ON d.Id = p.DependenciaId
ORDER BY p.Nombre;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
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
        const string sql = """
INSERT INTO dbo.PlantillasCarga (Codigo, Nombre, Descripcion, DependenciaId, Activo)
OUTPUT INSERTED.Id VALUES (@Codigo, @Nombre, @Desc, @DepId, @Activo);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Codigo", req.Codigo.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Nombre", req.Nombre);
        cmd.Parameters.AddWithValue("@Desc", (object?)req.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DepId", (object?)req.DependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Activo", req.Activo);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task ActualizarAsync(int id, PlantillaUpsert req, CancellationToken ct = default)
    {
        const string sql = """
UPDATE dbo.PlantillasCarga SET Codigo=@Codigo, Nombre=@Nombre, Descripcion=@Desc,
    DependenciaId=@DepId, Activo=@Activo WHERE Id=@Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Codigo", req.Codigo.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("@Nombre", req.Nombre);
        cmd.Parameters.AddWithValue("@Desc", (object?)req.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DepId", (object?)req.DependenciaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Activo", req.Activo);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<CampoPlantillaRow>> ListarCamposAsync(int plantillaId, CancellationToken ct = default)
    {
        const string sql = """
SELECT Id, PlantillaId, NombreCampo, TipoDato, Obligatorio, Descripcion, Longitud, Formato, ValoresPermitidos, Orden
FROM dbo.CamposPlantilla WHERE PlantillaId = @Pid ORDER BY Orden, Id;
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Pid", plantillaId);
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
        const string sql = """
INSERT INTO dbo.CamposPlantilla (PlantillaId, NombreCampo, TipoDato, Obligatorio, Descripcion, Longitud, Formato, ValoresPermitidos, Orden)
OUTPUT INSERTED.Id VALUES (@Pid, @Nombre, @Tipo, @Obl, @Desc, @Len, @Fmt, @Val, @Ord);
""";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Pid", plantillaId);
        cmd.Parameters.AddWithValue("@Nombre", req.NombreCampo);
        cmd.Parameters.AddWithValue("@Tipo", req.TipoDato);
        cmd.Parameters.AddWithValue("@Obl", req.Obligatorio);
        cmd.Parameters.AddWithValue("@Desc", (object?)req.Descripcion ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Len", (object?)req.Longitud ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Fmt", (object?)req.Formato ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Val", (object?)req.ValoresPermitidos ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Ord", req.Orden);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task EliminarCampoAsync(int campoId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM dbo.CamposPlantilla WHERE Id = @Id;";
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@Id", campoId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed record PlantillaRow(int Id, string Codigo, string Nombre, string? Descripcion,
    int? DependenciaId, string? DependenciaNombre, bool Activo, DateTime CreadoEn, int TotalCampos);

public sealed record PlantillaUpsert(string Codigo, string Nombre, string? Descripcion, int? DependenciaId, bool Activo);

public sealed record CampoPlantillaRow(int Id, int PlantillaId, string NombreCampo, string TipoDato, bool Obligatorio,
    string? Descripcion, int? Longitud, string? Formato, string? ValoresPermitidos, int Orden);

public sealed record CampoPlantillaUpsert(string NombreCampo, string TipoDato, bool Obligatorio,
    string? Descripcion, int? Longitud, string? Formato, string? ValoresPermitidos, int Orden);
