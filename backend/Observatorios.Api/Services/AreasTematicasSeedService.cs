using ClosedXML.Excel;
using Observatorios.Api.Data;

namespace Observatorios.Api.Services;

/// <summary>
/// Importa dependencias y áreas temáticas desde «Áreas temáticas OSC V.2.xlsx».
/// Busca en data/ de la raíz del repositorio.
/// </summary>
public sealed class AreasTematicasSeedService(
    DependenciasRepository dependencias,
    AreaTematicaRepository areas)
{
    private static readonly string[] NombresArchivo =
    [
        "Áreas temáticas OSC V.2.xlsx",
        "Areas tematicas OSC V.2.xlsx",
        "areas-tematicas-osc-v2.xlsx"
    ];

    /// <summary>Importa desde data/ si existe el Excel de áreas temáticas OSC V.2.</summary>
    public async Task<SeedResult> ImportarSiExisteAsync(string repoRoot, CancellationToken ct = default)
    {
        var dataDir = Path.Combine(repoRoot, "data");
        string? path = null;
        foreach (var name in NombresArchivo)
        {
            var p = Path.Combine(dataDir, name);
            if (File.Exists(p)) { path = p; break; }
        }

        if (path is null)
            return new SeedResult(false, 0, 0, "No se encontró el Excel en data/. Coloque «Áreas temáticas OSC V.2.xlsx» y reinicie la API.");

        return await ImportarDesdeArchivoAsync(path, ct);
    }

    /// <summary>Importa dependencias y áreas desde una ruta de archivo Excel específica.</summary>
    public async Task<SeedResult> ImportarDesdeArchivoAsync(string path, CancellationToken ct = default)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var used = ws.RangeUsed();
        if (used is null) return new SeedResult(false, 0, 0, "Hoja vacía.");

        var header = used.FirstRow();
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in header.CellsUsed())
        {
            var n = Normalizar(c.GetString());
            if (!string.IsNullOrEmpty(n)) map[n] = c.Address.ColumnNumber;
        }

        int Col(params string[] names)
        {
            foreach (var n in names)
                if (map.TryGetValue(Normalizar(n), out var col)) return col;
            return -1;
        }

        var colDep = Col("dependencia", "nombre_dependencia", "secretaria");
        var colArea = Col("area_tematica", "area", "nombre_area", "tema");
        var colCodDep = Col("codigo_dependencia", "cod_dependencia");
        var colCodArea = Col("codigo_area", "codigo", "cod_area");

        if (colDep < 0 && colCodDep < 0)
            return new SeedResult(false, 0, 0, "No se identificaron columnas de dependencia en el Excel.");

        var depsCreadas = 0;
        var areasCreadas = 0;
        var depCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var r = header.RowBelow().RowNumber(); r <= used.LastRow().RowNumber(); r++)
        {
            var row = ws.Row(r);
            if (FilaVacia(row)) continue;

            string Leer(int col) => col < 0 ? "" : row.Cell(col).GetString().Trim();
            var depNombre = Leer(colDep);
            var depCodigo = Leer(colCodDep);
            if (string.IsNullOrWhiteSpace(depCodigo))
                depCodigo = GenerarCodigo(depNombre);
            if (string.IsNullOrWhiteSpace(depNombre) && string.IsNullOrWhiteSpace(depCodigo))
                continue;

            if (!depCache.TryGetValue(depCodigo, out var depId))
            {
                depId = await dependencias.ObtenerOCrearPorCodigoAsync(depCodigo, depNombre, ct);
                depCache[depCodigo] = depId;
                depsCreadas++;
            }

            var areaNombre = Leer(colArea);
            var areaCodigo = Leer(colCodArea);
            if (string.IsNullOrWhiteSpace(areaNombre) && string.IsNullOrWhiteSpace(areaCodigo))
                continue;
            if (string.IsNullOrWhiteSpace(areaCodigo))
                areaCodigo = GenerarCodigo(areaNombre);

            try
            {
                await areas.CrearAsync(depId, areaCodigo, areaNombre, null, ct);
                areasCreadas++;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2627 or 2601)
            {
                /* ya existe */
            }
        }

        return new SeedResult(true, depsCreadas, areasCreadas, $"Importación desde {Path.GetFileName(path)}");
    }

    private static string GenerarCodigo(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var s = new string(nombre.Where(char.IsLetterOrDigit).Take(20).ToArray()).ToUpperInvariant();
        return string.IsNullOrEmpty(s) ? "AREA" : s;
    }

    private static bool FilaVacia(IXLRow row) =>
        !row.CellsUsed().Any(c => !c.IsEmpty() && !string.IsNullOrWhiteSpace(c.GetString()));

    private static string Normalizar(string? s) =>
        (s ?? "").Trim().ToLowerInvariant().Replace(" ", "_").Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u");
}

public sealed record SeedResult(bool Ok, int Dependencias, int Areas, string Mensaje);
