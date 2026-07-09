using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace Observatorios.Api.Data;

/// <summary>
/// Repositorio ASIS del OSD: consultas paginadas sobre vistas vw_ASIS_* de población,
/// mortalidad, nacimientos e indicadores derivados para Casanare.
/// </summary>
public sealed class AsisRepository(IConfiguration config, IMemoryCache cache)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");
    private readonly bool _usarCapaFact = string.Equals(
        config["Asis:CapaPoblacion"], "fact", StringComparison.OrdinalIgnoreCase);
    private readonly int _idProyeccionDefault = config.GetValue("Asis:IdProyeccionDaneDefault", 1);

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);
    private const string CacheVigencias = "asis:catalogo:vigencias";
    private const string CacheProyecciones = "asis:catalogo:proyecciones";

    /// <param name="SqlFromFact">Vista sobre <c>fact_poblacion_proyeccion</c>; null = solo legacy.</param>
    /// <param name="MunicipioWhere">Condición SQL al filtrar por @CodigoMunicipio (null = sin filtro municipal).</param>
    private sealed record VistaDef(
        string SqlFromLegacy,
        string? SqlFromFact,
        string OrderBy,
        string? MunicipioWhere = null,
        bool FiltroNivelTerritorio = false);

    private static readonly Dictionary<string, VistaDef> Vistas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["poblacion-total"] = new("dbo.vw_ASIS_Poblacion_Total", "dbo.vw_ASIS_Poblacion_Total_Fact", "vigencia DESC"),
        ["poblacion-municipio"] = new("dbo.vw_ASIS_Poblacion_Municipio", "dbo.vw_ASIS_Poblacion_Municipio_Fact",
            "vigencia DESC, codigo_municipio", "codigo_municipio = @CodigoMunicipio"),
        ["poblacion-sexo"] = new("dbo.vw_ASIS_Poblacion_Sexo", "dbo.vw_ASIS_Poblacion_Sexo_Fact",
            "vigencia DESC, codigo_territorio_dane",
            "(codigo_municipio = @CodigoMunicipio OR codigo_territorio_dane = @CodigoMunicipio)"),
        ["poblacion-area"] = new("dbo.vw_ASIS_Poblacion_Area", "dbo.vw_ASIS_Poblacion_Area_Fact",
            "vigencia DESC, codigo_territorio_dane",
            "(codigo_municipio = @CodigoMunicipio OR codigo_territorio_dane = @CodigoMunicipio)"),
        ["poblacion-grupo-edad"] = new("dbo.vw_ASIS_Poblacion_GrupoEdad", "dbo.vw_ASIS_Poblacion_GrupoEdad_Fact",
            "vigencia DESC, id_grupo_edad",
            "(codigo_municipio = @CodigoMunicipio OR codigo_territorio_dane = @CodigoMunicipio)"),
        ["poblacion-curso-vida"] = new("dbo.vw_ASIS_Poblacion_CursoVida", "dbo.vw_ASIS_Poblacion_CursoVida_Fact",
            "vigencia DESC, id_curso_vida",
            "(codigo_municipio = @CodigoMunicipio OR codigo_territorio_dane = @CodigoMunicipio)"),
        ["piramide-poblacional"] = new("dbo.vw_ASIS_Piramide_Poblacional", null,
            "vigencia DESC, codigo_municipio, edad_simple", "codigo_municipio = @CodigoMunicipio"),
        ["mortalidad-total"] = new("dbo.vw_ASIS_Mortalidad_Total", null, "vigencia DESC"),
        ["mortalidad-municipio"] = new("dbo.vw_ASIS_Mortalidad_Municipio", null, "vigencia DESC, codigo_municipio",
            "codigo_municipio = @CodigoMunicipio"),
        ["mortalidad-sexo"] = new("dbo.vw_ASIS_Mortalidad_Sexo", null, "vigencia DESC",
            "codigo_municipio = @CodigoMunicipio"),
        ["mortalidad-area"] = new("dbo.vw_ASIS_Mortalidad_Area", null, "vigencia DESC",
            "codigo_municipio = @CodigoMunicipio"),
        ["mortalidad-grupo-edad"] = new("dbo.vw_ASIS_Mortalidad_GrupoEdad", null, "vigencia DESC, id_grupo_edad",
            "codigo_municipio = @CodigoMunicipio"),
        ["mortalidad-curso-vida"] = new("dbo.vw_ASIS_Mortalidad_CursoVida", null, "vigencia DESC, id_curso_vida",
            "codigo_municipio = @CodigoMunicipio"),
        ["mortalidad-detalle"] = new("dbo.vw_ASIS_Mortalidad_Detalle", null,
            "vigencia DESC, codigo_municipio, grupo_etareo_quinquenios_dane, nombre_curso_vida, sexo",
            "codigo_municipio = @CodigoMunicipio"),
        ["tasa-bruta-mortalidad"] = new("dbo.vw_ASIS_Tasa_Bruta_Mortalidad", null,
            "vigencia DESC, nivel_territorio, codigo_municipio", "codigo_municipio = @CodigoMunicipio",
            FiltroNivelTerritorio: true),
        ["serie-mortalidad"] = new("dbo.vw_ASIS_Serie_Mortalidad", null, "vigencia ASC"),
        ["comparativo-poblacion-mortalidad"] = new("dbo.vw_ASIS_Comparativo_Poblacion_Mortalidad", null,
            "vigencia DESC, codigo_municipio", "codigo_municipio = @CodigoMunicipio"),
        ["nacimientos-total"] = new("dbo.vw_ASIS_Nacimientos_Total", null, "vigencia DESC"),
        ["nacimientos-municipio"] = new("dbo.vw_ASIS_Nacimientos_Municipio", null, "vigencia DESC, codigo_municipio",
            "codigo_municipio = @CodigoMunicipio"),
        ["nacimientos-sexo"] = new("dbo.vw_ASIS_Nacimientos_Sexo", null, "vigencia DESC",
            "codigo_municipio = @CodigoMunicipio"),
        ["nacimientos-area"] = new("dbo.vw_ASIS_Nacimientos_Area", null, "vigencia DESC",
            "codigo_municipio = @CodigoMunicipio"),
        ["nacimientos-grupo-edad"] = new("dbo.vw_ASIS_Nacimientos_GrupoEdad", null, "vigencia DESC, id_grupo_edad_madre",
            "codigo_municipio = @CodigoMunicipio"),
        ["nacimientos-detalle"] = new("dbo.vw_ASIS_Nacimientos_Detalle", null,
            "vigencia DESC, codigo_municipio, grupo_etareo_quinquenios_dane, nivel_educativo, sexo",
            "codigo_municipio = @CodigoMunicipio"),
        ["nacimientos-nivel-educativo"] = new("dbo.vw_ASIS_Nacimientos_NivelEducativo", null, "vigencia DESC, id_nivel_educativo",
            "codigo_municipio = @CodigoMunicipio"),
        ["nacimientos-pertenencia-etnica"] = new("dbo.vw_ASIS_Nacimientos_PertenenciaEtnica", null, "vigencia DESC, id_pertenencia_etnica",
            "codigo_municipio = @CodigoMunicipio"),
        ["nacimientos-peso-al-nacer"] = new("dbo.vw_ASIS_Nacimientos_PesoAlNacer", null, "vigencia DESC, id_peso_al_nacer",
            "codigo_municipio = @CodigoMunicipio"),
        ["nacimientos-semanas-gestacion"] = new("dbo.vw_ASIS_Nacimientos_SemanasGestacion", null, "vigencia DESC, id_semanas_gestacion",
            "codigo_municipio = @CodigoMunicipio"),
    };

    /// <summary>Indica si las vistas de población usan capa fact (vw_*_Fact).</summary>
    public bool UsarCapaFact => _usarCapaFact;
    /// <summary>Id de proyección DANE por defecto cuando el cliente no envía filtro.</summary>
    public int IdProyeccionDaneDefault => _idProyeccionDefault;

    /// <summary>Claves de indicadores ASIS expuestos en la API.</summary>
    public static IReadOnlyCollection<string> ClavesValidas => Vistas.Keys.ToArray();

    /// <summary>Indica si la clave admite filtro por código de municipio.</summary>
    public static bool SoportaFiltroMunicipio(string clave) =>
        Vistas.TryGetValue(clave, out var v) && !string.IsNullOrEmpty(v.MunicipioWhere);

    /// <summary>Catálogo de proyecciones DANE desde dbo.dim_proyeccion_dane.</summary>
    public async Task<IReadOnlyList<ProyeccionDaneDto>> ListarProyeccionesAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheProyecciones, out IReadOnlyList<ProyeccionDaneDto>? cached) && cached is not null)
            return cached;

        const string sql = """
            SELECT id_proyeccion_dane, nombre_proyeccion, anio_publicacion
            FROM dbo.dim_proyeccion_dane WITH (NOLOCK)
            ORDER BY anio_publicacion DESC, id_proyeccion_dane DESC
            """;

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 };
        var list = new List<ProyeccionDaneDto>();
        try
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new ProyeccionDaneDto(
                    r.GetInt32(0),
                    r.GetString(1),
                    r.IsDBNull(2) ? (int?)null : r.GetInt32(2)));
            }
        }
        catch (SqlException)
        {
            return Array.Empty<ProyeccionDaneDto>();
        }

        cache.Set(CacheProyecciones, list, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) });
        return list;
    }

    /// <summary>Años de vigencia disponibles en vistas ASIS (población, mortalidad, nacimientos).</summary>
    public async Task<IReadOnlyList<int>> ListarVigenciasAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheVigencias, out IReadOnlyList<int>? cached) && cached is not null)
            return cached;

        var poblacionFrom = _usarCapaFact
            ? "dbo.vw_ASIS_Poblacion_Total_Fact WITH (NOLOCK)"
            : "dbo.vw_ASIS_Poblacion_Total WITH (NOLOCK)";
        var proyeccionFilter = _usarCapaFact
            ? $" WHERE id_proyeccion_dane = {_idProyeccionDefault}"
            : "";

        var sql = $"""
            SELECT DISTINCT v.vigencia
            FROM (
                SELECT vigencia FROM {poblacionFrom}{proyeccionFilter}
                UNION
                SELECT vigencia FROM dbo.vw_ASIS_Mortalidad_Total WITH (NOLOCK)
                UNION
                SELECT vigencia FROM dbo.vw_ASIS_Nacimientos_Total WITH (NOLOCK)
            ) AS v
            WHERE v.vigencia IS NOT NULL
            ORDER BY v.vigencia DESC
            """;

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 };
        var list = new List<int>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(r.GetInt32(0));

        cache.Set(CacheVigencias, list, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) });
        return list;
    }

    private static readonly HashSet<string> ColumnasOcultas = new(StringComparer.OrdinalIgnoreCase)
    {
        "id_proyeccion_dane",
        "nombre_proyeccion",
        "fuente_datos",
        "criterio_agregacion",
        "id_sexo",
        "id_area",
        "id_grupo_edad",
        "id_curso_vida",
        "codigo_territorio_dane",
        "area_proyeccion",
        "sexo_dim",
        "codigo_grupo_edad_dim",
        "codigo_curso_vida_dim",
        "id_grupo_edad_madre",
        "codigo_grupo_edad_madre",
        "id_nivel_educativo",
        "id_pertenencia_etnica",
        "id_peso_al_nacer",
        "id_semanas_gestacion",
        "codigo_nivel_educativo",
        "codigo_pertenencia_etnica",
        "codigo_peso_al_nacer",
        "codigo_semanas_gestacion",
        "categoria_normalizada",
    };

    /// <summary>
    /// Consulta paginada de un indicador ASIS con filtros de vigencia, municipio y proyección DANE.
    /// </summary>
    public async Task<VistaPoblacionPaginada> ConsultarPaginadoAsync(
        string clave, int pagina, int tamanoPagina,
        int? vigencia = null, string? codigoMunicipio = null, string? nivelTerritorio = null,
        int? idProyeccionDane = null,
        CancellationToken ct = default)
    {
        if (!Vistas.TryGetValue(clave, out var vista))
            throw new ArgumentException($"Indicador ASIS no reconocido: {clave}", nameof(clave));

        var sqlFrom = ResolveSqlFrom(vista);
        var filtraProyeccion = UsaProyeccionDane(vista);
        var idProyeccion = filtraProyeccion
            ? (idProyeccionDane is > 0 ? idProyeccionDane.Value : _idProyeccionDefault)
            : (int?)null;

        var tam = Math.Clamp(tamanoPagina, 1, 200);
        var p = Math.Max(1, pagina);
        var cacheKey = $"asis|v9|{_usarCapaFact}|{clave}|{p}|{tam}|{vigencia}|{codigoMunicipio}|{nivelTerritorio}|{idProyeccion}";
        if (cache.TryGetValue(cacheKey, out VistaPoblacionPaginada? cached) && cached is not null)
            return cached;

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        var (whereSql, parameters) = BuildWhere(vista, vigencia, codigoMunicipio, nivelTerritorio, idProyeccion);
        var total = await CountAsync(con, sqlFrom, whereSql, parameters, ct);
        var totalPaginas = total == 0 ? 0 : (int)Math.Ceiling(total / (double)tam);
        if (totalPaginas > 0 && p > totalPaginas) p = totalPaginas;

        var offset = (p - 1) * tam;
        var (columnas, filas) = await FetchPageAsync(con, sqlFrom, vista.OrderBy, whereSql, parameters, offset, tam, ct);
        (columnas, filas) = FiltrarColumnasVisibles(columnas, filas);

        var result = new VistaPoblacionPaginada(clave, p, tam, total, totalPaginas, columnas, filas);
        cache.Set(cacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
        return result;
    }

    /// <summary>Filas completas para exportación Excel (sin paginación UI ni caché).</summary>
    public async Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarFilasExportacionAsync(
        string clave, int? vigencia = null, string? codigoMunicipio = null, CancellationToken ct = default)
    {
        if (!Vistas.TryGetValue(clave, out var vista))
            throw new ArgumentException($"Indicador ASIS no reconocido: {clave}", nameof(clave));

        var sqlFrom = ResolveSqlFrom(vista);
        var filtraProyeccion = UsaProyeccionDane(vista);
        var idProyeccion = filtraProyeccion ? _idProyeccionDefault : (int?)null;

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        var (whereSql, parameters) = BuildWhere(vista, vigencia, codigoMunicipio, null, idProyeccion);
        var total = await CountAsync(con, sqlFrom, whereSql, parameters, ct);
        var tam = (int)Math.Min(total, 500_000);
        if (tam == 0)
            return Array.Empty<Dictionary<string, object?>>();

        var (_, filas) = await FetchPageAsync(con, sqlFrom, vista.OrderBy, whereSql, parameters, 0, tam, ct);
        return filas;
    }

    private string ResolveSqlFrom(VistaDef vista) =>
        _usarCapaFact && vista.SqlFromFact is not null ? vista.SqlFromFact : vista.SqlFromLegacy;

    private static bool UsaProyeccionDane(VistaDef vista) => vista.SqlFromFact is not null;

    private static (string WhereSql, List<SqlParameter> Parameters) BuildWhere(
        VistaDef vista, int? vigencia, string? codigoMunicipio, string? nivelTerritorio, int? idProyeccionDane)
    {
        var parts = new List<string>();
        var pars = new List<SqlParameter>();

        if (idProyeccionDane is > 0 && UsaProyeccionDane(vista))
        {
            parts.Add("id_proyeccion_dane = @IdProyeccionDane");
            pars.Add(new SqlParameter("@IdProyeccionDane", SqlDbType.Int) { Value = idProyeccionDane.Value });
        }

        if (vigencia is > 0)
        {
            parts.Add("vigencia = @Vigencia");
            pars.Add(new SqlParameter("@Vigencia", SqlDbType.Int) { Value = vigencia.Value });
        }

        var mun = string.IsNullOrWhiteSpace(codigoMunicipio) ? null : codigoMunicipio.Trim();
        if (mun is not null && vista.MunicipioWhere is not null)
        {
            parts.Add(vista.MunicipioWhere);
            pars.Add(new SqlParameter("@CodigoMunicipio", SqlDbType.NVarChar, 10) { Value = mun });
        }

        if (vista.FiltroNivelTerritorio && !string.IsNullOrWhiteSpace(nivelTerritorio))
        {
            parts.Add("nivel_territorio = @NivelTerritorio");
            pars.Add(new SqlParameter("@NivelTerritorio", SqlDbType.NVarChar, 20)
            {
                Value = nivelTerritorio.Trim().ToUpperInvariant()
            });
        }

        var whereSql = parts.Count == 0 ? "" : " WHERE " + string.Join(" AND ", parts);
        return (whereSql, pars);
    }

    private static SqlParameter[] ClonarParametros(IEnumerable<SqlParameter> parameters) =>
        parameters.Select(p => (SqlParameter)((ICloneable)p).Clone()).ToArray();

    private static async Task<long> CountAsync(
        SqlConnection con, string sqlFrom, string whereSql, List<SqlParameter> parameters, CancellationToken ct)
    {
        var sql = $"SELECT COUNT_BIG(1) FROM {sqlFrom}{whereSql}";
        await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 };
        cmd.Parameters.AddRange(ClonarParametros(parameters));
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is null or DBNull ? 0 : Convert.ToInt64(o);
    }

    private static async Task<(List<string> Columnas, List<Dictionary<string, object?>> Filas)> FetchPageAsync(
        SqlConnection con, string sqlFrom, string orderBy, string whereSql, List<SqlParameter> parameters,
        int offset, int tam, CancellationToken ct)
    {
        var sql = new StringBuilder();
        sql.Append("SELECT * FROM ").Append(sqlFrom).Append(whereSql);
        sql.Append(" ORDER BY ").Append(orderBy);
        sql.Append(" OFFSET @Offset ROWS FETCH NEXT @Tam ROWS ONLY");

        await using var cmd = new SqlCommand(sql.ToString(), con) { CommandTimeout = 120 };
        cmd.Parameters.AddRange(ClonarParametros(parameters));
        cmd.Parameters.Add(new SqlParameter("@Offset", SqlDbType.Int) { Value = offset });
        cmd.Parameters.Add(new SqlParameter("@Tam", SqlDbType.Int) { Value = tam });

        var columnas = new List<string>();
        var filas = new List<Dictionary<string, object?>>();

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        for (var i = 0; i < reader.FieldCount; i++)
            columnas.Add(reader.GetName(i));

        while (await reader.ReadAsync(ct))
        {
            var fila = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
                fila[columnas[i]] = reader.IsDBNull(i) ? null : NormalizarCelda(reader.GetValue(i));
            filas.Add(fila);
        }

        return (columnas, filas);
    }

    private static (List<string> Columnas, List<Dictionary<string, object?>> Filas) FiltrarColumnasVisibles(
        List<string> columnas, List<Dictionary<string, object?>> filas)
    {
        var visibles = columnas.Where(c => !ColumnasOcultas.Contains(c)).ToList();
        if (visibles.Count == columnas.Count)
            return (columnas, filas);

        var filasFiltradas = filas.Select(f =>
        {
            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var c in visibles)
            {
                if (f.TryGetValue(c, out var v))
                    d[c] = v;
            }
            return d;
        }).ToList();

        return (visibles, filasFiltradas);
    }

    private static object? NormalizarCelda(object valor) => valor switch
    {
        DateTime dt => dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt,
        decimal d => d,
        double d => d,
        float f => f,
        _ => valor
    };
}

public sealed record ProyeccionDaneDto(int Id, string Nombre, int? AnioPublicacion);
