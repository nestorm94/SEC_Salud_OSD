using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

/// <summary>
/// Lee el diccionario del formato OSC V.2 (Secretaría de Salud).
/// Plantilla oficial: metadatos filas 1-4, encabezados fila 5, datos desde fila 6.
/// </summary>
public static class DiccionarioOscV2Reader
{
    /// <summary>Columnas fijas plantilla OSC V.2 (fila 5).</summary>
    private static readonly Dictionary<string, int> ColumnasFijasOsc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id_row"] = 1,
        ["nombre_campo"] = 2,
        ["descripcion"] = 3,
        ["llave_primaria"] = 4,
        ["llave_foranea"] = 5,
        ["obligatorio"] = 6,
        ["id_variable"] = 7,
        ["tipo_dato"] = 8,
        ["longitud"] = 9,
        ["dominios"] = 10,
        ["unidad_medida"] = 11,
        ["campo_calculado"] = 12,
        ["formula"] = 13
    };

    /// <summary>Fila estándar de encabezados en plantilla OSC V.2.</summary>
    public const int FilaEncabezadosDefault = 5;
    /// <summary>Primera fila de definición de variables en el diccionario.</summary>
    public const int FilaDatosInicioDefault = 6;

    /// <summary>Detecta si la hoja corresponde al formato OSC (Diccionario_datos).</summary>
    public static bool EsPlantillaOsc(IXLWorksheet hoja)
    {
        if (!hoja.Name.Contains("Diccionario", StringComparison.OrdinalIgnoreCase))
            return false;

        var fila = BuscarFilaEncabezados(hoja);
        if (fila > 0) return true;

        var b5 = ObtenerTextoCelda(hoja, FilaEncabezadosDefault, 2);
        return b5.Contains("nombre", StringComparison.OrdinalIgnoreCase)
            && b5.Contains("variable", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Localiza la fila de encabezados si difiere del valor por defecto.</summary>
    public static int BuscarFilaEncabezados(IXLWorksheet hoja, int maxFilas = 30)
    {
        for (var r = 1; r <= maxFilas; r++)
        {
            var b = ObtenerTextoCelda(hoja, r, 2);
            if (b.Contains("nombre", StringComparison.OrdinalIgnoreCase)
                && b.Contains("variable", StringComparison.OrdinalIgnoreCase))
                return r;
        }
        return -1;
    }

    /// <summary>Resuelve índices de columnas del diccionario según encabezados detectados.</summary>
    public static Dictionary<string, int> ResolverColumnas(IXLWorksheet hoja, int filaEncabezado)
    {
        var dinamico = MapearEncabezadosMultifila(hoja, filaEncabezado);
        CompletarAliasColumnas(dinamico);

        if (dinamico.ContainsKey("nombre_campo") && dinamico.ContainsKey("tipo_dato"))
            return dinamico;

        return new Dictionary<string, int>(ColumnasFijasOsc, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Combina encabezados de la fila detectada y filas adyacentes (plantilla con títulos en dos filas).</summary>
    public static Dictionary<string, int> MapearEncabezadosMultifila(IXLWorksheet hoja, int filaEncabezado)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var first = Math.Max(1, filaEncabezado - 2);
        var last = Math.Min(hoja.LastRowUsed()?.RowNumber() ?? filaEncabezado, filaEncabezado + 1);
        for (var r = first; r <= last; r++)
        {
            foreach (var kv in MapearEncabezados(hoja.Row(r)))
            {
                if (!map.ContainsKey(kv.Key))
                    map[kv.Key] = kv.Value;
            }
        }
        return map;
    }

    /// <summary>Mapea encabezados de una fila a nombres canónicos de campos OSC.</summary>
    public static Dictionary<string, int> MapearEncabezados(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCol = Math.Max(headerRow.LastCellUsed()?.Address.ColumnNumber ?? 20, 20);
        for (var c = 1; c <= lastCol; c++)
        {
            var texto = ObtenerTextoCelda(headerRow.Worksheet, headerRow.RowNumber(), c);
            var canon = CanonizarColumna(texto);
            if (string.IsNullOrEmpty(canon)) continue;
            map[canon] = c;
        }
        return map;
    }

    /// <summary>Alias cuando el título en Excel no coincide exactamente con el nombre canónico esperado.</summary>
    private static void CompletarAliasColumnas(Dictionary<string, int> map)
    {
        AsignarAlias(map, "descripcion", k => k.Contains("descrip"));
        AsignarAlias(map, "nombre_campo", k => k.Contains("nombre") && k.Contains("variable"));
        AsignarAlias(map, "tipo_dato", k => k.Contains("tipo") && k.Contains("dato"));
        AsignarAlias(map, "dominios", k => k.Contains("dominio") || k.Contains("categoria"));
        AsignarAlias(map, "id_row", k => k is "id" or "id_row");
    }

    private static void AsignarAlias(
        Dictionary<string, int> map,
        string canon,
        Func<string, bool> coincide)
    {
        if (map.ContainsKey(canon)) return;
        foreach (var kv in map)
        {
            if (coincide(kv.Key))
            {
                map[canon] = kv.Value;
                return;
            }
        }
    }

    /// <summary>Lee definición de campos del diccionario y acumula errores de formato.</summary>
    public static List<CampoDiccionarioDto> LeerCampos(
        IXLWorksheet hoja,
        int filaEncabezado,
        List<ValidationErrorDto> errores)
    {
        var headers = ResolverColumnas(hoja, filaEncabezado);
        var campos = new List<CampoDiccionarioDto>();
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastRow = hoja.LastRowUsed()?.RowNumber() ?? filaEncabezado;

        for (var r = filaEncabezado + 1; r <= lastRow; r++)
        {
            var row = hoja.Row(r);
            if (FilaVacia(row)) continue;

            string Leer(string canon)
            {
                if (!headers.TryGetValue(canon, out var col)) return "";
                return ObtenerTextoCelda(hoja, r, col);
            }

            var nombre = Leer("nombre_campo");
            if (string.IsNullOrWhiteSpace(nombre)) continue;

            if (!nombres.Add(nombre))
            {
                errores.Add(new ValidationErrorDto(r, nombre, $"Variable duplicada «{nombre}».", "DICCIONARIO"));
                continue;
            }

            var tipoRaw = Leer("tipo_dato");
            var tipo = MapearTipoDato(tipoRaw, Leer("longitud"), out _);

            var oblRaw = Leer("obligatorio");
            var obligatorio = EsObligatorio(oblRaw);

            var longitud = ParseLongitud(Leer("longitud"), out var lenErr);
            if (lenErr is not null)
                errores.Add(new ValidationErrorDto(r, nombre, lenErr, "DICCIONARIO"));

            var dominios = NullIfEmpty(Leer("dominios"));
            var llaveForanea = Leer("llave_foranea");
            var (tablaRef, campoRef) = InferirReferencia(llaveForanea, dominios, nombre);

            var descripcion = ConstruirDescripcion(
                NullIfEmpty(Leer("descripcion")),
                Leer("id_variable"),
                Leer("llave_primaria"),
                llaveForanea,
                NullIfEmpty(Leer("unidad_medida")),
                Leer("campo_calculado"),
                NullIfEmpty(Leer("formula")));

            campos.Add(new CampoDiccionarioDto(
                nombre, tipo, obligatorio,
                descripcion, longitud,
                NullIfEmpty(Leer("formula")),
                dominios,
                tablaRef, campoRef,
                campos.Count));
        }

        return campos;
    }

    /// <summary>Verifica presencia de columnas mínimas exigidas por la plantilla OSC.</summary>
    public static void ValidarColumnasMinimas(IReadOnlyDictionary<string, int> headers, List<ValidationErrorDto> errores)
    {
        string[] requeridas = ["nombre_campo", "tipo_dato", "obligatorio"];
        foreach (var col in requeridas)
        {
            if (!headers.ContainsKey(col))
                errores.Add(new ValidationErrorDto(null, col,
                    $"Falta la columna obligatoria del diccionario OSC («{EtiquetaHumana(col)}»).", "ESTRUCTURA"));
        }
    }

    private static string ObtenerTextoCelda(IXLWorksheet hoja, int fila, int col)
    {
        var cell = hoja.Cell(fila, col);
        if (cell.IsEmpty()) return "";

        if (cell.IsMerged())
        {
            var rango = cell.MergedRange();
            if (rango is not null)
                cell = rango.FirstCell();
        }

        var t = cell.GetString()?.Trim();
        if (!string.IsNullOrEmpty(t)) return t;

        t = cell.GetFormattedString();
        if (string.IsNullOrWhiteSpace(t)) t = cell.Value.ToString();
        return (t ?? "").Trim();
    }

    private static string ConstruirDescripcion(
        string? desc, string idVar, string llavePk, string llaveFk,
        string? unidad, string calculado, string? formula)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(desc)) partes.Add(desc.Trim());
        if (!string.IsNullOrWhiteSpace(idVar)) partes.Add($"[Id variable: {idVar}]");
        if (!string.IsNullOrWhiteSpace(llavePk)) partes.Add($"[Llave primaria: {llavePk}]");
        if (!string.IsNullOrWhiteSpace(llaveFk)) partes.Add($"[Llave foránea: {llaveFk}]");
        if (!string.IsNullOrWhiteSpace(unidad)) partes.Add($"[Unidad: {unidad}]");
        if (!string.IsNullOrWhiteSpace(calculado)) partes.Add($"[Calculado: {calculado}]");
        if (!string.IsNullOrWhiteSpace(formula)) partes.Add($"[Fórmula: {formula}]");
        return partes.Count == 0 ? "" : string.Join(" ", partes);
    }

    private static (string? Tabla, string? Campo) InferirReferencia(string llaveFk, string? dominios, string nombre)
    {
        if (!EsObligatorio(llaveFk) && !string.Equals(llaveFk.Trim(), "SI", StringComparison.OrdinalIgnoreCase))
            return (null, null);

        var texto = $"{dominios} {nombre}".ToLowerInvariant();
        if (texto.Contains("divipola") || texto.Contains("municipio"))
            return ("dim_municipios", "codigo_municipio");
        if (texto.Contains("departamento"))
            return ("dim_departamentos", "codigo_departamento");
        return (null, null);
    }

    private static string MapearTipoDato(string raw, string longitud, out string? advertencia)
    {
        advertencia = null;
        var t = raw.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(t))
        {
            advertencia = "Tipo de datos vacío; se asume texto.";
            return "texto";
        }

        if (t.Contains("fecha")) return "fecha";
        if (t.Contains("bool") || t.Contains("logico") || t.Contains("lógico")) return "booleano";
        if (t.Contains("num") || t.Contains("entero") || t.Contains("decimal") || t.Contains("int"))
        {
            if (longitud.Contains(',') || longitud.Contains("p(", StringComparison.OrdinalIgnoreCase))
                return "decimal";
            return "entero";
        }
        if (t.Contains("car") || t.Contains("text") || t.Contains("alfan") || t.Contains("cadena"))
            return "texto";

        advertencia = $"Tipo «{raw}» no reconocido; se asume texto.";
        return "texto";
    }

    private static int? ParseLongitud(string raw, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var m = Regex.Match(raw, @"p\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
        if (m.Success)
            return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0)
            return n;
        error = $"Longitud «{raw}» no interpretable; use número o p(10,2).";
        return null;
    }

    private static string CanonizarColumna(string? header)
    {
        var n = Normalizar(header);
        if (string.IsNullOrEmpty(n)) return "";

        if (n is "id" or "id_") return "id_row";
        if ((n.Contains("nombre") && n.Contains("variable")) || n is "nombre_de_la_variable")
            return "nombre_campo";
        if ((n.Contains("tipo") && n.Contains("dato")) || n is "tipo_de_datos")
            return "tipo_dato";
        if (n.Contains("descrip") || (n.Contains("definicion") && n.Contains("variable")))
            return "descripcion";
        if (n.Contains("llave") && n.Contains("primaria")) return "llave_primaria";
        if (n.Contains("llave") && n.Contains("foranea")) return "llave_foranea";
        if (n.Contains("obligatorio") || n.Contains("campo_obligatorio")) return "obligatorio";
        if (n.Contains("id") && n.Contains("variable")) return "id_variable";
        if (n is "longitud") return "longitud";
        if (n.Contains("dominio") || n.Contains("categoria")) return "dominios";
        if (n.Contains("unidad") && n.Contains("medida")) return "unidad_medida";
        if (n.Contains("calculado")) return "campo_calculado";
        if (n.Contains("formula")) return "formula";

        return n;
    }

    private static string EtiquetaHumana(string canon) => canon switch
    {
        "nombre_campo" => "Nombre de la variable",
        "tipo_dato" => "Tipo de datos",
        "obligatorio" => "Campo obligatorio",
        _ => canon
    };

    /// <summary>Normaliza nombres de variable para comparación entre diccionario y DATA.</summary>
    public static string Normalizar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Trim()
            .Replace('\u00A0', ' ').Replace('\u2007', ' ')
            .Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
            .Replace("ñ", "n");
        while (t.Contains("  ")) t = t.Replace("  ", " ");
        t = t.Replace(" ", "_").Replace(".", "").Replace("(", "").Replace(")", "");
        return t.ToLowerInvariant();
    }

    private static bool EsObligatorio(string raw)
    {
        var v = raw.Trim().ToUpperInvariant();
        return v is "S" or "SI" or "YES" or "Y" or "TRUE" or "1";
    }

    private static bool FilaVacia(IXLRow row) =>
        !row.CellsUsed().Any(c => !c.IsEmpty() && !string.IsNullOrWhiteSpace(c.GetFormattedString()));

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
