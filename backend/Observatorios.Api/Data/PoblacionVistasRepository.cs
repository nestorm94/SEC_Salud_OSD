using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace Observatorios.Api.Data;

/// <summary>
/// Consulta vistas de proyección de población. Los nombres de vista solo salen de un mapa fijo (sin texto del cliente).
/// </summary>
/// <remarks>
/// Proceso equivalente a ejecutar en SSMS <c>SELECT * FROM dbo.vw_...</c>, con paginación en el servidor:
/// <list type="number">
/// <item>Se abre <see cref="SqlConnection"/> con <c>ConnectionStrings:Default</c> (misma base que uses en SSMS).</item>
/// <item><c>SELECT COUNT_BIG(*) FROM [dbo].[vw_...]</c> obtiene el total de filas (para páginas y totales).</item>
/// <item><c>SELECT * FROM [dbo].[vw_...] ORDER BY (SELECT NULL) OFFSET @offset FETCH NEXT @take</c> devuelve solo la página pedida;
/// es el mismo conjunto de columnas que <c>SELECT *</c>; <c>ORDER BY (SELECT NULL)</c> solo cumple la regla de SQL Server de que OFFSET/FETCH requiere ORDER BY.</item>
/// <item>El lector mapea cada fila a <c>Dictionary&lt;string, object?&gt;</c> y se serializa a JSON en la API.</item>
/// </list>
/// </remarks>
public sealed class PoblacionVistasRepository(IConfiguration config, IMemoryCache cache)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);

    /// <summary>Nombres entre corchetes, mismo esquema que en SSMS: <c>[dbo].[vw_...]</c>.</summary>
    private static readonly Dictionary<string, string> VistaSqlPorClave = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nacional-casanare"] = "[dbo].[vw_Poblacion_Nacional_Casanare]",
        ["curso-vida"] = "[dbo].[vw_Reporte_Poblacion_CursoVida_Unificado]",
        ["quinquenios"] = "[dbo].[vw_Reporte_Poblacion_Quinquenios_Unificado]",
    };

    public static IReadOnlyCollection<string> ClavesValidas => VistaSqlPorClave.Keys;

    public async Task<VistaPoblacionPaginada> ConsultarPaginadoAsync(
        string clave,
        int pagina,
        int tamanoPagina,
        string? territorio = null,
        string? regional = null,
        string? area = null,
        string? sexo = null,
        int? ano = null,
        string? codigoDepartamento = null,
        string? codigoMunicipio = null,
        CancellationToken ct = default)
    {
        if (!VistaSqlPorClave.TryGetValue(clave, out var vistaSql))
            throw new ArgumentException($"Vista no reconocida: {clave}", nameof(clave));

        var tam = Math.Clamp(tamanoPagina, 1, 200);
        var p = Math.Max(1, pagina);
        var cacheKey = CacheKey(clave, p, tam, territorio, regional, area, sexo, ano, codigoDepartamento, codigoMunicipio);
        if (cache.TryGetValue(cacheKey, out VistaPoblacionPaginada? cached) && cached is not null)
            return cached;

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_ProyeccionPoblacion_ConsultarPaginado", ct))
        {
            var resultSp = await ConsultarPaginadoDesdeSpAsync(
                con, clave, p, tam, territorio, regional, area, sexo, ano, codigoDepartamento, codigoMunicipio, ct);
            cache.Set(cacheKey, resultSp, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });
            return resultSp;
        }

        var offset = (p - 1) * tam;

        var filtros = await ConstruirFiltrosAsync(con, territorio, regional, area, sexo, ano, codigoDepartamento, codigoMunicipio, ct);
        var whereSql = filtros.WhereSql;

        long totalFilas;
        var countSql = $"SELECT COUNT_BIG(*) FROM {vistaSql}{whereSql}";
        await using (var cmdCount = new SqlCommand(countSql, con) { CommandTimeout = 300 })
        {
            foreach (var pFiltro in filtros.Parametros)
                cmdCount.Parameters.Add(pFiltro);
            var scalar = await cmdCount.ExecuteScalarAsync(ct);
            totalFilas = scalar is long l ? l : Convert.ToInt64(scalar, System.Globalization.CultureInfo.InvariantCulture);
        }

        var totalPaginas = totalFilas == 0 ? 0 : (int)Math.Ceiling(totalFilas / (double)tam);
        if (totalPaginas > 0 && p > totalPaginas)
            p = totalPaginas;

        offset = (p - 1) * tam;

        var dataSql = $"""
SELECT * FROM {vistaSql}{whereSql}
ORDER BY (SELECT NULL)
OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY
""";

        await using var cmd = new SqlCommand(dataSql, con) { CommandTimeout = 300 };
        foreach (var pFiltro in filtros.Parametros)
            cmd.Parameters.Add((SqlParameter)((ICloneable)pFiltro).Clone());
        cmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
        cmd.Parameters.Add("@take", SqlDbType.Int).Value = tam;

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

        var columnas = new List<string>(reader.FieldCount);
        for (var i = 0; i < reader.FieldCount; i++)
            columnas.Add(reader.GetName(i));

        var filas = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var fila = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var nombre = columnas[i];
                fila[nombre] = reader.IsDBNull(i) ? null : NormalizarCelda(reader.GetValue(i));
            }
            filas.Add(fila);
        }

        var result = new VistaPoblacionPaginada(clave, p, tam, totalFilas, totalPaginas, columnas, filas);
        cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });
        return result;
    }

    private static async Task<VistaPoblacionPaginada> ConsultarPaginadoDesdeSpAsync(
        SqlConnection con,
        string clave,
        int pagina,
        int tamanoPagina,
        string? territorio,
        string? regional,
        string? area,
        string? sexo,
        int? ano,
        string? codigoDepartamento,
        string? codigoMunicipio,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("dbo.usp_ProyeccionPoblacion_ConsultarPaginado", con)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 300
        };
        cmd.Parameters.AddWithValue("@Clave", clave);
        cmd.Parameters.AddWithValue("@Pagina", pagina);
        cmd.Parameters.AddWithValue("@TamanoPagina", tamanoPagina);
        cmd.Parameters.AddWithValue("@Territorio", (object?)territorio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Regional", (object?)regional ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Area", (object?)area ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Sexo", (object?)sexo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Anio", (object?)ano ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CodigoDepartamento", (object?)codigoDepartamento ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CodigoMunicipio", (object?)codigoMunicipio ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

        long totalFilas = 0;
        if (await reader.ReadAsync(ct))
            totalFilas = reader.GetInt64(0);

        var p = pagina;
        var tam = tamanoPagina;
        var totalPaginas = totalFilas == 0 ? 0 : (int)Math.Ceiling(totalFilas / (double)tam);
        if (totalPaginas > 0 && p > totalPaginas)
            p = totalPaginas;

        var columnas = new List<string>();
        var filas = new List<Dictionary<string, object?>>();

        if (await reader.NextResultAsync(ct) && reader.HasRows)
        {
            for (var i = 0; i < reader.FieldCount; i++)
                columnas.Add(reader.GetName(i));

            while (await reader.ReadAsync(ct))
            {
                var fila = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var nombre = columnas[i];
                    fila[nombre] = reader.IsDBNull(i) ? null : NormalizarCelda(reader.GetValue(i));
                }
                filas.Add(fila);
            }
        }

        return new VistaPoblacionPaginada(clave, p, tam, totalFilas, totalPaginas, columnas, filas);
    }

    private static string CacheKey(
        string clave,
        int pagina,
        int tamanoPagina,
        string? territorio,
        string? regional,
        string? area,
        string? sexo,
        int? ano,
        string? codigoDepartamento,
        string? codigoMunicipio)
    {
        static string N(string? s) => (s ?? "").Trim().ToUpperInvariant();
        return string.Join('|',
            "proy-v1",
            clave.Trim().ToLowerInvariant(),
            pagina,
            tamanoPagina,
            N(territorio),
            N(regional),
            N(area),
            N(sexo),
            ano?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            N(codigoDepartamento),
            N(codigoMunicipio));
    }

    private static async Task<(string WhereSql, List<SqlParameter> Parametros)> ConstruirFiltrosAsync(
        SqlConnection con,
        string? territorio,
        string? regional,
        string? area,
        string? sexo,
        int? ano,
        string? codigoDepartamento,
        string? codigoMunicipio,
        CancellationToken ct)
    {
        var where = new List<string>();
        var ps = new List<SqlParameter>();

        void AddTexto(string columnaSql, string param, string? valor)
        {
            var v = (valor ?? "").Trim();
            if (string.IsNullOrWhiteSpace(v)) return;

            var usaLike = v.Contains('%', StringComparison.Ordinal) || v.Contains('_', StringComparison.Ordinal);
            where.Add(usaLike ? $"{columnaSql} LIKE @{param}" : $"{columnaSql} = @{param}");
            ps.Add(new SqlParameter("@" + param, SqlDbType.NVarChar, 400) { Value = v });
        }

        AddTexto("[Territorio]", "territorio", territorio);
        AddTexto("[Regional]", "regional", regional);
        AddTexto("[Área]", "area", area);
        AddTexto("[Sexo]", "sexo", sexo);

        if (ano is not null)
        {
            where.Add("[Año] = @ano");
            ps.Add(new SqlParameter("@ano", SqlDbType.Int) { Value = ano.Value });
        }

        var codDep = (codigoDepartamento ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(codDep))
        {
            var sub = await SubconsultasTerritorioPorDepartamentoAsync(con, ct);
            if (sub.Count > 0)
            {
                where.Add($"[Territorio] IN ({string.Join(" UNION ", sub)})");
                ps.Add(new SqlParameter("@codDep", SqlDbType.NVarChar, 10) { Value = codDep });
            }
        }

        var codMun = (codigoMunicipio ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(codMun))
        {
            var sub = await SubconsultasTerritorioPorMunicipioAsync(con, ct);
            if (sub.Count > 0)
            {
                where.Add($"[Territorio] IN ({string.Join(" UNION ", sub)})");
                ps.Add(new SqlParameter("@codMun", SqlDbType.NVarChar, 10) { Value = codMun });
            }
        }

        var whereSql = where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where);
        return (whereSql, ps);
    }

    private static async Task<List<string>> SubconsultasTerritorioPorDepartamentoAsync(SqlConnection con, CancellationToken ct)
    {
        var parts = new List<string>();
        if (await DimCatalogSql.TablaExisteAsync(con, "dbo.dim_municipio", ct))
        {
            var colDep = await DimCatalogSql.ColumnaDepartamentoMunicipioAsync(con, "dbo.dim_municipio", ct);
            if (colDep is not null)
            {
                parts.Add($"""
SELECT LTRIM(RTRIM(CAST(m.nombre_municipio AS nvarchar(300))))
FROM dbo.dim_municipio m
WHERE LTRIM(RTRIM(CAST(m.{colDep} AS nvarchar(10)))) = @codDep
""");
            }
        }
        if (await DimCatalogSql.TablaExisteAsync(con, "dbo.dim_municipios", ct))
        {
            var colDep = await DimCatalogSql.ColumnaDepartamentoMunicipioAsync(con, "dbo.dim_municipios", ct);
            if (colDep is not null)
            {
                parts.Add($"""
SELECT LTRIM(RTRIM(CAST(m.nombre_municipio AS nvarchar(300))))
FROM dbo.dim_municipios m
WHERE LTRIM(RTRIM(CAST(m.{colDep} AS nvarchar(10)))) = @codDep
""");
            }
        }
        if (await DimCatalogSql.TablaExisteAsync(con, "dbo.dim_departamento", ct))
            parts.Add("""
SELECT LTRIM(RTRIM(CAST(d.nombre_departamento AS nvarchar(300))))
FROM dbo.dim_departamento d
WHERE LTRIM(RTRIM(CAST(d.cod_departamento AS nvarchar(10)))) = @codDep
""");
        if (await DimCatalogSql.TablaExisteAsync(con, "dbo.dim_departamentos", ct))
            parts.Add("""
SELECT LTRIM(RTRIM(CAST(d.nombre_departamento AS nvarchar(300))))
FROM dbo.dim_departamentos d
WHERE LTRIM(RTRIM(CAST(d.codigo_departamento AS nvarchar(10)))) = @codDep
""");
        return parts;
    }

    private static async Task<List<string>> SubconsultasTerritorioPorMunicipioAsync(SqlConnection con, CancellationToken ct)
    {
        var parts = new List<string>();
        if (await DimCatalogSql.TablaExisteAsync(con, "dbo.dim_municipio", ct))
        {
            var col = await DimCatalogSql.ColumnaCodigoMunicipioAsync(con, "dbo.dim_municipio", ct);
            if (col is not null)
            {
                parts.Add($"""
SELECT LTRIM(RTRIM(CAST(m.nombre_municipio AS nvarchar(300))))
FROM dbo.dim_municipio m
WHERE LTRIM(RTRIM(CAST(m.{col} AS nvarchar(10)))) = @codMun
""");
            }
        }
        if (await DimCatalogSql.TablaExisteAsync(con, "dbo.dim_municipios", ct))
        {
            var col = await DimCatalogSql.ColumnaCodigoMunicipioAsync(con, "dbo.dim_municipios", ct);
            if (col is not null)
            {
                parts.Add($"""
SELECT LTRIM(RTRIM(CAST(m.nombre_municipio AS nvarchar(300))))
FROM dbo.dim_municipios m
WHERE LTRIM(RTRIM(CAST(m.{col} AS nvarchar(10)))) = @codMun
""");
            }
        }
        return parts;
    }

    private static object? NormalizarCelda(object valor)
    {
        return valor switch
        {
            DateTime dt => dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt,
            decimal d => d,
            double x => x,
            float f => f,
            _ => valor
        };
    }
}

public sealed record VistaPoblacionPaginada(
    string Clave,
    int Pagina,
    int TamanoPagina,
    long TotalFilas,
    int TotalPaginas,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<Dictionary<string, object?>> Filas
);
