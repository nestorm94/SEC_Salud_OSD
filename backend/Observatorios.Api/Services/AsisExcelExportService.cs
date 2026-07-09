using ClosedXML.Excel;
using Observatorios.Api.Data;

namespace Observatorios.Api.Services;

/// <summary>Exporta indicadores ASIS al formato Excel DANE para reportes departamentales.</summary>
public sealed class AsisExcelExportService(AsisRepository repo)
{
    private const string DeptCodigo = "85";
    private const string DeptNombre = "Casanare";

    /// <summary>Genera libro Excel de nacimientos por dimensiones (área, sexo, edad, etc.).</summary>
    public async Task<byte[]> ExportNacimientosAsync(int? vigencia, string? codigoMunicipio, CancellationToken ct)
    {
        using var wb = new XLWorkbook();

        await HojaDimensionAsync(wb, "Nacimientos total y área",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Área", "Año", "Nacimientos"],
            "nacimientos-area", vigencia, codigoMunicipio, ct, "nacimientos",
            f => AsisExcelFormat.Area(ValStr(f, "area_normalizada")));

        await HojaDimensionAsync(wb, "Nacimientos por sexo",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Sexo", "Año", "Nacimientos"],
            "nacimientos-sexo", vigencia, codigoMunicipio, ct, "nacimientos",
            f => AsisExcelFormat.Sexo(ValStr(f, "sexo_dim")));

        await HojaDimensionAsync(wb, "Nacimientos por grupos de edad",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Grupos de edad (quinquenios)", "Año", "Nacimientos"],
            "nacimientos-grupo-edad", vigencia, codigoMunicipio, ct, "nacimientos",
            f => ValStr(f, "grupo_edad_madre") ?? "");

        await HojaDimensionAsync(wb, "Nacimientos peso",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Peso en gramos", "Año", "Nacimientos"],
            "nacimientos-peso-al-nacer", vigencia, codigoMunicipio, ct, "nacimientos",
            f => ValStr(f, "peso_al_nacer") ?? "", regionalTotal: true);

        await HojaDimensionAsync(wb, "Nacimientos por escolaridad",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Nivel Educativo", "Año", "Nacimientos"],
            "nacimientos-nivel-educativo", vigencia, codigoMunicipio, ct, "nacimientos",
            f => ValStr(f, "nivel_educativo") ?? "");

        await HojaDimensionAsync(wb, "Nacimientos por EG",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Semanas de Gestación", "Año", "Nacimientos"],
            "nacimientos-semanas-gestacion", vigencia, codigoMunicipio, ct, "nacimientos",
            f => ValStr(f, "semanas_gestacion") ?? "");

        await HojaGeAsync(wb, vigencia, codigoMunicipio, ct);

        HojaSoloEncabezados(wb, "Nacimientos tipo de parto",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Tipo de parto", "Año", "Nacimientos"]);
        HojaSoloEncabezados(wb, "Nacimientos sitio parto",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Sitio de parto", "Año", "Nacimientos"]);

        EscribirFuentes(wb.Worksheets.Add("Fuentes"),
            "fact_nacimientos_casanare_normalizada");

        return Guardar(wb);
    }

    /// <summary>Genera libro Excel de defunciones por dimensiones demográficas.</summary>
    public async Task<byte[]> ExportMortalidadAsync(int? vigencia, string? codigoMunicipio, CancellationToken ct)
    {
        using var wb = new XLWorkbook();

        await HojaDimensionAsync(wb, "Sexo",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Sexo", "Año", "Defunciones"],
            "mortalidad-sexo", vigencia, codigoMunicipio, ct, "defunciones",
            f => AsisExcelFormat.Sexo(ValStr(f, "sexo_dim")));

        await HojaCursoVidaAsync(wb, vigencia, codigoMunicipio, ct);

        await HojaDimensionAsync(wb, "Quinquenio",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Grupos de edad (quinquenios)", "Año", "Defunciones"],
            "mortalidad-grupo-edad", vigencia, codigoMunicipio, ct, "defunciones",
            f => ValStr(f, "etiqueta_rango") ?? "");

        await HojaDimensionAsync(wb, "Area",
            ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Área", "Año", "Defunciones"],
            "mortalidad-area", vigencia, codigoMunicipio, ct, "defunciones",
            f => AsisExcelFormat.Area(ValStr(f, "area_normalizada")));

        EscribirFuentes(wb.Worksheets.Add("Fuentes"),
            "fact_defunciones_casanare_normalizada");

        return Guardar(wb);
    }

    private async Task HojaDimensionAsync(
        XLWorkbook wb, string nombreHoja, string[] headers, string claveAsis,
        int? vigencia, string? codigoMunicipio, CancellationToken ct, string colValor,
        Func<Dictionary<string, object?>, string> dimFn, bool regionalTotal = false)
    {
        var filas = await repo.ConsultarFilasExportacionAsync(claveAsis, vigencia, codigoMunicipio, ct);
        var ws = wb.Worksheets.Add(nombreHoja);
        EscribirEncabezados(ws, headers);

        var agregadas = AgregarPorTerritorio(filas, codigoMunicipio, colValor,
            f => (dimFn(f), ValInt(f, "vigencia")));

        var row = 2;
        foreach (var a in agregadas.OrderBy(x => x.Territorio.Codigo).ThenBy(x => x.Dimension).ThenBy(x => x.Anio))
        {
            var regional = regionalTotal ? "Total" : a.Territorio.Regional;
            EscribirFila(ws, row++, [
                a.Territorio.Codigo, a.Territorio.Nombre, a.Territorio.CodigoTerritorio,
                regional, a.Dimension, a.Anio, a.Valor
            ]);
        }
        ws.Columns().AdjustToContents();
    }

    private async Task HojaGeAsync(XLWorkbook wb, int? vigencia, string? codigoMunicipio, CancellationToken ct)
    {
        var filas = await repo.ConsultarFilasExportacionAsync("nacimientos-detalle", vigencia, codigoMunicipio, ct);
        var ws = wb.Worksheets.Add("Nacimientos por GE");
        EscribirEncabezados(ws, ["Código DANE", "Territorio", "Código-Territorio", "Área", "Pertenencia Étnica", "Año", "Nacimientos"]);

        var agregadas = AgregarPorTerritorio(filas, codigoMunicipio, "nacimientos",
            f => ($"{AsisExcelFormat.Area(ValStr(f, "area_residencia"))}\u001f{ValStr(f, "pertenencia_etnica")}", ValInt(f, "vigencia")));

        var row = 2;
        foreach (var a in agregadas.OrderBy(x => x.Territorio.Codigo).ThenBy(x => x.Dimension).ThenBy(x => x.Anio))
        {
            var parts = a.Dimension.Split('\u001f');
            EscribirFila(ws, row++, [
                a.Territorio.Codigo, a.Territorio.Nombre, a.Territorio.CodigoTerritorio,
                parts[0], parts.Length > 1 ? parts[1] : "", a.Anio, a.Valor
            ]);
        }
        ws.Columns().AdjustToContents();
    }

    private async Task HojaCursoVidaAsync(XLWorkbook wb, int? vigencia, string? codigoMunicipio, CancellationToken ct)
    {
        var filas = await repo.ConsultarFilasExportacionAsync("mortalidad-curso-vida", vigencia, codigoMunicipio, ct);
        var ws = wb.Worksheets.Add("Curso de vida");
        EscribirEncabezados(ws, ["Código DANE", "Territorio", "Código-Territorio", "Regional", "Curso de vida", "Curso de vida2", "Sexo", "Año", "Defunciones"]);

        var agregadas = AgregarPorTerritorio(filas, codigoMunicipio, "defunciones",
            f =>
            {
                var nombre = ValStr(f, "nombre_curso_vida") ?? "";
                var codigo = ValStr(f, "codigo_curso_vida") ?? "";
                return ($"{AsisExcelFormat.CursoVidaPrefijo(codigo, nombre)}\u001f{nombre}", ValInt(f, "vigencia"));
            });

        var row = 2;
        foreach (var a in agregadas.OrderBy(x => x.Territorio.Codigo).ThenBy(x => x.Dimension).ThenBy(x => x.Anio))
        {
            var parts = a.Dimension.Split('\u001f');
            EscribirFila(ws, row++, [
                a.Territorio.Codigo, a.Territorio.Nombre, a.Territorio.CodigoTerritorio,
                a.Territorio.Regional, parts[0], parts.Length > 1 ? parts[1] : "", "Total", a.Anio, a.Valor
            ]);
        }
        ws.Columns().AdjustToContents();
    }

    private static List<RegistroExport> AgregarPorTerritorio(
        IReadOnlyList<Dictionary<string, object?>> filas,
        string? codigoMunicipioFiltro,
        string colValor,
        Func<Dictionary<string, object?>, (string Dimension, int Anio)> keyFn)
    {
        var resultado = new List<RegistroExport>();
        foreach (var t in ObtenerTerritorios(filas, codigoMunicipioFiltro))
        {
            var subset = t.EsDepartamento
                ? filas
                : filas.Where(f => string.Equals(ValStr(f, "codigo_municipio"), t.Codigo, StringComparison.OrdinalIgnoreCase));

            foreach (var g in subset.GroupBy(f => keyFn(f)))
            {
                var total = g.Sum(f => ValLong(f, colValor));
                if (total == 0) continue;
                resultado.Add(new RegistroExport(t, g.Key.Dimension, g.Key.Anio, total));
            }
        }
        return resultado;
    }

    private static List<TerritorioExport> ObtenerTerritorios(
        IReadOnlyList<Dictionary<string, object?>> filas, string? codigoMunicipioFiltro)
    {
        if (!string.IsNullOrWhiteSpace(codigoMunicipioFiltro))
        {
            var cod = codigoMunicipioFiltro.Trim().PadLeft(5, '0');
            var f = filas.FirstOrDefault(x => string.Equals(ValStr(x, "codigo_municipio"), cod, StringComparison.OrdinalIgnoreCase));
            return [TerritorioExport.Municipio(cod, ValStr(f, "nombre_municipio") ?? cod, ValStr(f, "regional"))];
        }

        var lista = new List<TerritorioExport> { TerritorioExport.Departamento() };
        foreach (var g in filas
                     .Where(f => !string.IsNullOrWhiteSpace(ValStr(f, "codigo_municipio")))
                     .GroupBy(f => ValStr(f, "codigo_municipio")!)
                     .OrderBy(g => g.Key))
        {
            var f0 = g.First();
            lista.Add(TerritorioExport.Municipio(g.Key, ValStr(f0, "nombre_municipio") ?? g.Key, ValStr(f0, "regional")));
        }
        return lista;
    }

    private static void HojaSoloEncabezados(XLWorkbook wb, string nombre, string[] headers)
    {
        var ws = wb.Worksheets.Add(nombre);
        EscribirEncabezados(ws, headers);
    }

    private static void EscribirEncabezados(IXLWorksheet ws, string[] headers)
    {
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
    }

    private static void EscribirFila(IXLWorksheet ws, int row, object?[] values)
    {
        for (var c = 0; c < values.Length; c++)
            ws.Cell(row, c + 1).Value = values[c]?.ToString() ?? "";
    }

    private static void EscribirFuentes(IXLWorksheet ws, string fuente)
    {
        ws.Cell(1, 1).Value = $"Fuente: {fuente} — Observatorio Salud Departamental Casanare.";
        ws.Cell(2, 1).Value = $"Exportación generada: {DateTime.Now:dd-MM-yyyy HH:mm}.";
    }

    private static byte[] Guardar(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string? ValStr(Dictionary<string, object?> f, string key) =>
        f.TryGetValue(key, out var v) && v is not null ? v.ToString() : null;

    private static int ValInt(Dictionary<string, object?> f, string key) =>
        f.TryGetValue(key, out var v) && v is not null ? Convert.ToInt32(v) : 0;

    private static long ValLong(Dictionary<string, object?> f, string key) =>
        f.TryGetValue(key, out var v) && v is not null ? Convert.ToInt64(v) : 0;

    private sealed record TerritorioExport(string Codigo, string Nombre, string CodigoTerritorio, string Regional, bool EsDepartamento)
    {
        public static TerritorioExport Departamento() =>
            new(DeptCodigo, DeptNombre, $"{DeptCodigo} - {DeptNombre}", "No aplica", true);

        public static TerritorioExport Municipio(string codigo, string nombre, string? regional) =>
            new(codigo, nombre, $"{codigo} - {nombre}", string.IsNullOrWhiteSpace(regional) ? "No aplica" : regional!, false);
    }

    private sealed record RegistroExport(TerritorioExport Territorio, string Dimension, int Anio, long Valor);
}

internal static class AsisExcelFormat
{
    public static string Sexo(string? sexo) => sexo?.ToUpperInvariant() switch
    {
        "FEMENINO" => "Femenino",
        "MASCULINO" => "Masculino",
        "INDETERMINADO" => "Indeterminado",
        _ => sexo ?? ""
    };

    public static string Area(string? area) => area?.Trim().ToUpperInvariant() switch
    {
        "URBANO" => "1. Urbano",
        "RURAL" => "2. Rural",
        _ => area ?? ""
    };

    public static string CursoVidaPrefijo(string? codigo, string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "";
        if (codigo?.Length >= 4 && codigo.StartsWith("CV", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(codigo[2..], out var n) && n is >= 1 and <= 26)
            return $"{(char)('a' + n - 1)}. {nombre}";
        return nombre;
    }
}
