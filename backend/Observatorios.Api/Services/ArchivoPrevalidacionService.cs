using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Observatorios.Api.Data;

namespace Observatorios.Api.Services;

/// <summary>Validaciones previas al envío definitivo (estructura básica del archivo).</summary>
public sealed class ArchivoPrevalidacionService(IndicadorRepository indicadores)
{
    private static readonly string[] ColumnasPorDefecto =
        ["anio", "periodo", "valor", "departamento", "municipio"];

    public async Task<PrevalidacionResultado> ValidarAsync(
        Stream stream,
        string nombreArchivo,
        int lineaTematicaId,
        int indicadorId,
        CancellationToken ct = default)
    {
        var errores = new List<string>();

        if (stream is null || !stream.CanRead)
        {
            errores.Add("El archivo no se pudo leer.");
            return new PrevalidacionResultado(false, errores);
        }

        if (stream.CanSeek) stream.Position = 0;
        if (stream.Length == 0)
        {
            errores.Add("El archivo está vacío.");
            return new PrevalidacionResultado(false, errores);
        }

        var ext = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".csv")
        {
            errores.Add("Formato no permitido. Solo se aceptan archivos Excel (.xlsx) o CSV (.csv).");
            return new PrevalidacionResultado(false, errores);
        }

        if (!await indicadores.PerteneceALineaAsync(indicadorId, lineaTematicaId, ct))
        {
            errores.Add("El indicador no pertenece a la línea temática seleccionada.");
            return new PrevalidacionResultado(false, errores);
        }

        var columnasObligatorias = await ObtenerColumnasObligatoriasAsync(indicadorId, ct);

        TablaLeida tabla;
        try
        {
            tabla = ext == ".csv"
                ? LeerCsv(stream, errores)
                : LeerExcel(stream, errores);
        }
        catch (Exception ex)
        {
            errores.Add($"No se pudo procesar el archivo: {ex.Message}");
            return new PrevalidacionResultado(false, errores);
        }

        if (errores.Count > 0)
            return new PrevalidacionResultado(false, errores);

        ValidarEstructuraTabla(tabla, columnasObligatorias, errores);
        return new PrevalidacionResultado(errores.Count == 0, errores);
    }

    private async Task<IReadOnlyList<string>> ObtenerColumnasObligatoriasAsync(int indicadorId, CancellationToken ct)
    {
        var json = await indicadores.GetColumnasObligatoriasJsonAsync(indicadorId, ct);
        if (string.IsNullOrWhiteSpace(json))
            return ColumnasPorDefecto;

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list is { Count: > 0 })
                return list.Where(c => !string.IsNullOrWhiteSpace(c)).Select(NormalizarNombre).ToList();
        }
        catch
        {
            // ignorar JSON inválido
        }

        return ColumnasPorDefecto;
    }

    private static void ValidarEstructuraTabla(
        TablaLeida tabla,
        IReadOnlyList<string> columnasObligatorias,
        List<string> errores)
    {
        if (tabla.Encabezados.Count == 0)
        {
            errores.Add("El archivo no tiene encabezados en la primera fila.");
            return;
        }

        var headersNorm = tabla.Encabezados
            .Select((h, i) => new { Original = h, Norm = NormalizarNombre(h), Index = i })
            .Where(x => !string.IsNullOrEmpty(x.Norm))
            .ToList();

        if (headersNorm.Count == 0)
        {
            errores.Add("Los encabezados están vacíos.");
            return;
        }

        var headerSet = new HashSet<string>(headersNorm.Select(h => h.Norm), StringComparer.OrdinalIgnoreCase);

        foreach (var col in columnasObligatorias)
        {
            if (!headerSet.Contains(col))
                errores.Add($"Falta la columna obligatoria «{col}».");
        }

        var columnasVacias = new List<string>();
        foreach (var h in headersNorm)
        {
            var todasVacias = tabla.Filas.All(fila =>
                h.Index >= fila.Count || string.IsNullOrWhiteSpace(fila[h.Index]));
            if (todasVacias)
                columnasVacias.Add(h.Original);
        }
        if (columnasVacias.Count > 0)
            errores.Add($"Columnas completamente vacías: {string.Join(", ", columnasVacias)}.");

        if (tabla.Filas.Count == 0)
            errores.Add("No hay filas de datos después del encabezado.");
        else if (tabla.FilasVacias > 0)
            errores.Add($"Se encontraron {tabla.FilasVacias} fila(s) totalmente vacía(s).");
    }

    private static TablaLeida LeerExcel(Stream stream, List<string> errores)
    {
        using var wb = new XLWorkbook(stream);
        var hoja = wb.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Datos", StringComparison.OrdinalIgnoreCase))
            ?? wb.Worksheets.FirstOrDefault();

        if (hoja is null)
        {
            errores.Add("El libro Excel no contiene hojas.");
            return TablaLeida.Vacia;
        }

        var range = hoja.RangeUsed();
        if (range is null)
        {
            errores.Add("La hoja de datos está vacía.");
            return TablaLeida.Vacia;
        }

        var firstRow = range.FirstRow().RowNumber();
        var lastRow = range.LastRow().RowNumber();
        var firstCol = range.FirstColumn().ColumnNumber();
        var lastCol = range.LastColumn().ColumnNumber();

        var headers = new List<string>();
        for (var c = firstCol; c <= lastCol; c++)
            headers.Add(hoja.Cell(firstRow, c).GetString().Trim());

        var filas = new List<List<string>>();
        var filasVacias = 0;
        for (var r = firstRow + 1; r <= lastRow; r++)
        {
            var fila = new List<string>();
            var vacia = true;
            for (var c = firstCol; c <= lastCol; c++)
            {
                var val = hoja.Cell(r, c).GetString().Trim();
                fila.Add(val);
                if (!string.IsNullOrWhiteSpace(val)) vacia = false;
            }
            if (vacia)
            {
                filasVacias++;
                continue;
            }
            filas.Add(fila);
        }

        return new TablaLeida(headers, filas, filasVacias);
    }

    private static TablaLeida LeerCsv(Stream stream, List<string> errores)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lineas = new List<string>();
        while (!reader.EndOfStream)
        {
            var linea = reader.ReadLine();
            if (linea is not null) lineas.Add(linea);
        }

        if (lineas.Count == 0)
        {
            errores.Add("El archivo CSV está vacío.");
            return TablaLeida.Vacia;
        }

        var headers = ParsearLineaCsv(lineas[0]);
        var filas = new List<List<string>>();
        var filasVacias = 0;
        for (var i = 1; i < lineas.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lineas[i]))
            {
                filasVacias++;
                continue;
            }
            var celdas = ParsearLineaCsv(lineas[i]);
            if (celdas.All(string.IsNullOrWhiteSpace))
            {
                filasVacias++;
                continue;
            }
            filas.Add(celdas);
        }

        return new TablaLeida(headers, filas, filasVacias);
    }

    private static List<string> ParsearLineaCsv(string linea)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var enComillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var ch = linea[i];
            if (ch == '"')
            {
                if (enComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else enComillas = !enComillas;
            }
            else if ((ch == ',' || ch == ';') && !enComillas)
            {
                result.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else sb.Append(ch);
        }
        result.Add(sb.ToString().Trim());
        return result;
    }

    private static string NormalizarNombre(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre)
            ? ""
            : Regex.Replace(nombre.Trim().ToLowerInvariant(), @"[\s_\-]+", "_");

    private sealed record TablaLeida(List<string> Encabezados, List<List<string>> Filas, int FilasVacias)
    {
        public static TablaLeida Vacia => new([], [], 0);
    }
}

public sealed record PrevalidacionResultado(bool EsValido, IReadOnlyList<string> Errores);
