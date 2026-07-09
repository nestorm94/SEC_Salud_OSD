namespace Observatorios.Api.Models;

/// <summary>
/// DTO con datos de tasa de mortalidad por cáncer de próstata por territorio,
/// expuesto en consultas públicas y autenticadas del OSD Casanare.
/// </summary>
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
