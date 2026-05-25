using ClosedXML.Excel;

var path = args.Length > 0 ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data", "Diccionario_de_datos_OSC.V2.xlsx");

if (!File.Exists(path))
{
    Console.Error.WriteLine("No existe: " + path);
    return 1;
}

using var wb = new XLWorkbook(path);
foreach (var ws in wb.Worksheets)
{
    Console.WriteLine($"=== {ws.Name} ===");
    var used = ws.RangeUsed();
    if (used is null) { Console.WriteLine("(vacía)"); continue; }
    var headers = used.FirstRow().CellsUsed().Select(c => c.GetString()).ToList();
    Console.WriteLine("Columnas (" + headers.Count + "):");
    for (var i = 0; i < headers.Count; i++)
        Console.WriteLine($"  [{i + 1}] {headers[i]}");
    Console.WriteLine("Filas datos: " + Math.Max(0, used.RowCount() - 1));
    for (var ri = 1; ri <= Math.Min(15, used.RowCount()); ri++)
    {
        var row = used.Row(ri);
        var vals = row.CellsUsed().Select(c => $"{c.Address.ColumnLetter}{ri}={c.GetFormattedString()}").ToList();
        if (vals.Count > 0)
            Console.WriteLine($"  Fila {ri}: " + string.Join(" | ", vals));
    }
}
return 0;
