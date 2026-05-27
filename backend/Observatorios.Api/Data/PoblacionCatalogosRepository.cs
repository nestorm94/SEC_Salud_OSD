using System.Data;
using Microsoft.Data.SqlClient;
using Observatorios.Api.Models;

namespace Observatorios.Api.Data;

/// <summary>
/// Catálogos dinámicos para filtros de proyección de población.
/// Mantiene códigos DANE/DIVIPOLA como texto para preservar ceros a la izquierda.
/// </summary>
public sealed class PoblacionCatalogosRepository(IConfiguration config)
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default en appsettings.json");

    private static readonly string[] VistasPoblacion =
    [
        "dbo.vw_Poblacion_Nacional_Casanare",
        "dbo.vw_Reporte_Poblacion_CursoVida_Unificado",
        "dbo.vw_Reporte_Poblacion_Quinquenios_Unificado",
    ];

    private static string? _vistaPoblacionCache;

    /// <summary>Una sola ida al servidor: tablas dim (rápido) + valores de vista en paralelo.</summary>
    public async Task<CatalogosProyeccionDto> ObtenerCatalogosProyeccionAsync(CancellationToken ct = default)
    {
        var deptTask = ObtenerDepartamentosAsync(ct);
        var regTask = ObtenerRegionalesAsync(ct);
        var vistaTask = ObtenerFiltrosDesdeVistaAsync(ct);

        await Task.WhenAll(deptTask, regTask, vistaTask);

        var (areas, sexos, anios) = await vistaTask;
        return new CatalogosProyeccionDto(
            await deptTask,
            await regTask,
            areas,
            sexos,
            anios);
    }

    public async Task<IReadOnlyList<DepartamentoDto>> ObtenerDepartamentosAsync(CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Catalogo_Departamentos_Listar", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Catalogo_Departamentos_Listar", con)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 30
            };
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var list = new List<DepartamentoDto>();
            while (await r.ReadAsync(ct))
                list.Add(new DepartamentoDto(r.GetString(0), r.GetString(1)));
            return list;
        }

        var tabla = await ResolverTablaDepartamentoAsync(con, ct);
        if (tabla is null) return Array.Empty<DepartamentoDto>();

        var tieneEstado = await DimCatalogSql.ColumnaExisteAsync(con, tabla, "estado", ct);
        var filtroEstado = tieneEstado ? "WHERE (estado = 1 OR estado IS NULL)" : "";

        var sql = tabla.EndsWith("dim_departamento", StringComparison.OrdinalIgnoreCase)
            ? $"""
SELECT LTRIM(RTRIM(CAST(cod_departamento AS nvarchar(10)))) AS CodigoDane,
       LTRIM(RTRIM(CAST(nombre_departamento AS nvarchar(300)))) AS Nombre
FROM {tabla}
{filtroEstado}
ORDER BY 2;
"""
            : $"""
SELECT LTRIM(RTRIM(CAST(codigo_departamento AS nvarchar(10)))) AS CodigoDane,
       LTRIM(RTRIM(CAST(nombre_departamento AS nvarchar(300)))) AS Nombre
FROM {tabla}
ORDER BY 2;
""";

        return await LeerDepartamentosAsync(con, sql, ct);
    }

    public async Task<IReadOnlyList<MunicipioDto>> ObtenerMunicipiosAsync(
        string? codigoDepartamento = null,
        CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Catalogo_Municipios_Listar", ct))
        {
            await using var cmdSp = new SqlCommand("dbo.usp_Catalogo_Municipios_Listar", con)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 30
            };
            cmdSp.Parameters.AddWithValue("@CodigoDepartamento", (object?)codigoDepartamento ?? DBNull.Value);
            await using var readerSp = await cmdSp.ExecuteReaderAsync(ct);
            var listSp = new List<MunicipioDto>();
            while (await readerSp.ReadAsync(ct))
                listSp.Add(new MunicipioDto(readerSp.GetString(0), readerSp.GetString(1), readerSp.GetString(2), readerSp.GetString(3)));
            return listSp;
        }

        var tabla = await ResolverTablaMunicipioAsync(con, ct);
        if (tabla is null) return Array.Empty<MunicipioDto>();

        var porDep = !string.IsNullOrWhiteSpace(codigoDepartamento);

        var colCodigo = await DimCatalogSql.ColumnaCodigoMunicipioAsync(con, tabla, ct);
        if (colCodigo is null) return Array.Empty<MunicipioDto>();

        var colDep = await DimCatalogSql.ColumnaDepartamentoMunicipioAsync(con, tabla, ct);
        if (colDep is null) return Array.Empty<MunicipioDto>();
        var tieneEstado = await DimCatalogSql.ColumnaExisteAsync(con, tabla, "estado", ct);
        var tieneRegional = await DimCatalogSql.ColumnaExisteAsync(con, tabla, "regional", ct);

        var regionalExpr = tieneRegional
            ? "LTRIM(RTRIM(CAST(ISNULL(regional, '') AS nvarchar(200))))"
            : "CAST(N'' AS nvarchar(200))";

        var where = new List<string>();
        if (tieneEstado) where.Add("(estado = 1 OR estado IS NULL)");
        if (porDep) where.Add($"LTRIM(RTRIM(CAST({colDep} AS nvarchar(10)))) = @dep");
        var whereSql = where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where);

        var sql = $"""
SELECT LTRIM(RTRIM(CAST({colCodigo} AS nvarchar(10)))) AS CodigoDaneMunicipio,
       LTRIM(RTRIM(CAST({colDep} AS nvarchar(10)))) AS CodigoDepartamento,
       LTRIM(RTRIM(CAST(nombre_municipio AS nvarchar(300)))) AS NombreMunicipio,
       {regionalExpr} AS Regional
FROM {tabla}
{whereSql}
ORDER BY 3;
""";

        await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 30 };
        if (porDep)
            cmd.Parameters.Add("@dep", SqlDbType.NVarChar, 10).Value = codigoDepartamento!.Trim();

        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<MunicipioDto>();
        while (await r.ReadAsync(ct))
            list.Add(new MunicipioDto(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return list;
    }

    public async Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerRegionalesAsync(CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Catalogo_Regionales_Listar", ct))
            return await EjecutarCatalogoSimpleSpAsync(con, "dbo.usp_Catalogo_Regionales_Listar", ct);

        var tabla = await ResolverTablaMunicipioAsync(con, ct);
        if (tabla is not null && await DimCatalogSql.ColumnaExisteAsync(con, tabla, "regional", ct))
        {
            var sql = $"""
SELECT DISTINCT LTRIM(RTRIM(CAST(regional AS nvarchar(200)))) AS Regional
FROM {tabla}
WHERE ISNULL(LTRIM(RTRIM(CAST(regional AS nvarchar(200)))), N'') <> N''
ORDER BY 1;
""";

            await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 30 };
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await LeerCatalogoSimpleAsync(r, ct);
        }

        // Fallback: si no hay columna regional en dim_*, usar la vista de población.
        return await DistinctDesdeVistasAsync("[Regional]", ct);
    }

    public async Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAreasAsync(CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Catalogo_Areas_Listar", ct))
            return await EjecutarCatalogoSimpleSpAsync(con, "dbo.usp_Catalogo_Areas_Listar", ct);
        return await DistinctDesdeVistasAsync("[Área]", ct);
    }

    public async Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerSexosAsync(CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Catalogo_Sexos_Listar", ct))
            return await EjecutarCatalogoSimpleSpAsync(con, "dbo.usp_Catalogo_Sexos_Listar", ct);
        return await DistinctDesdeVistasAsync("[Sexo]", ct);
    }

    public async Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAniosAsync(CancellationToken ct = default)
    {
        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        if (await SqlProcHelper.StoredProcedureExisteAsync(con, "dbo", "usp_Catalogo_Anios_Listar", ct))
        {
            await using var cmd = new SqlCommand("dbo.usp_Catalogo_Anios_Listar", con)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 60
            };
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var list = new List<CatalogoSimpleDto>();
            while (await r.ReadAsync(ct))
            {
                var v = r.GetString(0);
                list.Add(new CatalogoSimpleDto(v, v));
            }
            return list;
        }
        return await ObtenerAniosDesdeVistaAsync(ct);
    }

    private async Task<(IReadOnlyList<CatalogoSimpleDto> Areas, IReadOnlyList<CatalogoSimpleDto> Sexos, IReadOnlyList<CatalogoSimpleDto> Anios)>
        ObtenerFiltrosDesdeVistaAsync(CancellationToken ct)
    {
        var areasTask = DistinctDesdeVistasAsync("[Área]", ct);
        var sexosTask = DistinctDesdeVistasAsync("[Sexo]", ct);
        var aniosTask = ObtenerAniosDesdeVistaAsync(ct);
        await Task.WhenAll(areasTask, sexosTask, aniosTask);
        return (await areasTask, await sexosTask, await aniosTask);
    }

    private async Task<IReadOnlyList<CatalogoSimpleDto>> ObtenerAniosDesdeVistaAsync(CancellationToken ct)
    {
        var vista = await ResolverVistaPoblacionAsync(ct);
        if (vista is null) return Array.Empty<CatalogoSimpleDto>();

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        // Una agregación MIN/MAX es mucho más rápida que DISTINCT sobre ~11k filas.
        var sql = $"""
SELECT MIN(TRY_CONVERT(int, [Año])) AS MinAnio, MAX(TRY_CONVERT(int, [Año])) AS MaxAnio
FROM {vista} WITH (NOLOCK)
WHERE [Año] IS NOT NULL;
""";

        await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return Array.Empty<CatalogoSimpleDto>();

        var min = r.IsDBNull(0) ? (int?)null : r.GetInt32(0);
        var max = r.IsDBNull(1) ? (int?)null : r.GetInt32(1);
        if (min is null || max is null || max < min) return Array.Empty<CatalogoSimpleDto>();

        var list = new List<CatalogoSimpleDto>(max.Value - min.Value + 1);
        for (var y = max.Value; y >= min.Value; y--)
        {
            var s = y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            list.Add(new CatalogoSimpleDto(s, s));
        }
        return list;
    }

    private async Task<IReadOnlyList<CatalogoSimpleDto>> DistinctDesdeVistasAsync(
        string columnaSql,
        CancellationToken ct)
    {
        var vista = await ResolverVistaPoblacionAsync(ct);
        if (vista is null) return Array.Empty<CatalogoSimpleDto>();

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);

        var expr = $"LTRIM(RTRIM(CAST({columnaSql} AS nvarchar(200))))";
        var sql = $"""
SELECT DISTINCT {expr} AS Valor
FROM {vista} WITH (NOLOCK)
WHERE {columnaSql} IS NOT NULL AND {expr} <> N''
ORDER BY 1;
""";

        await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerCatalogoSimpleAsync(r, ct);
    }

    private async Task<string?> ResolverVistaPoblacionAsync(CancellationToken ct)
    {
        if (_vistaPoblacionCache is not null) return _vistaPoblacionCache;

        await using var con = new SqlConnection(_cs);
        await con.OpenAsync(ct);
        foreach (var v in VistasPoblacion)
        {
            if (await TablaExisteAsync(con, v, ct))
            {
                _vistaPoblacionCache = v;
                return v;
            }
        }
        return null;
    }

    private static async Task<IReadOnlyList<DepartamentoDto>> LeerDepartamentosAsync(
        SqlConnection con, string sql, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 30 };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<DepartamentoDto>();
        while (await r.ReadAsync(ct))
            list.Add(new DepartamentoDto(r.GetString(0), r.GetString(1)));
        return list;
    }

    private static async Task<IReadOnlyList<CatalogoSimpleDto>> EjecutarCatalogoSimpleSpAsync(
        SqlConnection con,
        string spName,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand(spName, con)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await LeerCatalogoSimpleAsync(r, ct);
    }

    private static async Task<List<CatalogoSimpleDto>> LeerCatalogoSimpleAsync(SqlDataReader r, CancellationToken ct)
    {
        var list = new List<CatalogoSimpleDto>();
        while (await r.ReadAsync(ct))
        {
            var v = r.GetString(0);
            list.Add(new CatalogoSimpleDto(v, v));
        }
        return list;
    }

    private static async Task<string?> ResolverTablaDepartamentoAsync(SqlConnection con, CancellationToken ct) =>
        await TablaExisteAsync(con, "dbo.dim_departamento", ct) ? "dbo.dim_departamento"
        : await TablaExisteAsync(con, "dbo.dim_departamentos", ct) ? "dbo.dim_departamentos"
        : null;

    private static async Task<string?> ResolverTablaMunicipioAsync(SqlConnection con, CancellationToken ct) =>
        await TablaExisteAsync(con, "dbo.dim_municipio", ct) ? "dbo.dim_municipio"
        : await TablaExisteAsync(con, "dbo.dim_municipios", ct) ? "dbo.dim_municipios"
        : null;

    private static Task<bool> TablaExisteAsync(SqlConnection con, string objeto, CancellationToken ct) =>
        DimCatalogSql.TablaExisteAsync(con, objeto, ct);
}
