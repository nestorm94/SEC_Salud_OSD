using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace Observatorios.Api.Data;

/// <summary>
/// Consulta paginada de vistas de proyección de población del OSD Casanare
/// mediante usp_ProyeccionPoblacion_ConsultarPaginado con caché en memoria.
/// </summary>
public sealed class PoblacionVistasRepository(IConfiguration config, IMemoryCache cache)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);

    /// <summary>Claves de vista admitidas: nacional-casanare, curso-vida, quinquenios.</summary>
    public static readonly IReadOnlyCollection<string> ClavesValidas =
        ["nacional-casanare", "curso-vida", "quinquenios"];

    /// <summary>
    /// Ejecuta consulta paginada con filtros territoriales; resultados cacheados 3 minutos.
    /// </summary>
    /// <returns>Columnas dinámicas y filas según la vista solicitada.</returns>
    public async Task<VistaPoblacionPaginada> ConsultarPaginadoAsync(
        string clave, int pagina, int tamanoPagina,
        string? territorio = null, string? regional = null, string? area = null,
        string? sexo = null, int? ano = null,
        string? codigoDepartamento = null, string? codigoMunicipio = null,
        CancellationToken ct = default)
    {
        if (!ClavesValidas.Contains(clave, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Vista no reconocida: {clave}", nameof(clave));

        var tam = Math.Clamp(tamanoPagina, 1, 200);
        var p = Math.Max(1, pagina);
        var cacheKey = CacheKey(clave, p, tam, territorio, regional, area, sexo, ano, codigoDepartamento, codigoMunicipio);
        if (cache.TryGetValue(cacheKey, out VistaPoblacionPaginada? cached) && cached is not null)
            return cached;

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        var result = await ConsultarPaginadoDesdeSpAsync(
            con, clave, p, tam, territorio, regional, area, sexo, ano, codigoDepartamento, codigoMunicipio, ct);

        cache.Set(cacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
        return result;
    }

    private static async Task<VistaPoblacionPaginada> ConsultarPaginadoDesdeSpAsync(
        SqlConnection con, string clave, int pagina, int tamanoPagina,
        string? territorio, string? regional, string? area, string? sexo, int? ano,
        string? codigoDepartamento, string? codigoMunicipio, CancellationToken ct)
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
                    fila[columnas[i]] = reader.IsDBNull(i) ? null : NormalizarCelda(reader.GetValue(i));
                filas.Add(fila);
            }
        }

        return new VistaPoblacionPaginada(clave, p, tam, totalFilas, totalPaginas, columnas, filas);
    }

    private static string CacheKey(
        string clave, int pagina, int tamanoPagina,
        string? territorio, string? regional, string? area, string? sexo, int? ano,
        string? codigoDepartamento, string? codigoMunicipio)
    {
        static string N(string? s) => (s ?? "").Trim().ToUpperInvariant();
        return string.Join('|', "proy-v2", clave.Trim().ToLowerInvariant(), pagina, tamanoPagina,
            N(territorio), N(regional), N(area), N(sexo),
            ano?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            N(codigoDepartamento), N(codigoMunicipio));
    }

    private static object? NormalizarCelda(object valor) => valor switch
    {
        DateTime dt => dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt,
        _ => valor
    };
}

/// <summary>Resultado paginado de una vista de proyección de población.</summary>
public sealed record VistaPoblacionPaginada(
    string Clave, int Pagina, int TamanoPagina, long TotalFilas, int TotalPaginas,
    IReadOnlyList<string> Columnas, IReadOnlyList<Dictionary<string, object?>> Filas);
