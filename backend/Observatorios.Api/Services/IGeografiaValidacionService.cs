namespace Observatorios.Api.Services;

public interface IGeografiaValidacionService
{
    bool ValidarCodigoMunicipio(string codigoDane);
    bool ValidarNombreMunicipio(string nombreMunicipio);
    bool ValidarCodigoYNombreMunicipio(string codigoDane, string nombreMunicipio);
    bool ValidarCodigoDepartamento(string codigoDepartamento);
    bool ValidarNombreDepartamento(string nombreDepartamento);
    bool ValidarDepartamentoMunicipio(string codigoDepartamento, string codigoMunicipio);
    string NormalizarTexto(string? texto);

    GeografiaCatalogoContext ObtenerContexto();
}

public sealed record GeografiaResumenDto(
    int TotalRegistrosEvaluados,
    int CodigosMunicipioInvalidos,
    int MunicipiosInvalidos,
    int CodigosDepartamentoInvalidos,
    int DepartamentosInvalidos,
    int InconsistenciasCodigoMunicipio,
    int InconsistenciasDepartamentoMunicipio,
    string? Observacion);

public sealed record MunicipioGeoInfo(
    string CodigoMunicipio,
    string NombreMunicipio,
    string CodigoDepartamento,
    string NombreDepartamento);

public sealed class GeografiaCatalogoContext
{
    public Dictionary<string, string> DepartamentosPorCodigo { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DepartamentosPorNombreNormalizado { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, MunicipioGeoInfo> MunicipiosPorCodigo { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<MunicipioGeoInfo>> MunicipiosPorNombreNormalizado { get; } = new(StringComparer.OrdinalIgnoreCase);
}
