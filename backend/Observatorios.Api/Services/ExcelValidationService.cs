using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

/// <summary>
/// Valida libros Excel con hojas <c>Diccionario_Datos</c> y <c>Datos</c>.
/// </summary>
public sealed class ExcelValidationService
{
    private static readonly string[] ColumnasDiccionario =
    [
        "Nombre_Campo", "Tipo_Dato", "Obligatorio", "Descripcion",
        "Longitud", "Formato", "Valores_Permitidos"
    ];

    public ExcelValidationResult Validar(Stream excelStream)
    {
        var errores = new List<ValidationErrorDto>();
        using var wb = new XLWorkbook(excelStream);

        var hojaDict = wb.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Diccionario_Datos", StringComparison.OrdinalIgnoreCase));
        var hojaDatos = wb.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Datos", StringComparison.OrdinalIgnoreCase));

        if (hojaDict is null)
            errores.Add(new ValidationErrorDto(null, null, "Falta la hoja obligatoria «Diccionario_Datos».", "ESTRUCTURA"));
        if (hojaDatos is null)
            errores.Add(new ValidationErrorDto(null, null, "Falta la hoja obligatoria «Datos».", "ESTRUCTURA"));

        if (hojaDict is null || hojaDatos is null)
            return new ExcelValidationResult([], [], false, errores);

        var dictRange = hojaDict.RangeUsed();
        if (dictRange is null)
        {
            errores.Add(new ValidationErrorDto(null, "Diccionario_Datos", "La hoja Diccionario_Datos está vacía.", "ESTRUCTURA"));
            return new ExcelValidationResult([], [], false, errores);
        }

        var headerMap = LeerEncabezados(dictRange.FirstRow(), errores);
        if (errores.Count > 0)
            return new ExcelValidationResult([], [], false, errores);

        var campos = LeerCamposDiccionario(dictRange, headerMap, errores);
        if (campos.Count == 0 && errores.Count == 0)
            errores.Add(new ValidationErrorDto(null, "Diccionario_Datos", "No hay filas de definición de campos.", "ESTRUCTURA"));

        ValidarCoherenciaDiccionario(campos, errores);

        var datosRange = hojaDatos.RangeUsed();
        var filasDatos = new List<DatosFilaDto>();
        if (datosRange is null)
        {
            errores.Add(new ValidationErrorDto(null, "Datos", "La hoja Datos está vacía.", "ESTRUCTURA"));
            return new ExcelValidationResult(campos, filasDatos, false, errores);
        }

        var datosHeaderMap = LeerEncabezadosDatos(datosRange.FirstRow(), errores);
        if (datosHeaderMap.Count == 0 && errores.Count > 0)
            return new ExcelValidationResult(campos, filasDatos, false, errores);

        foreach (var campo in campos)
        {
            if (!datosHeaderMap.ContainsKey(Normalizar(campo.NombreCampo)))
            {
                errores.Add(new ValidationErrorDto(
                    null, campo.NombreCampo,
                    $"La columna «{campo.NombreCampo}» definida en el diccionario no existe en la hoja Datos.",
                    "COLUMNA"));
            }
        }

        var lastRow = datosRange.LastRow().RowNumber();
        for (var rowNum = datosRange.FirstRow().RowNumber() + 1; rowNum <= lastRow; rowNum++)
        {
            var row = hojaDatos.Row(rowNum);
            if (FilaVacia(row)) continue;

            var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in datosHeaderMap)
            {
                var cell = row.Cell(kv.Value);
                valores[kv.Key] = cell.IsEmpty() ? null : cell.GetFormattedString().Trim();
            }

            foreach (var campo in campos)
            {
                var key = Normalizar(campo.NombreCampo);
                valores.TryGetValue(key, out var valor);
                ValidarValorCelda(campo, valor, rowNum, errores);
            }

            filasDatos.Add(new DatosFilaDto(rowNum, JsonSerializer.Serialize(valores)));
        }

        if (filasDatos.Count == 0 && campos.Any(c => c.Obligatorio))
            errores.Add(new ValidationErrorDto(null, "Datos", "No hay filas de datos para validar.", "ESTRUCTURA"));

        return new ExcelValidationResult(campos, filasDatos, errores.Count == 0, errores);
    }

    private static Dictionary<string, int> LeerEncabezados(
        IXLRangeRow headerRow,
        List<ValidationErrorDto> errores,
        string prefijo = "Diccionario_Datos")
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var encontradas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = Normalizar(cell.GetString());
            if (string.IsNullOrEmpty(name)) continue;
            map[name] = cell.Address.ColumnNumber;
            encontradas.Add(name);
        }

        foreach (var col in ColumnasDiccionario)
        {
            if (!encontradas.Contains(Normalizar(col)))
            {
                errores.Add(new ValidationErrorDto(
                    null, col,
                    $"Falta la columna «{col}» en la hoja {prefijo}.",
                    "ESTRUCTURA"));
            }
        }

        return map;
    }

    /// <summary>Encabezados de la hoja Datos (nombres de campo, no columnas del meta-diccionario).</summary>
    private static Dictionary<string, int> LeerEncabezadosDatos(
        IXLRangeRow headerRow,
        List<ValidationErrorDto> errores)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = Normalizar(cell.GetString());
            if (string.IsNullOrEmpty(name)) continue;
            map[name] = cell.Address.ColumnNumber;
        }

        if (map.Count == 0)
            errores.Add(new ValidationErrorDto(null, "Datos", "La hoja Datos no tiene encabezados de columna.", "ESTRUCTURA"));

        return map;
    }

    private static List<CampoDiccionarioDto> LeerCamposDiccionario(
        IXLRange range,
        IReadOnlyDictionary<string, int> headers,
        List<ValidationErrorDto> errores)
    {
        var campos = new List<CampoDiccionarioDto>();
        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var firstDataRow = range.FirstRow().RowNumber() + 1;
        var lastRow = range.LastRow().RowNumber();

        for (var r = firstDataRow; r <= lastRow; r++)
        {
            var row = range.Worksheet.Row(r);
            if (FilaVacia(row)) continue;

            string Leer(string col)
            {
                if (!headers.TryGetValue(Normalizar(col), out var c)) return "";
                return row.Cell(c).GetFormattedString().Trim();
            }

            var nombre = Leer("Nombre_Campo");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                errores.Add(new ValidationErrorDto(r, "Nombre_Campo", "Nombre_Campo es obligatorio en el diccionario.", "DICCIONARIO"));
                continue;
            }

            if (!nombres.Add(nombre))
            {
                errores.Add(new ValidationErrorDto(r, "Nombre_Campo", $"Campo duplicado «{nombre}».", "DICCIONARIO"));
                continue;
            }

            var tipo = Leer("Tipo_Dato").ToLowerInvariant();
            if (!TiposValidos.Contains(tipo))
            {
                errores.Add(new ValidationErrorDto(r, "Tipo_Dato",
                    $"Tipo «{tipo}» no válido. Use: texto, entero, decimal, fecha, booleano.", "DICCIONARIO"));
                tipo = "texto";
            }

            var oblRaw = Leer("Obligatorio");
            var obligatorio = EsObligatorio(oblRaw);
            if (string.IsNullOrWhiteSpace(oblRaw))
                errores.Add(new ValidationErrorDto(r, "Obligatorio", "Indique S o N en Obligatorio.", "DICCIONARIO"));

            int? longitud = null;
            var longStr = Leer("Longitud");
            if (!string.IsNullOrWhiteSpace(longStr))
            {
                if (!int.TryParse(longStr, out var len) || len < 0)
                    errores.Add(new ValidationErrorDto(r, "Longitud", "Longitud debe ser un entero ≥ 0.", "DICCIONARIO"));
                else longitud = len;
            }

            campos.Add(new CampoDiccionarioDto(
                nombre,
                tipo,
                obligatorio,
                NullIfEmpty(Leer("Descripcion")),
                longitud,
                NullIfEmpty(Leer("Formato")),
                NullIfEmpty(Leer("Valores_Permitidos")),
                campos.Count));
        }

        return campos;
    }

    private static void ValidarCoherenciaDiccionario(List<CampoDiccionarioDto> campos, List<ValidationErrorDto> errores)
    {
        if (campos.Count == 0)
            errores.Add(new ValidationErrorDto(null, "Diccionario_Datos", "El diccionario no define ningún campo.", "ESTRUCTURA"));
    }

    private static void ValidarValorCelda(
        CampoDiccionarioDto campo,
        string? valor,
        int fila,
        List<ValidationErrorDto> errores)
    {
        var vacio = string.IsNullOrWhiteSpace(valor);
        if (campo.Obligatorio && vacio)
        {
            errores.Add(new ValidationErrorDto(fila, campo.NombreCampo, "Campo obligatorio vacío.", "OBLIGATORIO"));
            return;
        }

        if (vacio) return;

        switch (campo.TipoDato)
        {
            case "texto":
                if (campo.Longitud.HasValue && valor!.Length > campo.Longitud.Value)
                    errores.Add(new ValidationErrorDto(fila, campo.NombreCampo,
                        $"Longitud máxima {campo.Longitud} (actual: {valor.Length}).", "LONGITUD"));
                break;
            case "entero":
                if (!long.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    errores.Add(new ValidationErrorDto(fila, campo.NombreCampo, "Debe ser un número entero.", "TIPO"));
                break;
            case "decimal":
                if (!decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                    && !decimal.TryParse(valor, NumberStyles.Number, CultureInfo.CurrentCulture, out _))
                    errores.Add(new ValidationErrorDto(fila, campo.NombreCampo, "Debe ser un número decimal.", "TIPO"));
                break;
            case "fecha":
                if (!TryParseFecha(valor!, campo.Formato, out _))
                    errores.Add(new ValidationErrorDto(fila, campo.NombreCampo,
                        $"Fecha no válida{(string.IsNullOrWhiteSpace(campo.Formato) ? "" : $" (formato: {campo.Formato})")}.", "FORMATO"));
                break;
            case "booleano":
                if (!EsBooleano(valor!))
                    errores.Add(new ValidationErrorDto(fila, campo.NombreCampo, "Use S/N, SI/NO, true/false o 1/0.", "TIPO"));
                break;
        }

        if (!string.IsNullOrWhiteSpace(campo.ValoresPermitidos))
        {
            var permitidos = campo.ValoresPermitidos
                .Split(';', ',', '|')
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (permitidos.Count > 0 && !permitidos.Contains(valor!))
                errores.Add(new ValidationErrorDto(fila, campo.NombreCampo,
                    $"Valor «{valor}» no está en valores permitidos: {campo.ValoresPermitidos}.", "DOMINIO"));
        }

        if (!string.IsNullOrWhiteSpace(campo.Formato) && campo.TipoDato == "texto")
        {
            try
            {
                if (!Regex.IsMatch(valor!, campo.Formato))
                    errores.Add(new ValidationErrorDto(fila, campo.NombreCampo,
                        "No cumple el patrón (regex) definido en Formato.", "FORMATO"));
            }
            catch
            {
                /* formato regex inválido en diccionario: se ignora en fila */
            }
        }
    }

    private static readonly HashSet<string> TiposValidos =
        ["texto", "entero", "decimal", "fecha", "booleano"];

    private static bool EsObligatorio(string raw)
    {
        var v = raw.Trim().ToUpperInvariant();
        return v is "S" or "SI" or "YES" or "Y" or "TRUE" or "1";
    }

    private static bool EsBooleano(string v)
    {
        var u = v.Trim().ToUpperInvariant();
        return u is "S" or "N" or "SI" or "NO" or "TRUE" or "FALSE" or "1" or "0";
    }

    private static bool TryParseFecha(string valor, string? formato, out DateTime dt)
    {
        if (!string.IsNullOrWhiteSpace(formato))
        {
            var fmt = formato.Replace("YYYY", "yyyy").Replace("DD", "dd").Replace("MM", "MM");
            if (DateTime.TryParseExact(valor, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return true;
        }

        return DateTime.TryParse(valor, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt)
            || DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
    }

    private static bool FilaVacia(IXLRow row) =>
        !row.CellsUsed().Any(c => !c.IsEmpty() && !string.IsNullOrWhiteSpace(c.GetFormattedString()));

    private static string Normalizar(string? s) =>
        (s ?? "").Trim().Replace(" ", "_");

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed record CampoDiccionarioDto(
    string NombreCampo,
    string TipoDato,
    bool Obligatorio,
    string? Descripcion,
    int? Longitud,
    string? Formato,
    string? ValoresPermitidos,
    int Orden);

public sealed record DatosFilaDto(int NumeroFila, string DatosJson);

public sealed record ValidationErrorDto(
    int? NumeroFila,
    string? NombreColumna,
    string Mensaje,
    string TipoError);

public sealed record ExcelValidationResult(
    IReadOnlyList<CampoDiccionarioDto> Campos,
    IReadOnlyList<DatosFilaDto> Filas,
    bool EsValido,
    IReadOnlyList<ValidationErrorDto> Errores);
