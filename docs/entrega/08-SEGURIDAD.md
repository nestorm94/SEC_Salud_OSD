# 08 — Seguridad

**Observatorio OSD — Casanare**  
Versión 1.0 — Julio 2026

---

## 1. Modelo de seguridad

| Capa | Mecanismo |
|------|-----------|
| Autenticación | JWT Bearer |
| Contraseñas | BCrypt (hash en `Usuarios.PasswordHash`) |
| Autorización | Roles en claims + `UserContext` en API |
| Transporte | HTTPS recomendado en producción |
| Archivos | Disco local `uploads/` — acceso solo vía API autenticada |

---

## 2. Autenticación JWT

### Configuración (`appsettings.json`)

```json
"Jwt": {
  "Key": "<secreto-minimo-32-caracteres>",
  "Issuer": "Observatorios.Api",
  "Audience": "Observatorios.Front",
  "ExpireMinutes": 720
}
```

| Parámetro | Valor actual | Descripción |
|-----------|--------------|-------------|
| `ExpireMinutes` | 720 (12 h) | Duración del token |
| `ClockSkew` | 2 min | Tolerancia de reloj en validación |

### Flujo de login

1. `POST /api/auth/login` con usuario y contraseña.
2. API verifica BCrypt y emite JWT con claims: `sub`, `name`, `role`, `dependencia_id`, `linea_tematica_id`, `areas`.
3. Frontend guarda token en `localStorage` (`observatorios.token`).

### Renovación automática (sesión activa)

| Componente | Función |
|------------|---------|
| `POST /api/auth/refresh` | Emite nuevo JWT sin pedir contraseña |
| `SessionKeepAliveService` | Renueva si quedan < 90 min y hay actividad |
| `apiErrorInterceptor` | Ante 401, intenta refresh y reintenta la petición |

**Inactividad:** si no hay actividad por ~45 min, no se renueva; tras expiración total debe volver a iniciar sesión.

---

## 3. Roles y permisos

### Roles del sistema

| Rol | Código | Capacidades principales |
|-----|--------|-------------------------|
| Administrador | `ADMIN`, `Administrador` | Todo + `/api/admin/*` |
| Validador | `VALIDADOR` | Aprobar/rechazar cargues; ver todos los archivos |
| Coordinador dependencia | `COORDINADOR_DEPENDENCIA` | Cargar archivos de su dependencia |
| Responsable temático | `RESPONSABLE_TEMATICO` | Cargar en líneas asignadas |
| Consulta | `CONSULTA` | Solo lectura |
| Auditor | `AUDITOR` | Log de auditoría |

### Matriz resumida

| Acción | ADMIN | VALIDADOR | Coordinador | Responsable | CONSULTA |
|--------|-------|-----------|-------------|-------------|----------|
| Cargar Excel | ✓ | — | ✓ | ✓ | — |
| Aprobar cargue | ✓ | ✓ | — | — | — |
| Ver todos los archivos | ✓ | ✓ | — | — | — |
| Admin usuarios | ✓ | — | — | — | — |
| Consultar indicadores | ✓ | ✓ | ✓ | ✓ | ✓ |
| Auditoría | ✓ | — | — | — | — (AUDITOR sí) |

### Alcance por dependencia

- Usuarios no admin/validador ven solo archivos de **su dependencia** o los que ellos subieron.
- `linea_tematica_id` en el token limita líneas temáticas disponibles.

---

## 4. Endpoints públicos

Únicamente consultas de indicadores para dashboards externos:

```
GET /api/public/indicadores/prostata
```

**No son públicos:** login, usuarios, cargues, administración, eliminación de datos.

---

## 5. Frontend — Protección de rutas

| Guard | Rutas |
|-------|-------|
| `authGuard` | Todas excepto `/login` |
| `adminGuard` | `/administracion/*` |

Token expirado → redirección a `/login?expired=1`.

---

## 6. Buenas prácticas — Producción

| Práctica | Acción |
|----------|--------|
| Cambiar `Jwt:Key` | Clave única ≥ 32 caracteres; no commitear |
| Cambiar contraseña admin | Eliminar `Admin123*` |
| HTTPS | Certificado en IIS |
| `SkipSchemaBootstrap` | true — no crear admin automático en prod |
| Secretos | `dotnet user-secrets` o variables de entorno |
| Backups | BD y `uploads/` periódicos |
| Swagger | Restringir acceso en producción si se expone a internet |

Ver: [docs/CONFIGURACION-SECRETOS.md](../CONFIGURACION-SECRETOS.md)

---

## 7. Datos sensibles

| Dato | Ubicación | Protección |
|------|-----------|------------|
| Contraseñas | SQL `Usuarios` | Hash BCrypt |
| JWT | localStorage navegador | Solo HTTPS en prod |
| Archivos Excel | `uploads/` | API + permisos por rol |
| Connection string | appsettings / secrets | No en repositorio público |

**No incluir en Git:** `temp_login.json`, `.env` con secretos, backups `.bak` con datos reales.

---

## 8. Validación de archivos

- Validación de estructura OSC (diccionario + DATA).
- Validación geográfica contra `dim_departamento` / `dim_municipios` cuando el Excel trae columnas territoriales.
- Los catálogos oficiales **no se modifican** por la validación; solo se reportan errores.

---

## 9. Auditoría

Tabla `AuditoriaSistema` + endpoint `GET /api/auditoria`.

Registra acciones administrativas para trazabilidad institucional.

---

## 10. Incidentes de seguridad — Respuesta

| Incidente | Acción |
|-----------|--------|
| Token comprometido | Rotar `Jwt:Key` (invalida todos los tokens) |
| Usuario no autorizado | Inactivar cuenta en administración |
| Archivo malicioso | Revisar validación; no ejecutar macros en servidor |
| Acceso no autorizado a BD | Revisar permisos SQL e IIS app pool |

---

*Seguridad — Observatorio de Salud Departamental Casanare.*
