using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Models;
using Observatorios.Api.Services;

namespace Observatorios.Api.Endpoints;

public static class ApiEndpoints
{
    public static void MapObservatorioApi(this WebApplication app, string repoRoot, string uploadsDir)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/ping", () => Results.Json(new { ok = true, mensaje = "API Observatorios.Api activa" }));
        api.MapGet("/salud", () => Results.Ok(new
        {
            ok = true,
            servicio = "Observatorio Salud Departamental Casanare — archivos, cargas Excel y proyección población"
        }));

        api.MapPost("/auth/login", async (LoginRequest body, AuthService auth, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Usuario) || string.IsNullOrWhiteSpace(body.Password))
                return Results.BadRequest(new { error = "Usuario y contraseña son obligatorios." });

            var result = await auth.LoginAsync(body.Usuario, body.Password, ct);
            return result is null
                ? Results.Json(new { error = "Credenciales inválidas." }, statusCode: 401)
                : Results.Ok(new
                {
                    token = result.Token,
                    usuario = new
                    {
                        id = result.UsuarioId,
                        nombre = result.NombreUsuario,
                        email = result.Email,
                        dependencia_id = result.DependenciaId,
                        dependencia = result.DependenciaNombre,
                        linea_tematica_id = result.LineaTematicaId,
                        linea_tematica = result.LineaTematicaNombre,
                        roles = result.Roles
                    }
                });
        });

        var secured = api.MapGroup("").RequireAuthorization();

        secured.MapGet("/auth/me", async (HttpContext http, UsuariosRepository repo, CancellationToken ct) =>
        {
            var ctx = http.GetUserContext();
            if (ctx is null) return Results.Unauthorized();

            var u = await repo.GetByIdAsync(ctx.UsuarioId, ct);
            var roles = u?.Roles ?? ctx.Roles;
            return Results.Ok(new
            {
                usuario = new
                {
                    id = ctx.UsuarioId,
                    nombre = u?.NombreUsuario ?? ctx.NombreUsuario,
                    email = u?.Email,
                    dependencia_id = u?.DependenciaId ?? ctx.DependenciaId,
                    dependencia = u?.DependenciaNombre,
                    linea_tematica_id = u?.LineaTematicaId ?? ctx.LineaTematicaId,
                    linea_tematica = u?.LineaTematicaNombre,
                    roles
                }
            });
        });

        secured.MapPost("/dependencias", async (
            CrearDependenciaRequest body,
            DependenciasRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (http.GetUserContext()?.EsAdministrador != true)
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(body.Codigo) || string.IsNullOrWhiteSpace(body.Nombre))
                return Results.BadRequest(new { error = "Código y nombre son obligatorios." });

            try
            {
                var id = await repo.CrearAsync(body.Codigo, body.Nombre, ct);
                return Results.Created($"/api/dependencias/{id}", new { id, codigo = body.Codigo.Trim(), nombre = body.Nombre.Trim() });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return Results.Conflict(new { error = "Ya existe una dependencia con ese código." });
            }
        });

        secured.MapGet("/dependencias", async (DependenciasRepository repo, CancellationToken ct) =>
        {
            var rows = await repo.ListarAsync(ct: ct);
            return Results.Ok(new
            {
                dependencias = rows.Select(d => new
                {
                    id = d.Id,
                    codigo = d.Codigo,
                    nombre = d.Nombre,
                    activo = d.Activo
                })
            });
        });

        secured.MapPost("/usuarios", async (
            CrearUsuarioApiRequest body,
            UsuariosRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (http.GetUserContext()?.EsAdministrador != true)
                return Results.Forbid();

            if (string.IsNullOrWhiteSpace(body.NombreUsuario) || string.IsNullOrWhiteSpace(body.Password))
                return Results.BadRequest(new { error = "Nombre de usuario y contraseña son obligatorios." });

            var roles = body.Roles?.Count > 0 ? body.Roles : new[] { "Operador" };
            if (!roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase) && body.DependenciaId is null)
                return Results.BadRequest(new { error = "Los usuarios no administradores requieren dependencia_id." });

            var id = await repo.CrearAsync(new CrearUsuarioRequest(
                body.NombreUsuario.Trim(),
                body.Password,
                body.Email,
                body.DependenciaId,
                body.LineaTematicaId,
                roles), ct);

            return Results.Created($"/api/usuarios/{id}", new { id, nombre_usuario = body.NombreUsuario });
        });

        secured.MapGet("/lineas-tematicas", async (LineaTematicaRepository repo, HttpContext http, CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var rows = await repo.ListarAsync(ct: ct);
            if (!user.EsAdministrador && user.LineaTematicaId is int asignada)
                rows = rows.Where(l => l.Id == asignada).ToList();
            return Results.Ok(new
            {
                lineas_tematicas = rows.Select(l => new
                {
                    id = l.Id,
                    codigo = l.Codigo,
                    nombre = l.Nombre,
                    descripcion = l.Descripcion
                })
            });
        });

        secured.MapGet("/indicadores", async (
            [FromQuery] int linea_tematica_id,
            IndicadorRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (linea_tematica_id <= 0)
                return Results.BadRequest(new { error = "Indique linea_tematica_id." });
            var user = http.GetUserContext()!;
            if (!user.PuedeAccederLineaTematica(linea_tematica_id))
                return Results.Forbid();
            var rows = await repo.ListarAsync(linea_tematica_id, ct: ct);
            return Results.Ok(new
            {
                indicadores = rows.Select(i => new
                {
                    id = i.Id,
                    linea_tematica_id = i.LineaTematicaId,
                    linea_tematica = i.LineaTematicaNombre,
                    codigo = i.Codigo,
                    nombre = i.Nombre,
                    descripcion = i.Descripcion
                })
            });
        });
        secured.MapGet("/indicadores/prostata", async (
            IndicadoresRepository repo,
            [FromQuery] string? codigoDane,
            [FromQuery] string? territorio,
            [FromQuery] string? regional,
            [FromQuery] int? anio,
            [FromQuery] string? area,
            [FromQuery] int? limit,
            CancellationToken ct) =>
        {
            var rows = await repo.ListarProstataAsync(
                codigoDane, territorio, regional, anio, area, limit ?? 20000, ct);
            return Results.Ok(new { indicador = "prostata", fuente = "vw_Tasa_Mortalidad_Prostata_Validada", datos = rows });
        });

        secured.MapGet("/archivos", async (ArchivosRepository repo, HttpContext http, CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            int? depFiltro = null, lineaFiltro = null, usuarioFiltro = null;
            if (!user.PuedeVerTodosLosArchivos)
                usuarioFiltro = user.UsuarioId;
            var rows = await repo.ListAsync(depFiltro, lineaFiltro, usuarioFiltro, ct);
            return Results.Ok(new
            {
                archivos = rows.Select(a => new
                {
                    id = a.Id,
                    dependencia_id = a.DependenciaId,
                    linea_tematica_id = a.LineaTematicaId,
                    linea_tematica = a.LineaTematicaNombre,
                    indicador_id = a.IndicadorId,
                    indicador = a.IndicadorNombre,
                    nombre_original = a.NombreOriginal,
                    tipo_mime = a.TipoMime,
                    tamano_bytes = a.TamanoBytes,
                    creado_en = a.CreadoEn,
                    subido_por = a.SubidoPor,
                    observaciones = a.Observaciones,
                    estado = a.Estado,
                    estado_etiqueta = ArchivoEstados.Etiqueta(a.Estado),
                    fecha_validacion = a.FechaValidacion,
                    fecha_envio = a.FechaEnvio
                })
            });
        });

        secured.MapPost("/archivos/validar", (HttpRequest req, ArchivoFlujoService flujo, HttpContext http, CancellationToken ct) =>
            ValidarArchivo(req, repoRoot, uploadsDir, flujo, http, ct));

        secured.MapPost("/archivos/enviar", async (
            EnviarArchivoRequest body,
            ArchivoFlujoService flujo,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (body.ArchivoId <= 0)
                return Results.BadRequest(new { error = "Indique el identificador del archivo validado." });
            var user = http.GetUserContext()!;
            try
            {
                var result = await flujo.EnviarAsync(body.ArchivoId, user, repoRoot, ct);
                return Results.Ok(new
                {
                    archivo_id = result.ArchivoId,
                    carga_id = result.CargaId,
                    estado = result.EstadoArchivo,
                    estado_carga = CargaEstados.Normalizar(result.EstadoCarga),
                    estado_etiqueta = ArchivoEstados.Etiqueta(result.EstadoArchivo),
                    procesamiento_valido = result.ProcesamientoValido,
                    total_errores = result.TotalErrores,
                    mensaje = result.ProcesamientoValido
                        ? "Archivo enviado. Pendiente de aprobación en Validaciones."
                        : "Archivo enviado con errores de validación."
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 403);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        secured.MapPost("/archivos/{id:int}/enviar-y-aprobar", async (
            int id,
            AprobarRechazarRequest? body,
            ArchivoFlujoService flujo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            try
            {
                var result = await flujo.EnviarYAprobarAsync(id, user, repoRoot, body?.Observaciones, ct);
                return Results.Ok(new
                {
                    archivo_id = result.ArchivoId,
                    carga_id = result.CargaId,
                    estado = result.EstadoArchivo,
                    estado_carga = CargaEstados.Normalizar(result.EstadoCarga),
                    procesamiento_valido = result.ProcesamientoValido,
                    total_errores = result.TotalErrores,
                    aprobado = result.Aprobado,
                    mensaje = result.Aprobado
                        ? "Cargue enviado y aprobado."
                        : result.ProcesamientoValido
                            ? "Cargue enviado; queda pendiente de aprobación."
                            : "El archivo tiene errores; no se puede aprobar."
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 403);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        secured.MapPost("/archivos/{id:int}/rechazar-validacion", async (
            int id,
            AprobarRechazarRequest body,
            ArchivoFlujoService flujo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            try
            {
                await flujo.RechazarValidacionAsync(id, user, body.Observaciones, ct);
                return Results.Ok(new { ok = true, estado = ArchivoEstados.Rechazado });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 403);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        secured.MapGet("/archivos/{id:int}", async (
            int id,
            ArchivosRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var row = await repo.GetAsync(id, ct);
            if (row is null) return Results.NotFound(new { error = "No encontrado" });
            if (!user.PuedeAccederArchivo(row.SubidoPorUsuarioId, row.DependenciaId))
                return Results.Forbid();

            return Results.Ok(new
            {
                id = row.Id,
                linea_tematica_id = row.LineaTematicaId,
                linea_tematica = row.LineaTematicaNombre,
                indicador_id = row.IndicadorId,
                indicador = row.IndicadorNombre,
                nombre_original = row.NombreOriginal,
                ruta_relativa = row.RutaRelativa,
                tipo_mime = row.TipoMime,
                tamano_bytes = row.TamanoBytes,
                creado_en = row.CreadoEn,
                subido_por = row.SubidoPor,
                observaciones = row.Observaciones,
                estado = row.Estado,
                estado_etiqueta = ArchivoEstados.Etiqueta(row.Estado),
                fecha_validacion = row.FechaValidacion,
                fecha_envio = row.FechaEnvio,
                errores_validacion = string.IsNullOrWhiteSpace(row.ErroresValidacionJson)
                    ? Array.Empty<string>()
                    : JsonSerializer.Deserialize<string[]>(row.ErroresValidacionJson) ?? Array.Empty<string>()
            });
        });

        secured.MapGet("/archivos/{id:int}/descargar", async (
            int id,
            ArchivosRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var row = await repo.GetAsync(id, ct);
            if (row is null) return Results.NotFound(new { error = "No encontrado" });
            if (!user.PuedeAccederArchivo(row.SubidoPorUsuarioId, row.DependenciaId))
                return Results.Forbid();

            var abs = Path.Combine(repoRoot, row.RutaRelativa);
            if (!File.Exists(abs)) return Results.NotFound(new { error = "Archivo en disco no existe" });

            var contentType = string.IsNullOrWhiteSpace(row.TipoMime) ? "application/octet-stream" : row.TipoMime;
            return Results.File(abs, contentType, fileDownloadName: row.NombreOriginal);
        });

        secured.MapDelete("/archivos/{id:int}", async (
            int id,
            ArchivosRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var row = await repo.GetAsync(id, ct);
            if (row is null) return Results.NotFound(new { error = "No encontrado" });
            if (!user.PuedeAccederArchivo(row.SubidoPorUsuarioId, row.DependenciaId))
                return Results.Forbid();

            var abs = Path.Combine(repoRoot, row.RutaRelativa);
            if (File.Exists(abs)) File.Delete(abs);
            await repo.DeleteAsync(id, ct);
            return Results.Ok(new { ok = true });
        });

        secured.MapPost("/cargas/upload", (HttpRequest req, CargaArchivoService svc, HttpContext http, CancellationToken ct) =>
            SubirExcelCarga(req, repoRoot, uploadsDir, svc, http, ct));
        secured.MapPost("/cargas/excel", (HttpRequest req, CargaArchivoService svc, HttpContext http, CancellationToken ct) =>
            SubirExcelCarga(req, repoRoot, uploadsDir, svc, http, ct));

        secured.MapGet("/cargas/mis-cargas", async (
            CargasRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var dep = user.EsAdministrador ? (int?)null : user.DependenciaId;
            var rows = await repo.ListarPorUsuarioAsync(user.UsuarioId, dep, ct);
            return Results.Ok(new
            {
                cargas = rows.Select(c => new
                {
                    id = c.Id,
                    dependencia = c.DependenciaNombre,
                    estado = CargaEstados.Normalizar(c.Estado),
                    fecha_inicio = c.FechaInicio,
                    archivo = c.NombreArchivo,
                    total_errores = c.TotalErrores
                })
            });
        });

        secured.MapGet("/auditoria", async (AuditoriaRepository repo, HttpContext http, CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            if (!user.EsAdministrador && !user.EsAuditor)
                return Results.Forbid();
            var rows = await repo.ListarAsync(ct: ct);
            return Results.Ok(new
            {
                auditoria = rows.Select(a => new
                {
                    id = a.Id,
                    fecha = a.Fecha,
                    usuario = a.Usuario,
                    accion = a.Accion,
                    entidad = a.Entidad,
                    entidad_id = a.EntidadId,
                    detalle = a.Detalle
                })
            });
        });

        secured.MapPost("/cargas/{id:int}/validar", async (
            int id,
            HttpRequest req,
            CargaArchivoService cargaService,
            CargasRepository cargasRepo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var carga = await cargasRepo.GetCargaAsync(id, ct);
            if (carga is null) return Results.NotFound(new { error = "Carga no encontrada." });
            if (!user.PuedeAccederDependencia(carga.DependenciaId))
                return Results.Forbid();
            if (!AuthorizationService.PuedeValidarCargue(user))
                return Results.Forbid();

            Stream stream;
            if (req.HasFormContentType)
            {
                var form = await req.ReadFormAsync(ct);
                var file = form.Files.GetFile("archivo");
                if (file is null) return Results.BadRequest(new { error = "Envíe el Excel en 'archivo' o reutilice el archivo en disco." });
                stream = new MemoryStream();
                await file.CopyToAsync(stream, ct);
            }
            else
            {
                var abs = Path.Combine(repoRoot, carga.RutaRelativa);
                if (!File.Exists(abs)) return Results.NotFound(new { error = "Archivo físico no encontrado." });
                stream = File.OpenRead(abs);
            }

            await using (stream)
            {
                var result = await cargaService.RevalidarAsync(id, stream, user, ct);
                return Results.Ok(new
                {
                    carga_id = result.CargaId,
                    estado = result.Estado,
                    valido = result.EsValido,
                    total_errores = result.TotalErrores
                });
            }
        });

        secured.MapGet("/cargas/{id:int}/errores", async (
            int id,
            CargasRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var carga = await repo.GetCargaAsync(id, ct);
            if (carga is null) return Results.NotFound();
            if (!user.PuedeAccederDependencia(carga.DependenciaId))
                return Results.Forbid();

            var errores = await repo.ListarErroresAsync(id, ct);
            return Results.Ok(new
            {
                carga_id = id,
                estado = carga.Estado,
                errores = errores.Select(e => new
                {
                    id = e.Id,
                    fila = e.NumeroFila,
                    columna = e.NombreColumna,
                    mensaje = e.Mensaje,
                    tipo = e.TipoError
                })
            });
        });

        secured.MapPost("/cargas/{id:int}/aprobar", async (
            int id,
            AprobarRechazarRequest? body,
            CargasRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var carga = await repo.GetCargaAsync(id, ct);
            if (carga is null) return Results.NotFound();
            if (!user.PuedeAccederDependencia(carga.DependenciaId))
                return Results.Forbid();
            if (!CargaEstados.EsPendienteAprobacion(carga.Estado))
                return Results.BadRequest(new { error = "Solo se aprueban cargas validadas exitosamente." });
            if (!AuthorizationService.PuedeValidarCargue(user))
                return Results.Forbid();

            await repo.ActualizarEstadoAsync(id, CargaEstados.Aprobado, body?.Observaciones, ct);
            await repo.RegistrarHistorialAsync(id, user.UsuarioId, "APROBADO", body?.Observaciones, ct);
            return Results.Ok(new { ok = true, estado = CargaEstados.Aprobado });
        });

        secured.MapPost("/cargas/{id:int}/rechazar", async (
            int id,
            AprobarRechazarRequest body,
            CargasRepository repo,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var carga = await repo.GetCargaAsync(id, ct);
            if (carga is null) return Results.NotFound();
            if (!user.PuedeAccederDependencia(carga.DependenciaId))
                return Results.Forbid();
            if (!AuthorizationService.PuedeValidarCargue(user))
                return Results.Forbid();

            await repo.ActualizarEstadoAsync(id, CargaEstados.Rechazado, body.Observaciones, ct);
            await repo.RegistrarHistorialAsync(id, user.UsuarioId, "RECHAZADO", body.Observaciones, ct);
            return Results.Ok(new { ok = true, estado = CargaEstados.Rechazado });
        });

        secured.MapGet("/cargas", async (
            CargasRepository repo,
            HttpContext http,
            [FromQuery] int? dependencia_id,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            int? filtro = user.EsAdministrador ? dependencia_id : user.DependenciaId;
            if (!user.EsAdministrador && dependencia_id.HasValue && dependencia_id != user.DependenciaId)
                return Results.Forbid();

            var rows = await repo.ListarAsync(filtro, ct);
            return Results.Ok(new
            {
                cargas = rows.Select(c => new
                {
                    id = c.Id,
                    dependencia_id = c.DependenciaId,
                    dependencia = c.DependenciaNombre,
                    estado = CargaEstados.Normalizar(c.Estado),
                    fecha_inicio = c.FechaInicio,
                    fecha_fin = c.FechaFin,
                    archivo = c.NombreArchivo,
                    usuario = c.Usuario,
                    total_errores = c.TotalErrores
                })
            });
        });

        secured.MapGet("/cargas/pendientes-aprobacion", async (
            CargasRepository repo,
            HttpContext http,
            [FromQuery] int? dependencia_id,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            if (!AuthorizationService.PuedeValidarCargue(user))
                return Results.Forbid();

            int? filtro = user.EsAdministrador ? dependencia_id : user.DependenciaId;
            if (!user.EsAdministrador && dependencia_id.HasValue && dependencia_id != user.DependenciaId)
                return Results.Forbid();

            var rows = await repo.ListarAsync(filtro, ct);
            var pendientes = rows
                .Where(c => CargaEstados.EsPendienteAprobacion(c.Estado))
                .ToList();

            return Results.Ok(new
            {
                cargas = pendientes.Select(c => new
                {
                    id = c.Id,
                    dependencia_id = c.DependenciaId,
                    dependencia = c.DependenciaNombre,
                    estado = CargaEstados.Normalizar(c.Estado),
                    fecha_inicio = c.FechaInicio,
                    fecha_fin = c.FechaFin,
                    archivo = c.NombreArchivo,
                    usuario = c.Usuario,
                    total_errores = c.TotalErrores
                })
            });
        });

        secured.MapGet("/cargas/{id:int}", async (int id, CargasRepository repo, HttpContext http, CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var carga = await repo.GetCargaAsync(id, ct);
            if (carga is null) return Results.NotFound();
            if (!user.PuedeAccederDependencia(carga.DependenciaId))
                return Results.Forbid();

            return Results.Ok(new
            {
                id = carga.Id,
                archivo_id = carga.ArchivoId,
                dependencia_id = carga.DependenciaId,
                dependencia = carga.DependenciaNombre,
                usuario = carga.NombreUsuario,
                estado = carga.Estado,
                observaciones = carga.Observaciones,
                fecha_inicio = carga.FechaInicio,
                fecha_fin = carga.FechaFin,
                nombre_archivo = carga.NombreOriginal
            });
        });

        secured.MapGet("/cargas/historial", async (
            CargasRepository repo,
            HttpContext http,
            [FromQuery] int? carga_id,
            CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var depFiltro = user.EsAdministrador ? (int?)null : user.DependenciaId;
            var rows = await repo.ListarHistorialAsync(carga_id, depFiltro, ct);
            return Results.Ok(new
            {
                historial = rows.Select(h => new
                {
                    id = h.Id,
                    carga_id = h.CargaArchivoId,
                    usuario = h.NombreUsuario,
                    accion = h.Accion,
                    detalle = h.Detalle,
                    fecha = h.Fecha
                })
            });
        });

        // Catálogos dinámicos (proyección población)
        var cat = secured.MapGroup("/catalogos");
        cat.MapGet("/proyeccion", async (ICatalogoService svc, CancellationToken ct) =>
            Results.Ok(await svc.ObtenerCatalogosProyeccionAsync(ct)));

        cat.MapGet("/departamentos", async (ICatalogoService svc, CancellationToken ct) =>
            Results.Ok(new { departamentos = await svc.ObtenerDepartamentosAsync(ct) }));

        cat.MapGet("/municipios", async ([FromQuery] string? codigoDepartamento, ICatalogoService svc, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(codigoDepartamento))
                return Results.Ok(new { municipios = await svc.ObtenerMunicipiosPorDepartamentoAsync(codigoDepartamento, ct) });
            return Results.Ok(new { municipios = await svc.ObtenerMunicipiosAsync(ct) });
        });

        cat.MapGet("/municipios/{codigoDepartamento}", async (string codigoDepartamento, ICatalogoService svc, CancellationToken ct) =>
            Results.Ok(new { municipios = await svc.ObtenerMunicipiosPorDepartamentoAsync(codigoDepartamento, ct) }));

        cat.MapGet("/regionales", async (ICatalogoService svc, CancellationToken ct) =>
            Results.Ok(new { regionales = await svc.ObtenerRegionalesAsync(ct) }));

        cat.MapGet("/areas", async (ICatalogoService svc, CancellationToken ct) =>
            Results.Ok(new { areas = await svc.ObtenerAreasAsync(ct) }));

        cat.MapGet("/sexos", async (ICatalogoService svc, CancellationToken ct) =>
            Results.Ok(new { sexos = await svc.ObtenerSexosAsync(ct) }));

        cat.MapGet("/anios", async (ICatalogoService svc, CancellationToken ct) =>
            Results.Ok(new { anios = await svc.ObtenerAniosAsync(ct) }));

        // Proyección población (requiere autenticación)
        secured.MapGet("/proyeccion-poblacion/vistas", () =>
            Results.Ok(new { vistas = PoblacionVistasRepository.ClavesValidas.ToArray() }));

        secured.MapGet("/proyeccion-poblacion/nacional-casanare",
            (HttpRequest req, PoblacionVistasRepository repo, CancellationToken ct) =>
                ProyeccionPoblacionJson("nacional-casanare", req, repo, ct));
        secured.MapGet("/proyeccion-poblacion/curso-vida",
            (HttpRequest req, PoblacionVistasRepository repo, CancellationToken ct) =>
                ProyeccionPoblacionJson("curso-vida", req, repo, ct));
        secured.MapGet("/proyeccion-poblacion/quinquenios",
            (HttpRequest req, PoblacionVistasRepository repo, CancellationToken ct) =>
                ProyeccionPoblacionJson("quinquenios", req, repo, ct));
        secured.MapGet("/proyeccion-poblacion/{clave}/excel",
            (string clave, HttpRequest req, PoblacionVistasRepository repo, CancellationToken ct) =>
                ProyeccionPoblacionExcel(clave, req, repo, ct));

        secured.MapAdminApi(repoRoot);
        secured.MapDashboardApi();
    }

    private static async Task<IResult> SubirExcelCarga(
        HttpRequest req,
        string repoRoot,
        string uploadsDir,
        CargaArchivoService cargaService,
        HttpContext http,
        CancellationToken ct)
    {
        var user = http.GetUserContext()!;
        if (!req.HasFormContentType)
            return Results.BadRequest(new { error = "Use multipart/form-data con campo 'archivo'." });

        var form = await req.ReadFormAsync(ct);
        var file = form.Files.GetFile("archivo");
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "Falta el archivo Excel." });

        if (!EsExcel(file.FileName))
            return Results.BadRequest(new { error = "Solo se aceptan archivos .xlsx." });

        int? depOverride = int.TryParse(form["dependencia_id"], out var d) ? d : null;
        if (!int.TryParse(form["linea_tematica_id"], out var lineaId) || lineaId <= 0)
            return Results.BadRequest(new { error = "Seleccione una línea temática." });
        if (!int.TryParse(form["indicador_id"], out var indicadorId) || indicadorId <= 0)
            return Results.BadRequest(new { error = "Seleccione un indicador." });
        if (!user.PuedeAccederLineaTematica(lineaId))
            return Results.Json(new { error = "No puede cargar archivos en otra línea temática." }, statusCode: 403);
        var observaciones = form["observaciones"].ToString();
        if (string.IsNullOrWhiteSpace(observaciones)) observaciones = null;
        int? areaId = int.TryParse(form["area_tematica_id"], out var a) ? a : null;
        int? plantillaId = int.TryParse(form["plantilla_carga_id"], out var p) ? p : null;

        var stored = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{SanitizeFileName(file.FileName)}";
        var abs = Path.Combine(uploadsDir, stored);
        try
        {
            await GuardarArchivoSubidoAsync(file, abs, ct);
        }
        catch (IOException ex)
        {
            return Results.Json(new { error = $"No se pudo guardar el archivo: {ex.Message}" }, statusCode: 500);
        }

        var rel = Path.GetRelativePath(repoRoot, abs).Replace("\\", "/");
        await using var mem = new MemoryStream();
        await file.CopyToAsync(mem, ct);

        try
        {
            var result = await cargaService.ProcesarExcelAsync(
                mem, file.FileName, abs, rel, file.Length, user, depOverride,
                lineaId, indicadorId, observaciones, areaId, plantillaId, ct);
            return Results.Created($"/api/cargas/{result.CargaId}", new
            {
                carga_id = result.CargaId,
                archivo_id = result.ArchivoId,
                estado = CargaEstados.Normalizar(result.Estado),
                valido = result.EsValido,
                total_errores = result.TotalErrores
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 403);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ValidarArchivo(
        HttpRequest req,
        string repoRoot,
        string uploadsDir,
        ArchivoFlujoService flujo,
        HttpContext http,
        CancellationToken ct)
    {
        var user = http.GetUserContext()!;
        if (!req.HasFormContentType)
            return Results.BadRequest(new { error = "Use multipart/form-data con campo 'archivo'." });

        var form = await req.ReadFormAsync(ct);
        var file = form.Files.GetFile("archivo");
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "Seleccione un archivo Excel (.xlsx) con plantilla OSC." });

        if (!EsArchivoPermitido(file.FileName))
            return Results.BadRequest(new { error = "Solo se aceptan archivos Excel (.xlsx) con hojas Diccionario_datos y DATA." });

        if (!int.TryParse(form["linea_tematica_id"], out var lineaId) || lineaId <= 0)
            return Results.BadRequest(new { error = "Seleccione una línea temática." });
        if (!int.TryParse(form["indicador_id"], out var indicadorId) || indicadorId <= 0)
            return Results.BadRequest(new { error = "Seleccione un indicador." });
        if (!user.PuedeAccederLineaTematica(lineaId))
            return Results.Json(new { error = "No puede cargar archivos en otra línea temática." }, statusCode: 403);

        var observaciones = form["observaciones"].ToString();
        if (string.IsNullOrWhiteSpace(observaciones)) observaciones = null;

        var stored = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{SanitizeFileName(file.FileName)}";
        var abs = Path.Combine(uploadsDir, stored);
        try
        {
            await GuardarArchivoSubidoAsync(file, abs, ct);
        }
        catch (IOException ex)
        {
            return Results.Json(new { error = $"No se pudo guardar el archivo: {ex.Message}" }, statusCode: 500);
        }

        var rel = Path.GetRelativePath(repoRoot, abs).Replace("\\", "/");
        var mime = file.ContentType;
        if (string.IsNullOrWhiteSpace(mime))
            mime = file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? "text/csv"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        await using var mem = new MemoryStream();
        await file.CopyToAsync(mem, ct);

        try
        {
            var result = await flujo.ValidarAsync(
                mem, file.FileName, abs, rel, file.Length, mime, user,
                lineaId, indicadorId, observaciones, ct);

            return Results.Ok(new
            {
                archivo_id = result.ArchivoId,
                estado = result.Estado,
                estado_etiqueta = result.EstadoEtiqueta,
                valido = result.Valido,
                errores = result.Errores,
                errores_diccionario = result.ErroresDiccionario,
                errores_data = result.ErroresData,
                observaciones = result.Observaciones,
                total_errores_diccionario = result.TotalErroresDiccionario,
                total_errores_data = result.TotalErroresData,
                geografia = result.Geografia,
                mensaje = result.Valido
                    ? "Archivo validado correctamente. Puede continuar con el envío."
                    : result.TotalErroresDiccionario > 0
                        ? "Corrija los errores en la hoja Diccionario_datos antes de continuar."
                        : "Corrija los errores en la hoja DATA antes de continuar."
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 403);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (IOException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 500);
        }
    }

    private static async Task GuardarArchivoSubidoAsync(IFormFile file, string rutaAbsoluta, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(rutaAbsoluta);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var fs = File.Create(rutaAbsoluta);
        await file.CopyToAsync(fs, ct);
    }

    private static bool EsArchivoPermitido(string name) =>
        name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

    private static bool EsExcel(string name) =>
        name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeFileName(string name)
    {
        var cleaned = Regex.Replace(name, @"[^a-zA-Z0-9._-]+", "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "archivo" : cleaned;
    }

    private static async Task<IResult> ProyeccionPoblacionJson(
        string clave,
        HttpRequest req,
        PoblacionVistasRepository repo,
        CancellationToken ct)
    {
        var pagina = int.TryParse(req.Query["pagina"], out var pq) ? pq : (int?)null;
        var tamanoPagina = int.TryParse(req.Query["tamanoPagina"], out var tq) ? tq : (int?)null;
        var territorio = req.Query["territorio"].FirstOrDefault();
        var regional = req.Query["regional"].FirstOrDefault();
        var area = req.Query["area"].FirstOrDefault();
        var sexo = req.Query["sexo"].FirstOrDefault();
        var ano = int.TryParse(req.Query["ano"], out var aq) ? aq : (int?)null;
        var codigoDepartamento = req.Query["codigoDepartamento"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(codigoDepartamento))
            codigoDepartamento = DimCatalogSql.CodigoDepartamentoCasanare;
        var codigoMunicipio = req.Query["codigoMunicipio"].FirstOrDefault();
        var p = pagina ?? 1;
        var t = tamanoPagina ?? 10;
        try
        {
            var r = await repo.ConsultarPaginadoAsync(
                clave, p, t, territorio, regional, area, sexo, ano, codigoDepartamento, codigoMunicipio, ct);
            return Results.Ok(new
            {
                clave = r.Clave,
                pagina = r.Pagina,
                tamanoPagina = r.TamanoPagina,
                totalFilas = r.TotalFilas,
                totalPaginas = r.TotalPaginas,
                columnas = r.Columnas,
                filas = r.Filas
            });
        }
        catch (ArgumentException)
        {
            return Results.NotFound(new { error = "Vista no encontrada." });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 502);
        }
    }

    private static async Task<IResult> ProyeccionPoblacionExcel(
        string clave,
        HttpRequest req,
        PoblacionVistasRepository repo,
        CancellationToken ct)
    {
        var territorio = req.Query["territorio"].FirstOrDefault();
        var regional = req.Query["regional"].FirstOrDefault();
        var area = req.Query["area"].FirstOrDefault();
        var sexo = req.Query["sexo"].FirstOrDefault();
        var ano = int.TryParse(req.Query["ano"], out var aq) ? aq : (int?)null;
        var codigoDepartamento = req.Query["codigoDepartamento"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(codigoDepartamento))
            codigoDepartamento = DimCatalogSql.CodigoDepartamentoCasanare;
        var codigoMunicipio = req.Query["codigoMunicipio"].FirstOrDefault();

        try
        {
            // Límite alto para exportar la consulta filtrada completa.
            var r = await repo.ConsultarPaginadoAsync(
                clave, 1, 200000, territorio, regional, area, sexo, ano, codigoDepartamento, codigoMunicipio, ct);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Consulta");

            for (var c = 0; c < r.Columnas.Count; c++)
                ws.Cell(1, c + 1).Value = r.Columnas[c];

            for (var i = 0; i < r.Filas.Count; i++)
            {
                var fila = r.Filas[i];
                for (var c = 0; c < r.Columnas.Count; c++)
                {
                    var col = r.Columnas[c];
                    fila.TryGetValue(col, out var v);
                    ws.Cell(i + 2, c + 1).Value = v?.ToString() ?? "";
                }
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            var safeClave = Regex.Replace(clave, @"[^a-zA-Z0-9_-]+", "_");
            var nombre = $"proyeccion-{safeClave}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
            return Results.File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                nombre);
        }
        catch (ArgumentException)
        {
            return Results.NotFound(new { error = "Vista no encontrada." });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: 502);
        }
    }
}

public sealed record EnviarArchivoRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("archivo_id")] int ArchivoId);
public sealed record LoginRequest(string Usuario, string Password);
public sealed record CrearDependenciaRequest(string Codigo, string Nombre);
public sealed record CrearUsuarioApiRequest(
    string NombreUsuario,
    string Password,
    string? Email,
    int? DependenciaId,
    int? LineaTematicaId,
    IReadOnlyList<string>? Roles);
public sealed record AprobarRechazarRequest(string? Observaciones);
