using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

/// <summary>
/// Contrato de acceso a catálogos territoriales y demográficos para filtros de proyección.
/// </summary>
public interface ICatalogoService
{
    /// <summary>Paquete consolidado de catálogos para la UI de proyección.</summary>
    Task<CatalogosProyeccionDto> ObtenerCatalogosProyeccionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DepartamentoDto>> ObtenerDepartamentosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MunicipioDto>> ObtenerMunicipiosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MunicipioDto>> ObtenerMunicipiosPorDepartamentoAsync(string codigoDepartamento, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerRegionalesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAreasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerSexosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAniosAsync(CancellationToken ct = default);
}
