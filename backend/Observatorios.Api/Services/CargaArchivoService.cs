using System.Text.Json;
using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

public sealed class CargaArchivoService(
    ArchivosRepository archivos,
    CargasRepository cargas,
    ExcelValidationService excelValidator)
{
    public async Task<CargaProcesoResult> ProcesarExcelAsync(
        Stream excelStream,
        string nombreOriginal,
        string rutaAbsoluta,
        string rutaRelativa,
        long tamano,
        UserContext user,
        int? dependenciaIdOverride,
        CancellationToken ct)
    {
        var depId = ResolverDependencia(user, dependenciaIdOverride);
        var stored = Path.GetFileName(rutaAbsoluta);
        var archivoId = await archivos.InsertAsync(new ArchivoInsert(
            depId,
            nombreOriginal,
            stored,
            rutaRelativa,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            tamano,
            user.UsuarioId), ct);

        var cargaId = await cargas.CrearCargaAsync(archivoId, depId, user.UsuarioId, CargaEstados.Validando, ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "INICIO_VALIDACION",
            $"Archivo recibido: {nombreOriginal}", ct);

        excelStream.Position = 0;
        var validacion = excelValidator.Validar(excelStream);

        await cargas.GuardarDiccionarioAsync(cargaId, validacion.Campos, ct);

        if (!validacion.EsValido)
        {
            await cargas.GuardarErroresAsync(cargaId, validacion.Errores, ct);
            await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoConErrores,
                $"Se encontraron {validacion.Errores.Count} error(es).", ct);
            await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "VALIDACION_CON_ERRORES",
                JsonSerializer.Serialize(new { totalErrores = validacion.Errores.Count }), ct);

            return new CargaProcesoResult(cargaId, archivoId, CargaEstados.ValidadoConErrores, false, validacion.Errores.Count);
        }

        await cargas.GuardarDatosAsync(cargaId, validacion.Filas, ct);
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoOk,
            $"Validación correcta. {validacion.Filas.Count} fila(s).", ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "VALIDACION_OK",
            $"{validacion.Filas.Count} filas persistidas.", ct);

        return new CargaProcesoResult(cargaId, archivoId, CargaEstados.ValidadoOk, true, 0);
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
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.Validando, "Revalidación en curso.", ct);

        excelStream.Position = 0;
        var validacion = excelValidator.Validar(excelStream);
        await cargas.GuardarDiccionarioAsync(cargaId, validacion.Campos, ct);

        if (!validacion.EsValido)
        {
            await cargas.GuardarErroresAsync(cargaId, validacion.Errores, ct);
            await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoConErrores, null, ct);
            await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "REVALIDACION_ERRORES", null, ct);
            return new CargaProcesoResult(cargaId, carga.ArchivoId, CargaEstados.ValidadoConErrores, false, validacion.Errores.Count);
        }

        await cargas.GuardarDatosAsync(cargaId, validacion.Filas, ct);
        await cargas.ActualizarEstadoAsync(cargaId, CargaEstados.ValidadoOk, null, ct);
        await cargas.RegistrarHistorialAsync(cargaId, user.UsuarioId, "REVALIDACION_OK", null, ct);
        return new CargaProcesoResult(cargaId, carga.ArchivoId, CargaEstados.ValidadoOk, true, 0);
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
