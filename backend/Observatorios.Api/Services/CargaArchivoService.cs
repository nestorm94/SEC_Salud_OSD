using System.Text.Json;
using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

/// <summary>
/// Orquesta el procesamiento completo de una carga Excel: validación OSC,
/// persistencia de diccionario/datos/errores y transición de estados.
/// </summary>
public sealed class CargaArchivoService(
    ArchivosRepository archivos,
    CargasRepository cargas,
    ExcelValidationService excelValidator,
    OscPlantillaValidacionService oscValidador,
    CatalogoRepository catalogos,
    ArchivoCargaRepository archivoCargaRepo,
    IndicadorRepository indicadores,
    AuditoriaRepository auditoria)
{
    /// <summary>
    /// Procesa un Excel subido: crea archivo, valida plantilla OSC y registra la carga.
    /// </summary>
    public async Task<CargaProcesoResult> ProcesarExcelAsync(
        Stream excelStream,
        string nombreOriginal,
        string rutaAbsoluta,
        string rutaRelativa,
        long tamano,
        UserContext user,
        int? dependenciaIdOverride,
        int lineaTematicaId,
        int indicadorId,
        string? observaciones,
        int? areaTematicaId,
        int? plantillaCargaId,
        CancellationToken ct)
    {
        if (!AuthorizationService.PuedeSubirCargue(user))
            throw new UnauthorizedAccessException("Su rol no permite cargar archivos.");

        if (!user.PuedeAccederLineaTematica(lineaTematicaId))
            throw new UnauthorizedAccessException("No tiene permiso para cargar en esa línea temática.");

        if (!await indicadores.PerteneceALineaAsync(indicadorId, lineaTematicaId, ct))
            throw new InvalidOperationException("El indicador no pertenece a la línea temática seleccionada.");

        var depId = ResolverDependencia(user, dependenciaIdOverride);
        var stored = Path.GetFileName(rutaAbsoluta);
        var archivoId = await archivos.InsertAsync(new ArchivoInsert(
            depId,
            lineaTematicaId,
            indicadorId,
            nombreOriginal,
            stored,
            rutaRelativa,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            tamano,
            user.UsuarioId,
            observaciones), ct);

        var cargaId = await cargas.CrearCargaAsync(archivoId, depId, user.UsuarioId, CargaEstados.Recibido, ct);
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.EnValidacion, "Validación automática", ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "INICIO_VALIDACION",
            $"Archivo recibido: {nombreOriginal}", ct);

        if (areaTematicaId.HasValue)
            await archivoCargaRepo.SincronizarAsync(archivoId, user.UsuarioId, depId,
                areaTematicaId.Value, plantillaCargaId, CargaEstados.EnValidacion, ct);

        excelStream.Position = 0;
        var catCtx = await catalogos.CargarContextoAsync(ct);
        var validacion = excelValidator.Validar(excelStream, catCtx);

        await cargas.GuardarDiccionarioAsync(cargaId, validacion.Campos, ct);

        if (!validacion.EsValido)
        {
            await cargas.GuardarErroresAsync(cargaId, validacion.Errores, ct);
            await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoConErrores,
                $"Se encontraron {validacion.Errores.Count} error(es).", ct);
            await archivoCargaRepo.ActualizarEstadoPorCargaAsync(cargaId, CargaEstados.ValidadoConErrores, ct);
            await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "VALIDACION_CON_ERRORES",
                JsonSerializer.Serialize(new { totalErrores = validacion.Errores.Count }), ct);

            return new CargaProcesoResult(cargaId, archivoId, CargaEstados.ValidadoConErrores, false, validacion.Errores.Count);
        }

        await cargas.GuardarDatosAsync(cargaId, validacion.Filas, ct);
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoExitoso,
            $"Validación correcta. {validacion.Filas.Count} fila(s).", ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "VALIDACION_OK",
            $"{validacion.Filas.Count} filas persistidas.", ct);
        await archivoCargaRepo.ActualizarEstadoPorCargaAsync(cargaId, CargaEstados.ValidadoExitoso, ct);
        await auditoria.RegistrarAsync(user.UsuarioId, "CARGA_VALIDADA", "CargasArchivo", cargaId.ToString(),
            nombreOriginal, null, ct);

        return new CargaProcesoResult(cargaId, archivoId, CargaEstados.ValidadoExitoso, true, 0);
    }

    /// <summary>Procesa un archivo ya registrado (tras validación previa y envío).</summary>
    /// <summary>Procesa un archivo ya registrado (re-envío o validación por validador).</summary>
    public async Task<CargaProcesoResult> ProcesarArchivoExistenteAsync(
        int archivoId,
        Stream excelStream,
        string nombreOriginal,
        string rutaAbsoluta,
        string rutaRelativa,
        long tamano,
        UserContext user,
        int lineaTematicaId,
        int indicadorId,
        int dependenciaArchivoId,
        string? observaciones,
        bool envioPorValidador,
        CancellationToken ct)
    {
        if (!envioPorValidador && !AuthorizationService.PuedeSubirCargue(user))
            throw new UnauthorizedAccessException("Su rol no permite cargar archivos.");
        if (envioPorValidador && !AuthorizationService.PuedeValidarCargue(user))
            throw new UnauthorizedAccessException("Su rol no permite aprobar cargues.");

        if (!user.PuedeAccederDependencia(dependenciaArchivoId))
            throw new UnauthorizedAccessException("No tiene permiso para esta dependencia.");

        if (!user.PuedeAccederLineaTematica(lineaTematicaId))
            throw new UnauthorizedAccessException("No tiene permiso para cargar en esa línea temática.");

        if (!await indicadores.PerteneceALineaAsync(indicadorId, lineaTematicaId, ct))
            throw new InvalidOperationException("El indicador no pertenece a la línea temática seleccionada.");

        var cargaId = await cargas.CrearCargaAsync(archivoId, dependenciaArchivoId, user.UsuarioId, CargaEstados.Recibido, ct);
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.EnValidacion, "Envío definitivo", ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "ENVIO_DEFINITIVO",
            $"Archivo enviado: {nombreOriginal}", ct);

        excelStream.Position = 0;
        var osc = nombreOriginal.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? oscValidador.Validar(excelStream)
            : null;

        if (osc is not null)
        {
            await cargas.GuardarDiccionarioAsync(cargaId, osc.Campos, ct);
            if (!osc.EsValido)
            {
                var erroresDto = AErroresDto(osc.TodosLosErrores);
                await cargas.GuardarErroresAsync(cargaId, erroresDto, ct);
                await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoConErrores,
                    $"Se encontraron {osc.TodosLosErrores.Count} error(es) en el envío.", ct);
                await archivoCargaRepo.ActualizarEstadoPorCargaAsync(cargaId, CargaEstados.ValidadoConErrores, ct);
                return new CargaProcesoResult(cargaId, archivoId, CargaEstados.ValidadoConErrores, false, osc.TodosLosErrores.Count);
            }

            await cargas.GuardarDatosAsync(cargaId, osc.Filas, ct);
            await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoExitoso,
                $"Envío correcto. {osc.Filas.Count} fila(s).", ct);
            await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "ENVIO_OK",
                $"{osc.Filas.Count} filas persistidas.", ct);
            await archivoCargaRepo.ActualizarEstadoPorCargaAsync(cargaId, CargaEstados.ValidadoExitoso, ct);
            await auditoria.RegistrarAsync(user.UsuarioId, "ARCHIVO_ENVIADO", "Archivos", archivoId.ToString(),
                nombreOriginal, null, ct);
            return new CargaProcesoResult(cargaId, archivoId, CargaEstados.ValidadoExitoso, true, 0);
        }

        excelStream.Position = 0;
        var catCtx = await catalogos.CargarContextoAsync(ct);
        var validacion = excelValidator.Validar(excelStream, catCtx);
        await cargas.GuardarDiccionarioAsync(cargaId, validacion.Campos, ct);

        if (!validacion.EsValido)
        {
            await cargas.GuardarErroresAsync(cargaId, validacion.Errores, ct);
            await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoConErrores,
                $"Se encontraron {validacion.Errores.Count} error(es) en el envío.", ct);
            await archivoCargaRepo.ActualizarEstadoPorCargaAsync(cargaId, CargaEstados.ValidadoConErrores, ct);
            return new CargaProcesoResult(cargaId, archivoId, CargaEstados.ValidadoConErrores, false, validacion.Errores.Count);
        }

        await cargas.GuardarDatosAsync(cargaId, validacion.Filas, ct);
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoExitoso,
            $"Envío correcto. {validacion.Filas.Count} fila(s).", ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "ENVIO_OK",
            $"{validacion.Filas.Count} filas persistidas.", ct);
        await archivoCargaRepo.ActualizarEstadoPorCargaAsync(cargaId, CargaEstados.ValidadoExitoso, ct);
        await auditoria.RegistrarAsync(user.UsuarioId, "ARCHIVO_ENVIADO", "Archivos", archivoId.ToString(),
            nombreOriginal, null, ct);

        return new CargaProcesoResult(cargaId, archivoId, CargaEstados.ValidadoExitoso, true, 0);
    }

    /// <summary>Marca la carga como aprobada y registra historial.</summary>
    public async Task AprobarCargaAsync(
        int cargaId,
        UserContext user,
        string? observaciones,
        CancellationToken ct)
    {
        if (!AuthorizationService.PuedeValidarCargue(user))
            throw new UnauthorizedAccessException("Su rol no permite aprobar cargues.");

        var carga = await cargas.GetCargaAsync(cargaId, ct)
            ?? throw new InvalidOperationException("Carga no encontrada.");
        if (!user.PuedeAccederDependencia(carga.DependenciaId))
            throw new UnauthorizedAccessException("Sin permiso para esta dependencia.");
        if (!CargaEstados.EsPendienteAprobacion(carga.Estado))
            throw new InvalidOperationException("Solo se aprueban cargas validadas exitosamente.");

        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.Aprobado, observaciones, ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "APROBADO", observaciones, ct);
    }

    /// <summary>Re-ejecuta validación sobre una carga existente y actualiza errores.</summary>
    public async Task<CargaProcesoResult> RevalidarAsync(
        int cargaId,
        Stream excelStream,
        UserContext user,
        CancellationToken ct)
    {
        var carga = await cargas.GetCargaAsync(cargaId, ct)
            ?? throw new InvalidOperationException("Carga no encontrada.");
        if (!user.PuedeAccederDependencia(carga.DependenciaId))
            throw new UnauthorizedAccessException("Sin permiso para esta dependencia.");

        await cargas.LimpiarResultadosValidacionAsync(cargaId, ct);
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.EnValidacion, "Revalidación en curso.", ct);

        excelStream.Position = 0;
        var catCtx = await catalogos.CargarContextoAsync(ct);
        var validacion = excelValidator.Validar(excelStream, catCtx);
        await cargas.GuardarDiccionarioAsync(cargaId, validacion.Campos, ct);

        if (!validacion.EsValido)
        {
            await cargas.GuardarErroresAsync(cargaId, validacion.Errores, ct);
            await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoConErrores, null, ct);
            await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "REVALIDACION_ERRORES", null, ct);
            return new CargaProcesoResult(cargaId, carga.ArchivoId, CargaEstados.ValidadoConErrores, false, validacion.Errores.Count);
        }

        await cargas.GuardarDatosAsync(cargaId, validacion.Filas, ct);
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoExitoso, null, ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "REVALIDACION_OK", null, ct);
        return new CargaProcesoResult(cargaId, carga.ArchivoId, CargaEstados.ValidadoExitoso, true, 0);
    }

    private static int ResolverDependencia(UserContext user, int? overrideId)
    {
        if (user.EsAdministrador && overrideId.HasValue)
            return overrideId.Value;
        if (user.DependenciaId is null)
            throw new InvalidOperationException("El usuario no tiene dependencia asignada.");
        return user.DependenciaId.Value;
    }

    private static List<ValidationErrorDto> AErroresDto(IReadOnlyList<string> mensajes) =>
        mensajes.Select(m => new ValidationErrorDto(null, null, m, "VALIDACION")).ToList();
}

public sealed record CargaProcesoResult(
    int CargaId,
    int ArchivoId,
    string Estado,
    bool EsValido,
    int TotalErrores);
