using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;

namespace Observatorios.Api.Data;

/// <summary>
/// Catálogos DANE para filtros de proyección de población (departamentos, municipios,
/// regionales, áreas, sexos y años) vía usp_Catalogo_*.
/// </summary>
public sealed class PoblacionCatalogosRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    /// <summary>Obtiene todos los catálogos de proyección en una sola llamada paralela.</summary>
    public async Task<CatalogosProyeccionDto> ObtenerCatalogosProyeccionAsync(CancellationToken ct = default)
    {
        var deptTask = ObtenerDepartamentosAsync(ct);
        var regTask = ObtenerRegionalesAsync(ct);
        var areasTask = ObtenerAreasAsync(ct);
        var sexosTask = ObtenerSexosAsync(ct);
        var aniosTask = ObtenerAniosAsync(ct);
        await Task.WhenAll(deptTask, regTask, areasTask, sexosTask, aniosTask);
        return new CatalogosProyeccionDto(
            await deptTask, await regTask,
            await areasTask, await sexosTask, await aniosTask);
    }

    /// <summary>Lista departamentos desde usp_Catalogo_Departamentos_Listar.</summary>
    public async Task<IReadOnlyList<DepartamentoDto>> ObtenerDepartamentosAsync(CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Catalogo_Departamentos_Listar", 30);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<DepartamentoDto>();
        while (await r.ReadAsync(ct))
            list.Add(new DepartamentoDto(r.GetString(0), r.GetString(1)));
        return list;
    }

    /// <summary>Lista municipios, opcionalmente filtrados por departamento.</summary>
    public async Task<IReadOnlyList<MunicipioDto>> ObtenerMunicipiosAsync(
        string? codigoDepartamento = null, CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Catalogo_Municipios_Listar", 30);
        cmd.Parameters.AddWithValue("@CodigoDepartamento", (object?)codigoDepartamento ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<MunicipioDto>();
        while (await r.ReadAsync(ct))
            list.Add(new MunicipioDto(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return list;
    }

    /// <summary>Regionales de salud disponibles para filtros.</summary>
    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerRegionalesAsync(CancellationToken ct) =>
        EjecutarCatalogoSimpleAsync("dbo.usp_Catalogo_Regionales_Listar", ct);

    /// <summary>Áreas geográficas (urbana/rural) para filtros.</summary>
    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAreasAsync(CancellationToken ct) =>
        EjecutarCatalogoSimpleAsync("dbo.usp_Catalogo_Areas_Listar", ct);

    /// <summary>Sexos o categorías de población para filtros.</summary>
    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerSexosAsync(CancellationToken ct) =>
        EjecutarCatalogoSimpleAsync("dbo.usp_Catalogo_Sexos_Listar", ct);

    /// <summary>Años disponibles en las series de proyección.</summary>
    public async Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAniosAsync(CancellationToken ct = default)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, "dbo.usp_Catalogo_Anios_Listar", 60);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<CatalogoSimpleDto>();
        while (await r.ReadAsync(ct))
        {
            var v = r.GetString(0);
            list.Add(new CatalogoSimpleDto(v, v));
        }
        return list;
    }

    private async Task<IReadOnlyList<CatalogoSimpleDto>> EjecutarCatalogoSimpleAsync(string spName, CancellationToken ct)
    {
        await using var con = await AbrirAsync(ct);
        await using var cmd = Sp(con, spName, 60);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<CatalogoSimpleDto>();
        while (await r.ReadAsync(ct))
        {
            var v = r.GetString(0);
            list.Add(new CatalogoSimpleDto(v, v));
        }
        return list;
    }

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        return con;
    }

    private static SqlCommand Sp(SqlConnection con, string name, int timeout = 30) =>
        new(name, con) { CommandType = CommandType.StoredProcedure, CommandTimeout = timeout };
}
