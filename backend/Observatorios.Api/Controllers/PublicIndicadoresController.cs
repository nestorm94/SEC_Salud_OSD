using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Observatorios.Api.Data;

namespace Observatorios.Api.Controllers;

/// <summary>
/// Endpoints públicos (sin autenticación) de indicadores de salud del OSD Casanare
/// para consumo externo o portales abiertos.
/// </summary>
[ApiController]
[Route("api/public/indicadores")]
[AllowAnonymous]
public sealed class PublicIndicadoresController(IndicadoresRepository repo) : ControllerBase
{
    /// <summary>
    /// Lista datos de mortalidad por cáncer de próstata desde la vista validada en BD.
    /// </summary>
    /// <param name="codigoDane">Filtro por código DANE del territorio.</param>
    /// <param name="territorio">Filtro por nombre de territorio.</param>
    /// <param name="regional">Filtro por regional de salud.</param>
    /// <param name="anio">Filtro por año de la serie.</param>
    /// <param name="area">Filtro por área geográfica (urbana/rural).</param>
    /// <param name="limit">Máximo de filas a retornar (default 20000).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>JSON con tasas, población y muertes por territorio.</returns>
    [HttpGet("prostata")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProstata(
        [FromQuery] string? codigoDane,
        [FromQuery] string? territorio,
        [FromQuery] string? regional,
        [FromQuery] int? anio,
        [FromQuery] string? area,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var rows = await repo.ListarProstataAsync(
            codigoDane, territorio, regional, anio, area, limit ?? 20000, ct);

        var data = rows.Select(r => new
        {
            codigoDane = r.CodigoDane,
            territorio = r.Territorio,
            codigoTerritorio = r.CodigoTerritorio,
            regional = r.Regional,
            anio = r.Anio,
            area = r.Area,
            muertes = r.Muertes,
            poblacion = r.Poblacion,
            coeficiente = r.Coeficiente,
            tasa = r.Tasa
        });
        return Ok(data);
    }
}
