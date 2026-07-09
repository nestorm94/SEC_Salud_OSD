namespace Observatorios.Api.Models;

/// <summary>Departamento DANE usado como filtro territorial en proyección de población.</summary>
public sealed record DepartamentoDto(
    string CodigoDane,
    string NombreDepartamento);

/// <summary>Municipio DANE con su departamento y regional de salud asociada.</summary>
public sealed record MunicipioDto(
    string CodigoDane,
    string CodigoDepartamento,
    string NombreMunicipio,
    string Regional);

/// <summary>Par código-nombre genérico para catálogos simples (regional, área, sexo, año).</summary>
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
