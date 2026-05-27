using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Observatorios.Api.Data;

namespace Observatorios.Api.Controllers;

[ApiController]
[Route("api/public/indicadores")]
[AllowAnonymous]
public sealed class PublicIndicadoresController(IndicadoresRepository repo) : ControllerBase
{
    /// <summary>PÚBLICO - Indicador próstata (vista validada).</summary>
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
