using Microsoft.AspNetCore.Mvc;
using Observatorios.Api.Auth;
using Observatorios.Api.Data;
using Observatorios.Api.Services;

namespace Observatorios.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminApi(this RouteGroupBuilder secured, string repoRoot)
    {
        var admin = secured.MapGroup("/admin").RequireAuthorization(AuthExtensions.PolicyAdmin);

        admin.MapGet("/dashboard", async (DashboardRepository repo, HttpContext http, CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var dep = user.EsAdministrador ? (int?)null : user.DependenciaId;
            var r = await repo.ObtenerResumenAsync(dep, null, ct);
            return Results.Ok(new
            {
                total_archivos = r.TotalArchivos,
                cargas_pendientes = r.CargasPendientes,
                cargas_con_error = r.CargasConError,
                cargas_aprobadas = r.CargasAprobadas,
                ultimos_cargues = r.UltimosCargues.Select(u => new
                {
                    id = u.Id,
                    dependencia = u.Dependencia,
                    estado = u.Estado,
                    archivo = u.Archivo,
                    fecha = u.Fecha,
                    usuario = u.Usuario
                })
            });
        });

        admin.MapPost("/usuarios", async (CrearUsuarioAdminRequest body, UsuariosRepository repo, AuditoriaRepository audit, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.NombreUsuario) || string.IsNullOrWhiteSpace(body.Password))
                return Results.BadRequest(new { error = "Nombre de usuario y contraseña son obligatorios." });
            var roles = body.Roles?.Count > 0 ? body.Roles : new[] { "CONSULTA" };
            var id = await repo.CrearAsync(new CrearUsuarioRequest(
                body.NombreUsuario.Trim(), body.Password, body.Email, body.DependenciaId, body.LineaTematicaId, roles), ct);
            if (body.AreasTematicasIds?.Count > 0)
                await repo.ActualizarAreasTematicasAsync(id, body.AreasTematicasIds, ct);
            var u = http.GetUserContext();
            await audit.RegistrarAsync(u?.UsuarioId, "CREAR_USUARIO", "Usuarios", id.ToString(), body.NombreUsuario, null, ct);
            return Results.Created($"/api/admin/usuarios/{id}", new { id });
        });

        admin.MapGet("/usuarios", async (UsuariosRepository repo, CancellationToken ct) =>
        {
            var rows = await repo.ListarAsync(ct);
            return Results.Ok(new
            {
                usuarios = rows.Select(u => new
                {
                    id = u.Id,
                    nombre_usuario = u.NombreUsuario,
                    email = u.Email,
                    activo = u.Activo,
                    dependencia_id = u.DependenciaId,
                    dependencia = u.DependenciaNombre,
                    linea_tematica_id = u.LineaTematicaId,
                    linea_tematica = u.LineaTematicaNombre,
                    roles = u.Roles
                })
            });
        });

        admin.MapGet("/usuarios/{id:int}", async (int id, UsuariosRepository repo, CancellationToken ct) =>
        {
            var u = await repo.GetByIdAsync(id, ct);
            return u is null ? Results.NotFound() : Results.Ok(new
            {
                id = u.Id,
                nombre_usuario = u.NombreUsuario,
                email = u.Email,
                activo = u.Activo,
                dependencia_id = u.DependenciaId,
                dependencia = u.DependenciaNombre,
                linea_tematica_id = u.LineaTematicaId,
                linea_tematica = u.LineaTematicaNombre,
                roles = u.Roles
            });
        });

        admin.MapPut("/usuarios/{id:int}", async (int id, ActualizarUsuarioApiRequest body, UsuariosRepository repo, AuditoriaRepository audit, HttpContext http, CancellationToken ct) =>
        {
            if (await repo.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Usuario no encontrado." });

            await repo.ActualizarAsync(id, new ActualizarUsuarioRequest(body.Email, body.DependenciaId, body.LineaTematicaId, body.Password), ct);
            if (body.Roles?.Count > 0)
                await repo.ActualizarRolesAsync(id, body.Roles, ct);

            var u = http.GetUserContext();
            await audit.RegistrarAsync(u?.UsuarioId, "ACTUALIZAR_USUARIO", "Usuarios", id.ToString(), null, null, ct);
            return Results.Ok(new { ok = true });
        });

        admin.MapDelete("/usuarios/{id:int}", async (int id, UsuariosRepository repo, AuditoriaRepository audit, HttpContext http, CancellationToken ct) =>
        {
            var ctx = http.GetUserContext();
            if (ctx?.UsuarioId == id)
                return Results.BadRequest(new { error = "No puede desactivar su propio usuario." });

            if (await repo.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Usuario no encontrado." });

            await repo.SetActivoAsync(id, false, ct);
            await audit.RegistrarAsync(ctx?.UsuarioId, "DESACTIVAR_USUARIO", "Usuarios", id.ToString(), null, null, ct);
            return Results.Ok(new { ok = true, activo = false });
        });

        admin.MapPatch("/usuarios/{id:int}/activo", async (int id, ActivarRequest body, UsuariosRepository repo, HttpContext http, CancellationToken ct) =>
        {
            var ctx = http.GetUserContext();
            if (!body.Activo && ctx?.UsuarioId == id)
                return Results.BadRequest(new { error = "No puede desactivar su propio usuario." });

            if (await repo.GetByIdAsync(id, ct) is null)
                return Results.NotFound(new { error = "Usuario no encontrado." });

            await repo.SetActivoAsync(id, body.Activo, ct);
            return Results.Ok(new { ok = true, activo = body.Activo });
        });

        admin.MapPut("/usuarios/{id:int}/roles", async (int id, AsignarRolesRequest body, UsuariosRepository repo, CancellationToken ct) =>
        {
            if (body.Roles is null || body.Roles.Count == 0)
                return Results.BadRequest(new { error = "Indique al menos un rol." });
            await repo.ActualizarRolesAsync(id, body.Roles, ct);
            return Results.Ok(new { ok = true, roles = body.Roles });
        });

        admin.MapGet("/dependencias", async (DependenciasRepository repo, CancellationToken ct) =>
        {
            var rows = await repo.ListarAsync(ct: ct);
            return Results.Ok(new { dependencias = rows.Select(d => new { id = d.Id, codigo = d.Codigo, nombre = d.Nombre, activo = d.Activo }) });
        });

        admin.MapPost("/dependencias", async (CrearDependenciaAdminRequest body, DependenciasRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Codigo) || string.IsNullOrWhiteSpace(body.Nombre))
                return Results.BadRequest(new { error = "Código y nombre son obligatorios." });
            try
            {
                var id = await repo.CrearAsync(body.Codigo, body.Nombre, ct);
                return Results.Created($"/api/admin/dependencias/{id}", new { id });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2627 or 2601)
            {
                return Results.Conflict(new { error = "Ya existe una dependencia con ese código." });
            }
        });

        admin.MapGet("/lineas-tematicas", async (LineaTematicaRepository repo, CancellationToken ct) =>
        {
            var rows = await repo.ListarAsync(soloActivas: false, ct);
            return Results.Ok(new
            {
                lineas_tematicas = rows.Select(l => new
                {
                    id = l.Id,
                    codigo = l.Codigo,
                    nombre = l.Nombre,
                    descripcion = l.Descripcion,
                    activo = l.Activo
                })
            });
        });

        admin.MapPost("/lineas-tematicas", async (LineaTematicaApiRequest body, LineaTematicaRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Codigo) || string.IsNullOrWhiteSpace(body.Nombre))
                return Results.BadRequest(new { error = "Código y nombre son obligatorios." });
            try
            {
                var id = await repo.CrearAsync(body.Codigo, body.Nombre, body.Descripcion, ct);
                return Results.Created($"/api/admin/lineas-tematicas/{id}", new { id });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2627 or 2601)
            {
                return Results.Conflict(new { error = "Ya existe una línea temática con ese código." });
            }
        });

        admin.MapPut("/lineas-tematicas/{id:int}", async (int id, LineaTematicaApiRequest body, LineaTematicaRepository repo, CancellationToken ct) =>
        {
            if (await repo.GetAsync(id, ct) is null)
                return Results.NotFound(new { error = "Línea temática no encontrada." });
            try
            {
                await repo.ActualizarAsync(id, body.Codigo, body.Nombre, body.Descripcion, body.Activo, ct);
                return Results.Ok(new { ok = true });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2627 or 2601)
            {
                return Results.Conflict(new { error = "Ya existe otra línea temática con ese código." });
            }
        });

        admin.MapGet("/indicadores", async ([FromQuery] int? linea_tematica_id, IndicadorRepository repo, CancellationToken ct) =>
        {
            var rows = await repo.ListarAsync(linea_tematica_id, soloActivas: false, ct);
            return Results.Ok(new
            {
                indicadores = rows.Select(i => new
                {
                    id = i.Id,
                    linea_tematica_id = i.LineaTematicaId,
                    linea_tematica = i.LineaTematicaNombre,
                    codigo = i.Codigo,
                    nombre = i.Nombre,
                    descripcion = i.Descripcion,
                    activo = i.Activo
                })
            });
        });

        admin.MapPost("/indicadores", async (IndicadorApiRequest body, IndicadorRepository repo, LineaTematicaRepository lineas, CancellationToken ct) =>
        {
            if (body.LineaTematicaId <= 0 || string.IsNullOrWhiteSpace(body.Codigo) || string.IsNullOrWhiteSpace(body.Nombre))
                return Results.BadRequest(new { error = "Línea temática, código y nombre son obligatorios." });
            if (await lineas.GetAsync(body.LineaTematicaId, ct) is null)
                return Results.BadRequest(new { error = "Línea temática no encontrada." });
            try
            {
                var id = await repo.CrearAsync(body.LineaTematicaId, body.Codigo, body.Nombre, body.Descripcion, ct);
                return Results.Created($"/api/admin/indicadores/{id}", new { id });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2627 or 2601)
            {
                return Results.Conflict(new { error = "Ya existe un indicador con ese código en la línea temática." });
            }
        });

        admin.MapPut("/indicadores/{id:int}", async (int id, IndicadorApiRequest body, IndicadorRepository repo, CancellationToken ct) =>
        {
            if (await repo.GetAsync(id, ct) is null)
                return Results.NotFound(new { error = "Indicador no encontrado." });
            try
            {
                await repo.ActualizarAsync(id, body.LineaTematicaId, body.Codigo, body.Nombre, body.Descripcion, body.Activo, ct);
                return Results.Ok(new { ok = true });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2627 or 2601)
            {
                return Results.Conflict(new { error = "Ya existe otro indicador con ese código en la línea temática." });
            }
        });

        admin.MapGet("/areas-tematicas", async ([FromQuery] int? dependencia_id, AreaTematicaRepository repo, CancellationToken ct) =>
        {
            var rows = await repo.ListarAsync(dependencia_id, ct);
            return Results.Ok(new
            {
                areas = rows.Select(a => new
                {
                    id = a.Id,
                    dependencia_id = a.DependenciaId,
                    dependencia = a.DependenciaNombre,
                    codigo = a.Codigo,
                    nombre = a.Nombre,
                    activo = a.Activo
                })
            });
        });

        admin.MapPost("/areas-tematicas", async (CrearAreaTematicaRequest body, AreaTematicaRepository repo, CancellationToken ct) =>
        {
            if (body.DependenciaId <= 0 || string.IsNullOrWhiteSpace(body.Codigo) || string.IsNullOrWhiteSpace(body.Nombre))
                return Results.BadRequest(new { error = "Dependencia, código y nombre son obligatorios." });
            try
            {
                var id = await repo.CrearAsync(body.DependenciaId, body.Codigo, body.Nombre, body.Descripcion, ct);
                return Results.Created($"/api/admin/areas-tematicas/{id}", new { id });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2627 or 2601)
            {
                return Results.Conflict(new { error = "Ya existe el área temática en esa dependencia." });
            }
        });

        admin.MapPost("/areas-tematicas/importar-excel", async (AreasTematicasSeedService seed, CancellationToken ct) =>
        {
            var r = await seed.ImportarSiExisteAsync(repoRoot, ct);
            return r.Ok ? Results.Ok(r) : Results.BadRequest(r);
        });

        admin.MapGet("/roles", async (RolesRepository repo, CancellationToken ct) =>
        {
            var rows = await repo.ListarAsync(ct);
            return Results.Ok(new { roles = rows.Select(r => new { id = r.Id, nombre = r.Nombre, descripcion = r.Descripcion }) });
        });

        admin.MapGet("/plantillas", async (PlantillasRepository repo, CancellationToken ct) =>
        {
            var rows = await repo.ListarAsync(ct);
            return Results.Ok(new
            {
                plantillas = rows.Select(p => new
                {
                    id = p.Id,
                    codigo = p.Codigo,
                    nombre = p.Nombre,
                    descripcion = p.Descripcion,
                    dependencia_id = p.DependenciaId,
                    dependencia = p.DependenciaNombre,
                    activo = p.Activo,
                    total_campos = p.TotalCampos
                })
            });
        });

        admin.MapPost("/plantillas", async (PlantillaApiRequest body, PlantillasRepository repo, CancellationToken ct) =>
        {
            var id = await repo.CrearAsync(new PlantillaUpsert(body.Codigo, body.Nombre, body.Descripcion, body.DependenciaId, body.Activo), ct);
            return Results.Created($"/api/admin/plantillas/{id}", new { id });
        });

        admin.MapPut("/plantillas/{id:int}", async (int id, PlantillaApiRequest body, PlantillasRepository repo, CancellationToken ct) =>
        {
            await repo.ActualizarAsync(id, new PlantillaUpsert(body.Codigo, body.Nombre, body.Descripcion, body.DependenciaId, body.Activo), ct);
            return Results.Ok(new { ok = true });
        });

        admin.MapGet("/plantillas/{id:int}/campos", async (int id, PlantillasRepository repo, CancellationToken ct) =>
        {
            var campos = await repo.ListarCamposAsync(id, ct);
            return Results.Ok(new
            {
                campos = campos.Select(c => new
                {
                    id = c.Id,
                    nombre_campo = c.NombreCampo,
                    tipo_dato = c.TipoDato,
                    obligatorio = c.Obligatorio,
                    descripcion = c.Descripcion,
                    longitud = c.Longitud,
                    formato = c.Formato,
                    valores_permitidos = c.ValoresPermitidos,
                    orden = c.Orden
                })
            });
        });

        admin.MapPost("/plantillas/{id:int}/campos", async (int id, CampoPlantillaApiRequest body, PlantillasRepository repo, CancellationToken ct) =>
        {
            var cid = await repo.CrearCampoAsync(id, new CampoPlantillaUpsert(
                body.NombreCampo, body.TipoDato, body.Obligatorio, body.Descripcion,
                body.Longitud, body.Formato, body.ValoresPermitidos, body.Orden), ct);
            return Results.Created($"/api/admin/plantillas/{id}/campos/{cid}", new { id = cid });
        });

        admin.MapDelete("/plantillas/campos/{campoId:int}", async (int campoId, PlantillasRepository repo, CancellationToken ct) =>
        {
            await repo.EliminarCampoAsync(campoId, ct);
            return Results.Ok(new { ok = true });
        });
    }

    public static void MapDashboardApi(this RouteGroupBuilder secured)
    {
        secured.MapGet("/dashboard/resumen", async (DashboardRepository repo, HttpContext http, CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var dep = user.EsAdministrador || user.EsValidador ? (int?)null : user.DependenciaId;
            int? usuarioFiltro = user.PuedeVerTodosLosArchivos ? null : user.UsuarioId;
            var r = await repo.ObtenerResumenAsync(dep, usuarioFiltro, ct);
            return Results.Ok(new
            {
                total_archivos = r.TotalArchivos,
                cargas_pendientes = r.CargasPendientes,
                cargas_con_error = r.CargasConError,
                cargas_aprobadas = r.CargasAprobadas,
                ultimos_cargues = r.UltimosCargues.Select(u => new
                {
                    id = u.Id,
                    dependencia = u.Dependencia,
                    estado = u.Estado,
                    archivo = u.Archivo,
                    fecha = u.Fecha,
                    usuario = u.Usuario
                })
            });
        });
    }
}

public sealed record CrearUsuarioAdminRequest(
    string NombreUsuario, string Password, string? Email, int? DependenciaId, int? LineaTematicaId,
    IReadOnlyList<string>? Roles, IReadOnlyList<int>? AreasTematicasIds);
public sealed record CrearDependenciaAdminRequest(string Codigo, string Nombre);
public sealed record LineaTematicaApiRequest(string Codigo, string Nombre, string? Descripcion, bool Activo = true);
public sealed record IndicadorApiRequest(int LineaTematicaId, string Codigo, string Nombre, string? Descripcion, bool Activo = true);
public sealed record CrearAreaTematicaRequest(int DependenciaId, string Codigo, string Nombre, string? Descripcion);
public sealed record ActualizarUsuarioApiRequest(string? Email, int? DependenciaId, int? LineaTematicaId, string? Password, IReadOnlyList<string>? Roles);
public sealed record ActivarRequest(bool Activo);
public sealed record AsignarRolesRequest(IReadOnlyList<string> Roles);
public sealed record PlantillaApiRequest(string Codigo, string Nombre, string? Descripcion, int? DependenciaId, bool Activo);
public sealed record CampoPlantillaApiRequest(string NombreCampo, string TipoDato, bool Obligatorio,
    string? Descripcion, int? Longitud, string? Formato, string? ValoresPermitidos, int Orden);
