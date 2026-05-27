using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Observatorios.Api.Services;

public sealed class GeografiaValidacionService(IConfiguration config) : IGeografiaValidacionService
{
    private readonly string _cs = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");

    private readonly Lazy<GeografiaCatalogoContext> _ctx = new(() => CargarCatalogo(config).GetAwaiter().GetResult());

    public GeografiaCatalogoContext ObtenerContexto() => _ctx.Value;

    public bool ValidarCodigoMunicipio(string codigoDane)
    {
        var c = NormalizarCodigo(codigoDane);
        if (string.IsNullOrWhiteSpace(c)) return false;
        return _ctx.Value.MunicipiosPorCodigo.ContainsKey(c);
    }

    public bool ValidarNombreMunicipio(string nombreMunicipio)
    {
        var n = NormalizarTexto(nombreMunicipio);
        if (string.IsNullOrWhiteSpace(n)) return false;
        return _ctx.Value.MunicipiosPorNombreNormalizado.ContainsKey(n);
    }

    public bool ValidarCodigoYNombreMunicipio(string codigoDane, string nombreMunicipio)
    {
        var c = NormalizarCodigo(codigoDane);
        var n = NormalizarTexto(nombreMunicipio);
        if (!_ctx.Value.MunicipiosPorCodigo.TryGetValue(c, out var muni)) return false;
        return string.Equals(NormalizarTexto(muni.NombreMunicipio), n, StringComparison.OrdinalIgnoreCase);
    }

    public bool ValidarCodigoDepartamento(string codigoDepartamento)
    {
        var c = NormalizarCodigo(codigoDepartamento);
        if (string.IsNullOrWhiteSpace(c)) return false;
        return _ctx.Value.DepartamentosPorCodigo.ContainsKey(c);
    }

    public bool ValidarNombreDepartamento(string nombreDepartamento)
    {
        var n = NormalizarTexto(nombreDepartamento);
        if (string.IsNullOrWhiteSpace(n)) return false;
        return _ctx.Value.DepartamentosPorNombreNormalizado.ContainsKey(n);
    }

    public bool ValidarDepartamentoMunicipio(string codigoDepartamento, string codigoMunicipio)
    {
        var dep = NormalizarCodigo(codigoDepartamento);
        var mun = NormalizarCodigo(codigoMunicipio);
        if (!_ctx.Value.MunicipiosPorCodigo.TryGetValue(mun, out var m)) return false;
        return string.Equals(dep, m.CodigoDepartamento, StringComparison.OrdinalIgnoreCase);
    }

    public string NormalizarTexto(string? texto)
    {
        if (texto is null) return string.Empty;
        var t = texto.Trim();
        if (t.Length == 0) return string.Empty;

        t = t.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(t.Length);
        foreach (var ch in t)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.Control or UnicodeCategory.Format)
                continue;
            sb.Append(ch);
        }

        var clean = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        clean = Regex.Replace(clean, @"\s+", " ");
        return clean.Trim();
    }

    private static string NormalizarCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return string.Empty;
        var c = Regex.Replace(codigo.Trim(), @"\s+", "");
        c = c.Replace("-", "");
        if (c.Contains('.'))
            c = c.Split('.')[0];
        return c;
    }

    private static async Task<GeografiaCatalogoContext> CargarCatalogo(IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");
        var ctx = new GeografiaCatalogoContext();
        await using var con = new SqlConnection(cs);
        await con.OpenAsync();

        var existeDep = await ExisteObjeto(con, "dbo.dim_departamento")
                        || await ExisteObjeto(con, "dbo.dim_departamentos");
        var existeMun = await ExisteObjeto(con, "dbo.dim_municipio")
                        || await ExisteObjeto(con, "dbo.dim_municipios");
        if (!existeDep || !existeMun) return ctx;

        var depTabla = await ExisteObjeto(con, "dbo.dim_departamento") ? "dbo.dim_departamento" : "dbo.dim_departamentos";
        var depColCod = depTabla.EndsWith("dim_departamento", StringComparison.OrdinalIgnoreCase) ? "cod_departamento" : "codigo_departamento";
        var depColNom = "nombre_departamento";
        await using (var cmd = new SqlCommand($"SELECT {depColCod}, {depColNom} FROM {depTabla}", con))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                var cod = (r[0]?.ToString() ?? "").Trim();
                var nom = (r[1]?.ToString() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(cod) || string.IsNullOrWhiteSpace(nom)) continue;
                ctx.DepartamentosPorCodigo[cod] = nom;
                var nn = NormalizarTextoEstatico(nom);
                if (!ctx.DepartamentosPorNombreNormalizado.ContainsKey(nn))
                    ctx.DepartamentosPorNombreNormalizado[nn] = cod;
            }
        }

        var munTabla = await ExisteObjeto(con, "dbo.dim_municipio") ? "dbo.dim_municipio" : "dbo.dim_municipios";
        var cols = await Columnas(con, munTabla);
        var munColCod = cols.FirstOrDefault(c => c.Equals("codigo_dane", StringComparison.OrdinalIgnoreCase))
                     ?? cols.FirstOrDefault(c => c.Equals("codigo_municipio", StringComparison.OrdinalIgnoreCase))
                     ?? cols.FirstOrDefault(c => c.Equals("cod_municipio", StringComparison.OrdinalIgnoreCase));
        var munColDep = cols.FirstOrDefault(c => c.Equals("cod_departamento", StringComparison.OrdinalIgnoreCase))
                     ?? cols.FirstOrDefault(c => c.Equals("codigo_departamento", StringComparison.OrdinalIgnoreCase));
        if (munColCod is null || munColDep is null) return ctx;

        await using var cmdM = new SqlCommand(
            $"SELECT {munColCod}, nombre_municipio, {munColDep} FROM {munTabla}", con);
        await using var rm = await cmdM.ExecuteReaderAsync();
        while (await rm.ReadAsync())
        {
            var cod = (rm[0]?.ToString() ?? "").Trim();
            var nom = (rm[1]?.ToString() ?? "").Trim();
            var dep = (rm[2]?.ToString() ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cod) || string.IsNullOrWhiteSpace(nom) || string.IsNullOrWhiteSpace(dep)) continue;
            ctx.DepartamentosPorCodigo.TryGetValue(dep, out var depNom);
            var info = new MunicipioGeoInfo(cod, nom, dep, depNom ?? "");
            ctx.MunicipiosPorCodigo[cod] = info;
            var nn = NormalizarTextoEstatico(nom);
            if (!ctx.MunicipiosPorNombreNormalizado.TryGetValue(nn, out var list))
            {
                list = [];
                ctx.MunicipiosPorNombreNormalizado[nn] = list;
            }
            list.Add(info);
        }

        return ctx;
    }

    private static async Task<bool> ExisteObjeto(SqlConnection con, string name)
    {
        await using var cmd = new SqlCommand("SELECT OBJECT_ID(@n)", con);
        cmd.Parameters.AddWithValue("@n", name);
        return (await cmd.ExecuteScalarAsync()) is not null;
    }

    private static async Task<List<string>> Columnas(SqlConnection con, string obj)
    {
        await using var cmd = new SqlCommand("""
SELECT c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@o)
""", con);
        cmd.Parameters.AddWithValue("@o", obj);
        var list = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    private static string NormalizarTextoEstatico(string? texto)
    {
        if (texto is null) return string.Empty;
        var t = texto.Trim();
        if (t.Length == 0) return string.Empty;
        t = t.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(t.Length);
        foreach (var ch in t)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.Control or UnicodeCategory.Format)
                continue;
            sb.Append(ch);
        }
        var clean = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        clean = Regex.Replace(clean, @"\s+", " ");
        return clean.Trim();
    }
}
