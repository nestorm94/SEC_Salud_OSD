namespace Observatorios.Api.Services;

/// <summary>
/// Contrato de validación geográfica DIVIPOLA contra dimensiones dim_departamento/dim_municipio.
/// </summary>
public interface IGeografiaValidacionService
{
    /// <summary>Verifica que el código DANE de municipio exista en el catálogo.</summary>
    bool ValidarCodigoMunicipio(string codigoDane);
    /// <summary>Verifica que el nombre de municipio exista (normalizado).</summary>
    bool ValidarNombreMunicipio(string nombreMunicipio);
    /// <summary>Verifica coherencia código-nombre de municipio.</summary>
    bool ValidarCodigoYNombreMunicipio(string codigoDane, string nombreMunicipio);
    bool ValidarCodigoDepartamento(string codigoDepartamento);
    bool ValidarNombreDepartamento(string nombreDepartamento);
    /// <summary>Verifica que el municipio pertenezca al departamento indicado.</summary>
    bool ValidarDepartamentoMunicipio(string codigoDepartamento, string codigoMunicipio);
    /// <summary>Normaliza texto para comparación insensible a tildes y mayúsculas.</summary>
    string NormalizarTexto(string? texto);
    /// <summary>Catálogo en memoria de departamentos y municipios cargado al inicio.</summary>
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
