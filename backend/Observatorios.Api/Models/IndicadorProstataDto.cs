namespace Observatorios.Api.Models;

public sealed record IndicadorProstataDto(
    string CodigoDane,
    string Territorio,
    string CodigoTerritorio,
    string Regional,
    int? Anio,
    string Area,
    decimal? Muertes,
    decimal? Poblacion,
    decimal? Coeficiente,
    decimal? Tasa);
