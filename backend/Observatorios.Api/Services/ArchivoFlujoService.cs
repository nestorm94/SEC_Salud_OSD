using System.Text.Json;
using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Models;

namespace Observatorios.Api.Services;

/// <summary>Flujo en dos pasos: validar archivo → enviar definitivamente.</summary>
public sealed class ArchivoFlujoService(
    ArchivosRepository archivos,
    ArchivoPrevalidacionService prevalidacion,
    CargaArchivoService cargaService,
    IndicadorRepository indicadores)
{
    public async Task<ValidarArchivoResponse> ValidarAsync(
        Stream stream,
        string nombreOriginal,
        string rutaAbsoluta,
        string rutaRelativa,
        long tamano,
        string? tipoMime,
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

        var depId = ResolverDependencia(user);
        var stored = Path.GetFileName(rutaAbsoluta);

        var archivoId = await archivos.InsertAsync(new ArchivoInsert(
            depId,
            lineaTematicaId,
            indicadorId,
            nombreOriginal,
            stored,
            rutaRelativa,
            tipoMime,
            tamano,
            user.UsuarioId,
            observaciones,
            ArchivoEstados.PendienteValidacion), ct);

        stream.Position = 0;
        var resultado = await prevalidacion.ValidarAsync(stream, nombreOriginal, lineaTematicaId, indicadorId, ct);

        var estado = resultado.EsValido ? ArchivoEstados.Validado : ArchivoEstados.Rechazado;
        var erroresJson = resultado.Errores.Count > 0
            ? JsonSerializer.Serialize(resultado.Errores)
            : null;

        await archivos.ActualizarResultadoValidacionAsync(archivoId, estado, erroresJson, ct);

        return new ValidarArchivoResponse(
            archivoId,
            estado,
            ArchivoEstados.Etiqueta(estado),
            resultado.EsValido,
            resultado.Errores,
            resultado.ErroresDiccionario,
            resultado.ErroresData,
            resultado.Observaciones,
            resultado.TotalErroresDiccionario,
            resultado.TotalErroresData);
    }

    public async Task<EnviarArchivoResponse> EnviarAsync(int archivoId, UserContext user, string repoRoot, CancellationToken ct)
    {
        if (!AuthorizationService.PuedeSubirCargue(user))
            throw new UnauthorizedAccessException("Su rol no permite enviar archivos.");

        var meta = await archivos.GetEstadoAsync(archivoId, ct)
            ?? throw new InvalidOperationException("Archivo no encontrado.");

        if (!user.PuedeAccederArchivo(meta.SubidoPorUsuarioId, meta.DependenciaId))
            throw new UnauthorizedAccessException("No tiene permiso sobre este archivo.");

        if (!ArchivoEstados.PuedeEnviar(meta.Estado))
            throw new InvalidOperationException(
                "Solo puede enviar archivos previamente validados. Valide el archivo primero.");

        if (meta.LineaTematicaId is not int lineaId || meta.IndicadorId is not int indicadorId)
            throw new InvalidOperationException("El archivo no tiene línea temática o indicador asociado.");

        var abs = Path.IsPathRooted(meta.RutaRelativa)
            ? meta.RutaRelativa
            : Path.Combine(repoRoot, meta.RutaRelativa);

        if (!File.Exists(abs))
            throw new InvalidOperationException("El archivo físico ya no existe en el servidor.");

        await using var mem = new MemoryStream();
        await using (var fs = File.OpenRead(abs))
            await fs.CopyToAsync(mem, ct);
        mem.Position = 0;

        var tamano = mem.Length;
        var result = await cargaService.ProcesarArchivoExistenteAsync(
            archivoId,
            mem,
            meta.NombreOriginal,
            abs,
            meta.RutaRelativa,
            tamano,
            user,
            lineaId,
            indicadorId,
            meta.DependenciaId,
            meta.Observaciones,
            envioPorValidador: false,
            ct);

        await archivos.MarcarEnviadoAsync(archivoId, ct);

        return new EnviarArchivoResponse(
            archivoId,
            result.CargaId,
            ArchivoEstados.Enviado,
            result.EsValido,
            result.TotalErrores,
            result.Estado);
    }

    /// <summary>Envío definitivo + aprobación en un paso (validador o administrador).</summary>
    public async Task<EnviarYAprobarResponse> EnviarYAprobarAsync(
        int archivoId,
        UserContext user,
        string repoRoot,
        string? observacionesAprobacion,
        CancellationToken ct)
    {
        if (!AuthorizationService.PuedeValidarCargue(user))
            throw new UnauthorizedAccessException("Su rol no permite aprobar cargues.");

        var meta = await archivos.GetEstadoAsync(archivoId, ct)
            ?? throw new InvalidOperationException("Archivo no encontrado.");

        if (!user.PuedeAccederArchivo(meta.SubidoPorUsuarioId, meta.DependenciaId))
            throw new UnauthorizedAccessException("No tiene permiso sobre este archivo.");

        if (!ArchivoEstados.PuedeEnviar(meta.Estado))
            throw new InvalidOperationException(
                "Solo puede aprobar archivos en estado Validado (tras la prevalidación en Carga Excel).");

        if (meta.LineaTematicaId is not int lineaId || meta.IndicadorId is not int indicadorId)
            throw new InvalidOperationException("El archivo no tiene línea temática o indicador asociado.");

        var abs = Path.IsPathRooted(meta.RutaRelativa)
            ? meta.RutaRelativa
            : Path.Combine(repoRoot, meta.RutaRelativa);

        if (!File.Exists(abs))
            throw new InvalidOperationException("El archivo físico ya no existe en el servidor.");

        await using var mem = new MemoryStream();
        await using (var fs = File.OpenRead(abs))
            await fs.CopyToAsync(mem, ct);
        mem.Position = 0;

        var tamano = mem.Length;
        var result = await cargaService.ProcesarArchivoExistenteAsync(
            archivoId,
            mem,
            meta.NombreOriginal,
            abs,
            meta.RutaRelativa,
            tamano,
            user,
            lineaId,
            indicadorId,
            meta.DependenciaId,
            meta.Observaciones,
            envioPorValidador: true,
            ct);

        await archivos.MarcarEnviadoAsync(archivoId, ct);

        var aprobado = false;
        if (result.EsValido && CargaEstados.EsPendienteAprobacion(result.Estado))
        {
            await cargaService.AprobarCargaAsync(result.CargaId, user, observacionesAprobacion, ct);
            aprobado = true;
        }

        return new EnviarYAprobarResponse(
            archivoId,
            result.CargaId,
            ArchivoEstados.Enviado,
            result.Estado,
            result.EsValido,
            result.TotalErrores,
            aprobado);
    }

    public async Task RechazarValidacionAsync(int archivoId, UserContext user, string? motivo, CancellationToken ct)
    {
        if (!AuthorizationService.PuedeValidarCargue(user))
            throw new UnauthorizedAccessException("Su rol no permite rechazar validaciones.");

        var meta = await archivos.GetEstadoAsync(archivoId, ct)
            ?? throw new InvalidOperationException("Archivo no encontrado.");

        if (!user.PuedeAccederArchivo(meta.SubidoPorUsuarioId, meta.DependenciaId))
            throw new UnauthorizedAccessException("No tiene permiso sobre este archivo.");

        if (!ArchivoEstados.PuedeEnviar(meta.Estado))
            throw new InvalidOperationException("Solo puede rechazar archivos validados que aún no se han enviado.");

        await archivos.ActualizarResultadoValidacionAsync(archivoId, ArchivoEstados.Rechazado, null, ct);
    }

    private static int ResolverDependencia(UserContext user)
    {
        if (user.DependenciaId is null)
            throw new InvalidOperationException("El usuario no tiene dependencia asignada.");
        return user.DependenciaId.Value;
    }
}

public sealed record ValidarArchivoResponse(
    int ArchivoId,
    string Estado,
    string EstadoEtiqueta,
    bool Valido,
    IReadOnlyList<string> Errores,
    IReadOnlyList<string> ErroresDiccionario,
    IReadOnlyList<string> ErroresData,
    IReadOnlyList<string> Observaciones,
    int TotalErroresDiccionario,
    int TotalErroresData);

public sealed record EnviarArchivoResponse(
    int ArchivoId,
    int CargaId,
    string EstadoArchivo,
    bool ProcesamientoValido,
    int TotalErrores,
    string EstadoCarga);

public sealed record EnviarYAprobarResponse(
    int ArchivoId,
    int CargaId,
    string EstadoArchivo,
    string EstadoCarga,
    bool ProcesamientoValido,
    int TotalErrores,
    bool Aprobado);
