using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Data;
using Observatorios.Api.Services;

namespace Observatorios.Api.Endpoints;

public static class AsisEndpoints
{
    public static void MapAsisApi(this RouteGroupBuilder secured)
    {
        var asis = secured.MapGroup("/asis");
        var clavesRegex = string.Join("|", AsisRepository.ClavesValidas.Select(Regex.Escape));

        asis.MapGet("/vistas", (AsisRepository repo) =>
            Results.Ok(new
            {
                capaPoblacion = repo.UsarCapaFact ? "fact" : "legacy",
                idProyeccionDaneDefault = repo.IdProyeccionDaneDefault,
                vistas = AsisRepository.ClavesValidas,
                grupos = new[]
                {
                    new { id = "poblacion", label = "Población", claves = new[] { "poblacion-total", "poblacion-municipio", "poblacion-sexo", "poblacion-area", "poblacion-grupo-edad", "poblacion-curso-vida", "piramide-poblacional" } },
                    new { id = "mortalidad", label = "Mortalidad", claves = new[] { "mortalidad-total", "mortalidad-municipio", "mortalidad-detalle", "mortalidad-sexo", "mortalidad-area", "mortalidad-grupo-edad", "mortalidad-curso-vida" } },
                    new { id = "nacimientos", label = "Nacimientos", claves = new[] { "nacimientos-total", "nacimientos-municipio", "nacimientos-detalle", "nacimientos-sexo", "nacimientos-area", "nacimientos-grupo-edad", "nacimientos-nivel-educativo", "nacimientos-pertenencia-etnica", "nacimientos-peso-al-nacer", "nacimientos-semanas-gestacion" } },
                    new { id = "indicadores", label = "Indicadores", claves = new[] { "tasa-bruta-mortalidad", "serie-mortalidad", "comparativo-poblacion-mortalidad" } }
                }
            }));

        asis.MapGet("/catalogos/proyecciones", async (AsisRepository repo, CancellationToken ct) =>
        {
            var items = await repo.ListarProyeccionesAsync(ct);
            return Results.Ok(new
            {
                proyecciones = items.Select(p => new
                {
                    id = p.Id,
                    nombre = p.Nombre,
                    anioPublicacion = p.AnioPublicacion
                })
            });
        });

        asis.MapGet("/catalogos/vigencias", async (AsisRepository repo, CancellationToken ct) =>
        {
            var años = await repo.ListarVigenciasAsync(ct);
            return Results.Ok(new
            {
                vigencias = años.Select(y => new { codigo = y.ToString(), nombre = y.ToString() })
            });
        });

        asis.MapGet("/export/nacimientos/excel", ExportNacimientosExcel);
        asis.MapGet("/export/mortalidad/excel", ExportMortalidadExcel);

        var consulta = asis.MapGroup("/indicadores");
        consulta.MapGet("/{clave:regex(^(" + clavesRegex + ")$)}", ConsultarAsis);

        // Compatibilidad con clientes que aún llaman /api/asis/{clave} (solo claves válidas, no códigos DANE).
        asis.MapGet("/{clave:regex(^(" + clavesRegex + ")$)}", ConsultarAsis);
    }

    private static Task<IResult> ConsultarAsis(
        string clave,
        HttpRequest req,
        AsisRepository repo,
        CancellationToken ct) => ConsultarAsisJson(clave, req, repo, ct);

    private static async Task<IResult> ConsultarAsisJson(
        string clave, HttpRequest req, AsisRepository repo, CancellationToken ct)
    {
        clave = clave.Trim();
        if (!AsisRepository.ClavesValidas.Contains(clave, StringComparer.OrdinalIgnoreCase))
        {
            return Results.NotFound(new
            {
                error = "Indicador ASIS no encontrado.",
                detalle = $"La clave '{clave}' no es válida. Ejemplo: GET /api/asis/indicadores/poblacion-municipio?codigoMunicipio=85015"
            });
        }

        var pagina = int.TryParse(req.Query["pagina"], out var pq) ? pq : 1;
        var tamanoPagina = int.TryParse(req.Query["tamanoPagina"], out var tq) ? tq : 10;
        int? vigencia = int.TryParse(req.Query["vigencia"], out var vq) ? vq
            : int.TryParse(req.Query["ano"], out var aq) ? aq : null;
        var codigoMunicipio = NormalizarCodigoMunicipio(req.Query["codigoMunicipio"].FirstOrDefault());
        var nivelTerritorio = req.Query["nivelTerritorio"].FirstOrDefault();
        int? idProyeccionDane = int.TryParse(req.Query["idProyeccionDane"], out var pid) ? pid
            : int.TryParse(req.Query["idProyeccion"], out var pid2) ? pid2 : null;

        try
        {
            var r = await repo.ConsultarPaginadoAsync(
                clave, pagina, tamanoPagina, vigencia, codigoMunicipio, nivelTerritorio, idProyeccionDane, ct);
            return Results.Ok(new
            {
                clave = r.Clave,
                pagina = r.Pagina,
                tamanoPagina = r.TamanoPagina,
                totalFilas = r.TotalFilas,
                totalPaginas = r.TotalPaginas,
                columnas = r.Columnas,
                filas = r.Filas
            });
        }
        catch (ArgumentException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (SqlException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 502);
        }
    }

    private static string? NormalizarCodigoMunicipio(string? codigo)
    {
        var c = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
        if (c is null) return null;
        if (c.All(char.IsDigit) && c.Length is >= 1 and <= 5)
            return c.PadLeft(5, '0');
        return c;
    }

    private static async Task<IResult> ExportNacimientosExcel(
        HttpRequest req, AsisExcelExportService export, CancellationToken ct)
    {
        int? vigencia = int.TryParse(req.Query["vigencia"], out var vq) ? vq
            : int.TryParse(req.Query["ano"], out var aq) ? aq : null;
        var codigoMunicipio = NormalizarCodigoMunicipio(req.Query["codigoMunicipio"].FirstOrDefault());

        try
        {
            var bytes = await export.ExportNacimientosAsync(vigencia, codigoMunicipio, ct);
            var nombre = $"Nacimientos-Casanare-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
        }
        catch (SqlException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 502);
        }
    }

    private static async Task<IResult> ExportMortalidadExcel(
        HttpRequest req, AsisExcelExportService export, CancellationToken ct)
    {
        int? vigencia = int.TryParse(req.Query["vigencia"], out var vq) ? vq
            : int.TryParse(req.Query["ano"], out var aq) ? aq : null;
        var codigoMunicipio = NormalizarCodigoMunicipio(req.Query["codigoMunicipio"].FirstOrDefault());

        try
        {
            var bytes = await export.ExportMortalidadAsync(vigencia, codigoMunicipio, ct);
            var nombre = $"Defunciones-Casanare-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
        }
        catch (SqlException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 502);
        }
    }
}
