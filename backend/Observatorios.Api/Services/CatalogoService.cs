using Microsoft.Extensions.Caching.Memory;
using Observatorios.Api.Data;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

public sealed class CatalogoService(PoblacionCatalogosRepository repo, IMemoryCache cache) : ICatalogoService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private const string CacheProyeccion = "catalogos:proyeccion";
    private const string CacheMunicipiosPrefix = "catalogos:municipios:";

    public async Task<CatalogosProyeccionDto> ObtenerCatalogosProyeccionAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(CacheProyeccion, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await repo.ObtenerCatalogosProyeccionAsync(ct);
        }))!;

    public Task<IReadOnlyList<DepartamentoDto>> ObtenerDepartamentosAsync(CancellationToken ct = default) =>
        repo.ObtenerDepartamentosAsync(ct);

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

    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerRegionalesAsync(CancellationToken ct = default) =>
        repo.ObtenerRegionalesAsync(ct);

    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAreasAsync(CancellationToken ct = default) =>
        repo.ObtenerAreasAsync(ct);

    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerSexosAsync(CancellationToken ct = default) =>
        repo.ObtenerSexosAsync(ct);

    public Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAniosAsync(CancellationToken ct = default) =>
        repo.ObtenerAniosAsync(ct);
}
