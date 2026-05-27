# SQL Refactor Fase 2 — Usuarios, roles y dependencias

Migración incremental con fallback: si el `usp_` no existe en la BD, el repositorio usa SQL legacy.

## Script SQL

`scripts/sql-refactor-fase2-usuarios-roles-dependencias.sql`

### Vistas (`vw_`)

| Vista | Uso |
|-------|-----|
| `vw_Usuarios_Auth` | Login (incluye `PasswordHash`) |
| `vw_Usuarios_Listado` | Listado admin con `RolesCsv` agregado |
| `vw_Roles_Listado` | Catálogo de roles |
| `vw_Dependencias_Listado` | Catálogo de dependencias |

### Procedimientos (`usp_`)

| Procedimiento | Operación |
|---------------|-----------|
| `usp_Usuario_ObtenerPorNombre` | Login por usuario |
| `usp_Usuario_ObtenerPorEmail` | Login por correo |
| `usp_Usuario_ObtenerPorId` | Detalle usuario + roles |
| `usp_Usuario_Listar` | Listado usuarios |
| `usp_Usuario_ObtenerRoles` | Roles de un usuario |
| `usp_Usuario_ObtenerAreasTematicas` | Áreas temáticas asignadas |
| `usp_Usuario_Crear` | Alta usuario + roles (`@PasswordHash` desde C#) |
| `usp_Usuario_Actualizar` | Actualizar perfil / contraseña |
| `usp_Usuario_SetActivo` | Activar / desactivar |
| `usp_Usuario_ActualizarRoles` | Reemplazar roles (`@RolesCsv`) |
| `usp_Usuario_ActualizarAreasTematicas` | Reemplazar áreas (`@AreaIdsCsv`) |
| `usp_Roles_Listar` | Listar roles |
| `usp_Dependencia_Listar` | Listar dependencias |
| `usp_Dependencia_ObtenerPorId` | Detalle dependencia |
| `usp_Dependencia_Crear` | Crear dependencia |
| `usp_Dependencia_ObtenerOCrear` | Upsert por código |

**Nota:** BCrypt sigue en C#; el SP recibe `@PasswordHash` ya calculado.

## Backend modificado

| Archivo | Cambio |
|---------|--------|
| `Data/SqlProcHelper.cs` | Helper compartido (`StoredProcedureExisteAsync`, CSV roles/áreas) |
| `Data/UsuariosRepository.cs` | Consume `usp_Usuario_*` con fallback |
| `Data/RolesRepository.cs` | Consume `usp_Roles_Listar` con fallback |
| `Data/DependenciasRepository.cs` | Consume `usp_Dependencia_*` con fallback |

## Endpoints impactados

- `POST /api/auth/login`
- `GET /api/auth/me`
- `GET/POST /api/admin/usuarios*`
- `GET /api/admin/roles`
- `GET/POST /api/dependencias`, `GET/POST /api/admin/dependencias`

## Despliegue

```powershell
sqlcmd -S "localhost\SQLEXPRESS2025" -d "ObservatorioDB" -E -b -i "scripts\sql-refactor-fase2-usuarios-roles-dependencias.sql"
.\scripts\publicar-iis.ps1
```

## Validación sugerida

1. Login: `admin` / `Admin123*`
2. `GET /api/admin/usuarios` (token admin)
3. `GET /api/admin/roles`
4. `GET /api/dependencias`
