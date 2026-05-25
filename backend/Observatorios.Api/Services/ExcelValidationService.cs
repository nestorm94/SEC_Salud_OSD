using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

/// <summary>
/// Valida libros Excel con hojas <c>Diccionario_Datos</c> y <c>Datos</c>.
/// Soporta formato estándar y <b>OSC V.2</b> (Diccionario_de_datos_OSC).
/// </summary>
public sealed class ExcelValidationService
{
    private static readonly string[] ColumnasDiccionarioLegacy =
    [
        "Nombre_Campo", "Tipo_Dato", "Obligatorio", "Descripcion",
        "Longitud", "Formato", "Valores_Permitidos", "Tabla_Referencia", "Campo_Referencia"
    ];

    public ExcelValidationResult Validar(Stream excelStream, CatalogoValidacionContext? catalogos = null)
    {
        var errores = new List<ValidationErrorDto>();
        using var wb = new XLWorkbook(excelStream);

        var hojaDict = wb.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Diccionario_Datos", StringComparison.OrdinalIgnoreCase))
            ?? wb.Worksheets.FirstOrDefault(w =>
                w.Name.Contains("Diccionario", StringComparison.OrdinalIgnoreCase));

        var hojaDatos = wb.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Datos", StringComparison.OrdinalIgnoreCase));

        if (hojaDict is null)
            errores.Add(new ValidationErrorDto(null, null, "Falta la hoja obligatoria «Diccionario_Datos».", "ESTRUCTURA"));
        if (hojaDatos is null)
            errores.Add(new ValidationErrorDto(null, null, "Falta la hoja obligatoria «Datos».", "ESTRUCTURA"));

        if (hojaDict is null || hojaDatos is null)
            return new ExcelValidationResult([], [], false, errores);

        var esOsc = DiccionarioOscV2Reader.EsPlantillaOsc(hojaDict);
        var filaHeaderOsc = esOsc
            ? DiccionarioOscV2Reader.BuscarFilaEncabezados(hojaDict)
            : -1;
        if (esOsc && filaHeaderOsc <= 0)
            filaHeaderOsc = DiccionarioOscV2Reader.FilaEncabezadosDefault;
        List<CampoDiccionarioDto> campos;

        if (esOsc)
        {
            var headersOsc = DiccionarioOscV2Reader.ResolverColumnas(hojaDict, filaHeaderOsc);
            DiccionarioOscV2Reader.ValidarColumnasMinimas(headersOsc, errores);
            if (errores.Count > 0)
                return new ExcelValidationResult([], [], false, errores);

            campos = DiccionarioOscV2Reader.LeerCampos(hojaDict, filaHeaderOsc, errores);
        }
        else
        {
            var dictRange = hojaDict.RangeUsed();
            if (dictRange is null)
            {
                errores.Add(new ValidationErrorDto(null, "Diccionario_Datos", "La hoja Diccionario_Datos está vacía.", "ESTRUCTURA"));
                return new ExcelValidationResult([], [], false, errores);
            }

            var headerMap = LeerEncabezadosLegacy(dictRange.FirstRow(), errores);
            if (errores.Count > 0)
                return new ExcelValidationResult([], [], false, errores);

            campos = LeerCamposDiccionarioLegacy(dictRange, headerMap, errores);
        }

        if (campos.Count == 0 && errores.Count == 0)
            errores.Add(new ValidationErrorDto(null, "Diccionario_Datos", "No hay filas de definición de campos.", "ESTRUCTURA"));

        ValidarCoherenciaDiccionario(campos, errores);

        var datosRange = hojaDatos.RangeUsed();
        var filasDatos = new List<DatosFilaDto>();
        if (datosRange is null)
        {
            // Plantilla OSC: puede traer solo diccionario; los datos se cargan después.
            return new ExcelValidationResult(campos, filasDatos, errores.Count == 0, errores);
        }
        else
        {
            var datosHeaderMap = LeerEncabezadosDatos(datosRange.FirstRow(), errores);
            if (datosHeaderMap.Count == 0 && errores.Any(e => e.TipoError == "ESTRUCTURA"))
                return new ExcelValidationResult(campos, filasDatos, false, errores);

            ValidarColumnasDatos(campos, datosHeaderMap, errores);

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

                catalogos?.ValoresFila.TryAdd(rowNum, valores);
                foreach (var campo in campos)
                {
                    var key = NormalizarNombreVariable(campo.NombreCampo);
                    valores.TryGetValue(key, out var valor);
                    ValidarValorCelda(campo, valor, rowNum, errores, catalogos);
                }
                filasDatos.Add(new DatosFilaDto(rowNum, JsonSerializer.Serialize(valores)));
            }

            if (filasDatos.Count == 0 && campos.Any(c => c.Obligatorio))
                errores.Add(new ValidationErrorDto(null, "Datos", "No hay filas de datos para validar.", "ESTRUCTURA"));
        }

        return new ExcelValidationResult(campos, filasDatos, errores.Count == 0, errores);
    }

    private static void ValidarColumnasDatos(
        IReadOnlyList<CampoDiccionarioDto> campos,
        IReadOnlyDictionary<string, int> datosHeaderMap,
        List<ValidationErrorDto> errores)
    {
        foreach (var campo in campos)
        {
            var key = NormalizarNombreVariable(campo.NombreCampo);
            if (!datosHeaderMap.ContainsKey(key))
            {
                errores.Add(new ValidationErrorDto(
                    null, campo.NombreCampo,
                    $"La columna «{campo.NombreCampo}» del diccionario no existe en la hoja Datos.",
                    "COLUMNA"));
            }
        }
    }

    private static Dictionary<string, int> LeerEncabezadosLegacy(
        IXLRangeRow headerRow,
        List<ValidationErrorDto> errores)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var encontradas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = NormalizarNombreVariable(cell.GetString());
            if (string.IsNullOrEmpty(name)) continue;
            map[name] = cell.Address.ColumnNumber;
            encontradas.Add(name);
        }

        foreach (var col in ColumnasDiccionarioLegacy)
        {
            if (!encontradas.Contains(NormalizarNombreVariable(col)))
            {
                errores.Add(new ValidationErrorDto(
                    null, col,
                    $"Falta la columna «{col}» en la hoja Diccionario_Datos.",
                    "ESTRUCTURA"));
            }
        }

        return map;
    }

    private static Dictionary<string, int> LeerEncabezadosDatos(IXLRangeRow headerRow, List<ValidationErrorDto> errores)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = NormalizarNombreVariable(cell.GetString());
            if (string.IsNullOrEmpty(name)) continue;
            map[name] = cell.Address.ColumnNumber;
        }

        if (map.Count == 0)
            errores.Add(new ValidationErrorDto(null, "Datos", "La hoja Datos no tiene encabezados de columna.", "ESTRUCTURA"));

        return map;
    }

    private static List<CampoDiccionarioDto> LeerCamposDiccionarioLegacy(
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
                if (!headers.TryGetValue(NormalizarNombreVariable(col), out var c)) return "";
                return row.Cell(c).GetFormattedString().Trim();
            }

            var nombre = Leer("Nombre_Campo");
            if (string.IsNullOrWhiteSpace(nombre)) continue;

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

            int? longitud = null;
            var longStr = Leer("Longitud");
            if (!string.IsNullOrWhiteSpace(longStr) && int.TryParse(longStr, out var len) && len >= 0)
                longitud = len;

            campos.Add(new CampoDiccionarioDto(
                nombre, tipo, obligatorio,
                NullIfEmpty(Leer("Descripcion")), longitud,
                NullIfEmpty(Leer("Formato")), NullIfEmpty(Leer("Valores_Permitidos")),
                NullIfEmpty(Leer("Tabla_Referencia")), NullIfEmpty(Leer("Campo_Referencia")),
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
        List<ValidationErrorDto> errores,
        CatalogoValidacionContext? catalogos)
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
                .Split(';', ',', '|', '\n')
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (permitidos.Count > 0 && !permitidos.Contains(valor!))
                errores.Add(new ValidationErrorDto(fila, campo.NombreCampo,
                    $"Valor «{valor}» no está en dominios permitidos.", "DOMINIO"));
        }

        if (!string.IsNullOrWhiteSpace(campo.Formato) && campo.TipoDato == "texto")
        {
            try
            {
                if (campo.Formato.StartsWith('^') && !Regex.IsMatch(valor!, campo.Formato))
                    errores.Add(new ValidationErrorDto(fila, campo.NombreCampo,
                        "No cumple el patrón definido en Fórmula/Formato.", "FORMATO"));
            }
            catch { /* ignore */ }
        }

        if (catalogos is not null && !string.IsNullOrWhiteSpace(campo.TablaReferencia) && !vacio)
            ValidarCatalogo(campo, valor!, fila, errores, catalogos);
    }

    private static void ValidarCatalogo(CampoDiccionarioDto campo, string valor, int fila, List<ValidationErrorDto> errores, CatalogoValidacionContext cat)
    {
        var tabla = campo.TablaReferencia!.Trim().ToLowerInvariant();
        if (tabla is "dim_departamentos" or "departamentos")
        {
            if (!cat.Departamentos.ContainsKey(valor))
                errores.Add(new ValidationErrorDto(fila, campo.NombreCampo, $"Departamento «{valor}» no existe en catálogo.", "CATALOGO"));
            return;
        }
        if (tabla is "dim_municipios" or "municipios")
        {
            if (!cat.Municipios.ContainsKey(valor))
            {
                errores.Add(new ValidationErrorDto(fila, campo.NombreCampo, $"Código DIVIPOLA/municipio «{valor}» no existe en catálogo.", "CATALOGO"));
                return;
            }
            foreach (var depKey in new[] { "codigo_departamento", "departamento", "cod_departamento" })
            {
                if (!cat.ValoresFila.TryGetValue(fila, out var filaVals) || !filaVals.TryGetValue(depKey, out var depVal))
                    continue;
                var munDep = cat.Municipios[valor];
                if (!string.Equals(depVal, munDep, StringComparison.OrdinalIgnoreCase))
                    errores.Add(new ValidationErrorDto(fila, campo.NombreCampo, "El municipio no pertenece al departamento indicado.", "CATALOGO"));
                break;
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

    /// <summary>Normaliza nombres de variable (CODIGO DIVIPOLA → codigo_divipola).</summary>
    public static string NormalizarNombreVariable(string? s) =>
        DiccionarioOscV2Reader.Normalizar(s);

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed record CampoDiccionarioDto(
    string NombreCampo, string TipoDato, bool Obligatorio,
    string? Descripcion, int? Longitud, string? Formato, string? ValoresPermitidos,
    string? TablaReferencia, string? CampoReferencia, int Orden);

public sealed class CatalogoValidacionContext
{
    public Dictionary<string, string> Departamentos { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Municipios { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, Dictionary<string, string?>> ValoresFila { get; } = new();
}

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
