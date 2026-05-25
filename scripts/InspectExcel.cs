using ClosedXML.Excel;
var path = args[0];
using var wb = new XLWorkbook(path);
foreach (var ws in wb.Worksheets) {
    Console.WriteLine("HOJA: " + ws.Name);
    var used = ws.RangeUsed();
    if (used == null) { Console.WriteLine("  (vacia)"); continue; }
    var cols = new List<string>();
    foreach (var c in used.FirstRow().CellsUsed()) cols.Add(c.GetString());
    Console.WriteLine("  COLS: " + string.Join(" | ", cols));
    Console.WriteLine("  FILAS: " + (used.RowCount() - 1));
}
