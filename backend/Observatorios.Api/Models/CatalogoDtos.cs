namespace Observatorios.Api.Models;

public sealed record DepartamentoDto(
    string CodigoDane,
    string NombreDepartamento);

public sealed record MunicipioDto(
    string CodigoDane,
    string CodigoDepartamento,
    string NombreMunicipio,
    string Regional);

public sealed record CatalogoSimpleDto(
    string Codigo,
    string Nombre);

/// <summary>Catálogos de filtros de proyección en una sola respuesta (menos viajes HTTP).</summary>
public sealed record CatalogosProyeccionDto(
    IReadOnlyList<DepartamentoDto> Departamentos,
    IReadOnlyList<CatalogoSimpleDto> Regionales,
    IReadOnlyList<CatalogoSimpleDto> Areas,
    IReadOnlyList<CatalogoSimpleDto> Sexos,
    IReadOnlyList<CatalogoSimpleDto> Anios);
