using System.Text.RegularExpressions;
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
                        roles = result.Roles
                    }
                });
        });

        var secured = api.MapGroup("").RequireAuthorization();

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
                roles), ct);

            return Results.Created($"/api/usuarios/{id}", new { id, nombre_usuario = body.NombreUsuario });
        });

        secured.MapGet("/archivos", async (ArchivosRepository repo, HttpContext http, CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var depFiltro = user.EsAdministrador ? (int?)null : user.DependenciaId;
            var rows = await repo.ListByDependenciaAsync(depFiltro, ct);
            return Results.Ok(new
            {
                archivos = rows.Select(a => new
                {
                    id = a.Id,
                    dependencia_id = a.DependenciaId,
                    dependencia = a.DependenciaNombre,
                    nombre_original = a.NombreOriginal,
                    tipo_mime = a.TipoMime,
                    tamano_bytes = a.TamanoBytes,
                    creado_en = a.CreadoEn,
                    subido_por = a.SubidoPor
                })
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
            if (!user.PuedeAccederDependencia(row.DependenciaId))
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
            if (!user.PuedeAccederDependencia(row.DependenciaId))
                return Results.Forbid();

            var abs = Path.Combine(repoRoot, row.RutaRelativa);
            if (File.Exists(abs)) File.Delete(abs);
            await repo.DeleteAsync(id, ct);
            return Results.Ok(new { ok = true });
        });

        secured.MapPost("/cargas/excel", async (
            HttpRequest req,
            CargaArchivoService cargaService,
            HttpContext http,
            CancellationToken ct) =>
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

            var stored = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{SanitizeFileName(file.FileName)}";
            var abs = Path.Combine(uploadsDir, stored);
            await using (var fs = File.Create(abs))
                await file.CopyToAsync(fs, ct);

            var rel = Path.GetRelativePath(repoRoot, abs).Replace("\\", "/");
            await using var mem = new MemoryStream();
            await file.CopyToAsync(mem, ct);

            try
            {
                var result = await cargaService.ProcesarExcelAsync(
                    mem, file.FileName, abs, rel, file.Length, user, depOverride, ct);
                return Results.Created($"/api/cargas/{result.CargaId}", new
                {
                    carga_id = result.CargaId,
                    archivo_id = result.ArchivoId,
                    estado = result.Estado,
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
            if (carga.Estado != CargaEstados.ValidadoOk)
                return Results.BadRequest(new { error = "Solo se aprueban cargas en estado VALIDADO_OK." });

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
                    estado = c.Estado,
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

        secured.MapAdminApi();
        secured.MapDashboardApi();
    }

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
        var p = pagina ?? 1;
        var t = tamanoPagina ?? 10;
        try
        {
            var r = await repo.ConsultarPaginadoAsync(
                clave, p, t, territorio, regional, area, sexo, ano, ct);
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
}

public sealed record LoginRequest(string Usuario, string Password);
public sealed record CrearDependenciaRequest(string Codigo, string Nombre);
public sealed record CrearUsuarioApiRequest(
    string NombreUsuario,
    string Password,
    string? Email,
    int? DependenciaId,
    IReadOnlyList<string>? Roles);
public sealed record AprobarRechazarRequest(string? Observaciones);
