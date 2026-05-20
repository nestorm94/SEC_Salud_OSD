using Microsoft.AspNetCore.Mvc;
using Observatorios.Api.Auth;
using Observatorios.Api.Data;

namespace Observatorios.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminApi(this RouteGroupBuilder secured)
    {
        var admin = secured.MapGroup("/admin").RequireAuthorization(AuthExtensions.PolicyAdmin);

        admin.MapGet("/dashboard", async (DashboardRepository repo, HttpContext http, CancellationToken ct) =>
        {
            var user = http.GetUserContext()!;
            var dep = user.EsAdministrador ? (int?)null : user.DependenciaId;
            var r = await repo.ObtenerResumenAsync(dep, ct);
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
                    fecha = u.Fecha
                })
            });
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
                roles = u.Roles
            });
        });

        admin.MapPut("/usuarios/{id:int}", async (int id, ActualizarUsuarioApiRequest body, UsuariosRepository repo, CancellationToken ct) =>
        {
            await repo.ActualizarAsync(id, new ActualizarUsuarioRequest(body.Email, body.DependenciaId, body.Password), ct);
            if (body.Roles?.Count > 0)
                await repo.ActualizarRolesAsync(id, body.Roles, ct);
            return Results.Ok(new { ok = true });
        });

        admin.MapPatch("/usuarios/{id:int}/activo", async (int id, ActivarRequest body, UsuariosRepository repo, CancellationToken ct) =>
        {
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
            var dep = user.EsAdministrador ? (int?)null : user.DependenciaId;
            var r = await repo.ObtenerResumenAsync(dep, ct);
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
                    fecha = u.Fecha
                })
            });
        });
    }
}

public sealed record ActualizarUsuarioApiRequest(string? Email, int? DependenciaId, string? Password, IReadOnlyList<string>? Roles);
public sealed record ActivarRequest(bool Activo);
public sealed record AsignarRolesRequest(IReadOnlyList<string> Roles);
public sealed record PlantillaApiRequest(string Codigo, string Nombre, string? Descripcion, int? DependenciaId, bool Activo);
public sealed record CampoPlantillaApiRequest(string NombreCampo, string TipoDato, bool Obligatorio,
    string? Descripcion, int? Longitud, string? Formato, string? ValoresPermitidos, int Orden);
