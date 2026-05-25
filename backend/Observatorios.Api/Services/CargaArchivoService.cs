using System.Text.Json;
using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

public sealed class CargaArchivoService(
    ArchivosRepository archivos,
    CargasRepository cargas,
    ExcelValidationService excelValidator,
    CatalogoRepository catalogos,
    ArchivoCargaRepository archivoCargaRepo,
    IndicadorRepository indicadores,
    AuditoriaRepository auditoria)
{
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
            await archivoCargaRepo.SincronizarAsync(archivoId, cargaId, user.UsuarioId, depId,
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
        string? observaciones,
        CancellationToken ct)
    {
        if (!AuthorizationService.PuedeSubirCargue(user))
            throw new UnauthorizedAccessException("Su rol no permite cargar archivos.");

        if (!user.PuedeAccederLineaTematica(lineaTematicaId))
            throw new UnauthorizedAccessException("No tiene permiso para cargar en esa línea temática.");

        if (!await indicadores.PerteneceALineaAsync(indicadorId, lineaTematicaId, ct))
            throw new InvalidOperationException("El indicador no pertenece a la línea temática seleccionada.");

        var depId = ResolverDependencia(user, null);
        var cargaId = await cargas.CrearCargaAsync(archivoId, depId, user.UsuarioId, CargaEstados.Recibido, ct);
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.EnValidacion, "Envío definitivo", ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "ENVIO_DEFINITIVO",
            $"Archivo enviado: {nombreOriginal}", ct);

        var esCsv = nombreOriginal.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        if (esCsv)
        {
            await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoExitoso,
                "Archivo CSV enviado (validación estructural previa aplicada).", ct);
            await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "ENVIO_CSV_OK", nombreOriginal, ct);
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
}

public sealed record CargaProcesoResult(
    int CargaId,
    int ArchivoId,
    string Estado,
    bool EsValido,
    int TotalErrores);
