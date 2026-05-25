using Observatorios.Api.Data;

namespace Observatorios.Api.Services;

/// <summary>Validación previa al envío: plantilla OSC (Diccionario_datos + DATA).</summary>
public sealed class ArchivoPrevalidacionService(
    IndicadorRepository indicadores,
    OscPlantillaValidacionService oscValidador)
{
    public async Task<PrevalidacionResultado> ValidarAsync(
        Stream stream,
        string nombreArchivo,
        int lineaTematicaId,
        int indicadorId,
        CancellationToken ct = default)
    {
        if (stream is null || !stream.CanRead)
            return Fallo(["El archivo no se pudo leer."]);

        if (stream.CanSeek) stream.Position = 0;
        if (stream.Length == 0)
            return Fallo(["El archivo está vacío."]);

        var ext = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".csv")
        {
            return Fallo(["Formato no permitido. Solo se aceptan archivos Excel (.xlsx) o CSV (.csv)."]);
        }

        if (!await indicadores.PerteneceALineaAsync(indicadorId, lineaTematicaId, ct))
        {
            return Fallo(["El indicador no pertenece a la línea temática seleccionada."]);
        }

        if (ext == ".csv")
        {
            return Fallo([
                "La plantilla OSC requiere un archivo Excel (.xlsx) con las hojas Diccionario_datos y DATA.",
            ]);
        }

        try
        {
            var osc = oscValidador.Validar(stream);
            return new PrevalidacionResultado(
                osc.EsValido,
                osc.TodosLosErrores,
                osc.ErroresDiccionario,
                osc.ErroresData,
                osc.Observaciones,
                osc.TotalErroresDiccionario,
                osc.TotalErroresData,
                osc.Campos,
                osc.Filas);
        }
        catch (Exception ex)
        {
            return Fallo([$"No se pudo procesar el archivo: {ex.Message}"]);
        }
    }

    private static PrevalidacionResultado Fallo(IReadOnlyList<string> errores) =>
        new(false, errores, errores, [], [], errores.Count, 0, [], []);
}

public sealed record PrevalidacionResultado(
    bool EsValido,
    IReadOnlyList<string> Errores,
    IReadOnlyList<string> ErroresDiccionario,
    IReadOnlyList<string> ErroresData,
    IReadOnlyList<string> Observaciones,
    int TotalErroresDiccionario,
    int TotalErroresData,
    IReadOnlyList<CampoDiccionarioDto> Campos,
    IReadOnlyList<DatosFilaDto> Filas);
