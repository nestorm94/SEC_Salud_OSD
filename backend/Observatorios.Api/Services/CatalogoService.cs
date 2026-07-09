using Microsoft.Extensions.Caching.Memory;
using Observatorios.Api.Data;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

/// <summary>
/// Servicio de catálogos con caché en memoria para reducir consultas a usp_Catalogo_*.
/// </summary>
public sealed class CatalogoService(PoblacionCatalogosRepository repo, IMemoryCache cache) : ICatalogoService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private const string CacheProyeccion = "catalogos:proyeccion";
    private const string CacheMunicipiosPrefix = "catalogos:municipios:";

    /// <summary>Obtiene catálogos de proyección cacheados 6 horas.</summary>
    public async Task<CatalogosProyeccionDto> ObtenerCatalogosProyeccionAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(CacheProyeccion, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await repo.ObtenerCatalogosProyeccionAsync(ct);
        }))!;

    /// <summary>Lista departamentos sin caché (consulta directa).</summary>
    public Task<IReadOnlyList<DepartamentoDto>> ObtenerDepartamentosAsync(CancellationToken ct = default) =>
        repo.ObtenerDepartamentosAsync(ct);

    /// <summary>Lista todos los municipios de Casanare.</summary>
    public Task<IReadOnlyList<MunicipioDto>> ObtenerMunicipiosAsync(CancellationToken ct = default) =>
        repo.ObtenerMunicipiosAsync(null, ct);

    public async Task<IReadOnlyList<MunicipioDto>> ObtenerMunicipiosPorDepartamentoAsync(
        string codigoDepartamento,
        CancellationToken ct = default)
    {
        var key = CacheMunicipiosPrefix + codigoDepartamento.Trim();
        return (await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await repo.ObtenerMunicipiosAsync(codigoDepartamento, ct);
        }))!;
    }

    /// <summary>Regionales de salud para filtros de proyección.</summary>
    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerRegionalesAsync(CancellationToken ct = default) =>
        repo.ObtenerRegionalesAsync(ct);

    /// <summary>Áreas geográficas para filtros de proyección.</summary>
    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAreasAsync(CancellationToken ct = default) =>
        repo.ObtenerAreasAsync(ct);

    /// <summary>Sexos o categorías de población para filtros.</summary>
    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerSexosAsync(CancellationToken ct = default) =>
        repo.ObtenerSexosAsync(ct);

    /// <summary>Años disponibles en series de proyección.</summary>
    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAniosAsync(CancellationToken ct = default) =>
        repo.ObtenerAniosAsync(ct);
}
