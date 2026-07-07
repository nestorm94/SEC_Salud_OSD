# 07 — API — Endpoints REST

**Observatorio OSD**  
Versión 1.0 — Julio 2026  
Base URL: `/api`

---

## Leyenda de autenticación

| Símbolo | Significado |
|---------|-------------|
| 🔓 | Público (sin JWT) |
| 🔒 | JWT requerido |
| 👑 | JWT + rol Administrador |
| ✓ | JWT + rol Validador (o Admin) |
| ↑ | JWT + permiso de cargue |

---

## 1. Salud y diagnóstico

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/health` | 🔓 | Liveness |
| GET | `/health/db` | 🔓 | Conexión SQL |
| GET | `/api/ping` | 🔓 | API activa |
| GET | `/api/salud` | 🔓 | Metadatos servicio |

---

## 2. Autenticación

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/api/auth/login` | 🔓 | Login — body: `{ usuario, password }` |
| GET | `/api/auth/me` | 🔒 | Perfil actual |
| POST | `/api/auth/refresh` | 🔒 | Renovar JWT |

### Ejemplo login

```http
POST /api/auth/login
Content-Type: application/json

{
  "usuario": "admin@observatorio.gov.co",
  "password": "Admin123*"
}
```

Respuesta:

```json
{
  "token": "eyJ...",
  "usuario": {
    "id": 1,
    "nombre": "admin",
    "email": "admin@observatorio.gov.co",
    "dependencia_id": null,
    "dependencia": null,
    "roles": ["ADMIN", "Administrador"]
  }
}
```

---

## 3. Dashboard

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/dashboard/resumen` | 🔒 | KPIs y últimos cargues |
| GET | `/api/admin/dashboard` | 👑 | Resumen administrativo |

---

## 4. Archivos y cargues

### Archivos

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/archivos` | 🔒 | Listar archivos |
| GET | `/api/archivos/{id}` | 🔒 | Detalle |
| GET | `/api/archivos/{id}/descargar` | 🔒 | Descargar .xlsx |
| DELETE | `/api/archivos/{id}` | 🔒 | Eliminar |
| POST | `/api/archivos/validar` | ↑ | Validar Excel (multipart) |
| POST | `/api/archivos/enviar` | ↑ | Enviar tras validación |
| POST | `/api/archivos/{id}/enviar-y-aprobar` | ↑/✓ | Enviar y aprobar |
| POST | `/api/archivos/{id}/rechazar-validacion` | ✓ | Rechazar en validación |

### Cargas

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/api/cargas/upload` | ↑ | Subir cargue |
| POST | `/api/cargas/excel` | ↑ | Alias upload |
| GET | `/api/cargas` | 🔒 | Listar cargues |
| GET | `/api/cargas/mis-cargas` | 🔒 | Mis cargues |
| GET | `/api/cargas/pendientes-aprobacion` | ✓ | Cola aprobación |
| GET | `/api/cargas/{id}` | 🔒 | Detalle |
| GET | `/api/cargas/{id}/errores` | 🔒 | Errores validación |
| GET | `/api/cargas/historial` | 🔒 | Historial |
| POST | `/api/cargas/{id}/validar` | ✓ | Re-validar |
| POST | `/api/cargas/{id}/aprobar` | ✓ | Aprobar |
| POST | `/api/cargas/{id}/rechazar` | ✓ | Rechazar |

---

## 5. Catálogos (proyección población)

Base: `/api/catalogos` — todos 🔒

| Método | Ruta | Parámetros |
|--------|------|------------|
| GET | `/proyeccion` | Bundle completo |
| GET | `/departamentos` | — |
| GET | `/municipios` | `?codigoDepartamento=85` |
| GET | `/municipios/{codigoDepartamento}` | — |
| GET | `/regionales` | — |
| GET | `/areas` | — |
| GET | `/sexos` | — |
| GET | `/anios` | — |

---

## 6. Proyección población

Base: `/api/proyeccion-poblacion` — todos 🔒

| Método | Ruta | Query params |
|--------|------|--------------|
| GET | `/vistas` | — |
| GET | `/nacional-casanare` | `pagina`, `tamanoPagina`, filtros DANE |
| GET | `/curso-vida` | idem |
| GET | `/quinquenios` | idem |
| GET | `/{clave}/excel` | Exportación Excel |

---

## 7. ASIS departamental

Base: `/api/asis` — todos 🔒

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/vistas` | Catálogo de indicadores |
| GET | `/catalogos/proyecciones` | Proyecciones DANE |
| GET | `/catalogos/vigencias` | Años disponibles |
| GET | `/indicadores/{clave}` | Consulta paginada |
| GET | `/{clave}` | Alias legacy |
| GET | `/export/nacimientos/excel` | Excel nacimientos |
| GET | `/export/mortalidad/excel` | Excel defunciones |

### Claves ASIS válidas

`poblacion-total`, `poblacion-municipio`, `poblacion-sexo`, `poblacion-area`, `poblacion-grupo-edad`, `poblacion-curso-vida`, `piramide-poblacional`, `mortalidad-total`, `mortalidad-municipio`, `mortalidad-detalle`, `mortalidad-sexo`, `mortalidad-area`, `mortalidad-grupo-edad`, `mortalidad-curso-vida`, `nacimientos-total`, `nacimientos-municipio`, `nacimientos-detalle`, `nacimientos-sexo`, `nacimientos-area`, `nacimientos-grupo-edad`, `nacimientos-nivel-educativo`, `nacimientos-pertenencia-etnica`, `nacimientos-peso-al-nacer`, `nacimientos-semanas-gestacion`, `tasa-bruta-mortalidad`, `serie-mortalidad`, `comparativo-poblacion-mortalidad`

### Ejemplo consulta ASIS

```http
GET /api/asis/indicadores/poblacion-municipio?vigencia=2024&codigoMunicipio=85015&pagina=1&tamanoPagina=10
Authorization: Bearer {token}
```

---

## 8. Líneas temáticas e indicadores (operación)

| Método | Ruta | Auth |
|--------|------|------|
| GET | `/api/lineas-tematicas` | 🔒 |
| GET | `/api/indicadores?linea_tematica_id={id}` | 🔒 |
| GET | `/api/indicadores/prostata` | 🔒 |
| GET | `/api/dependencias` | 🔒 |

---

## 9. Administración (`/api/admin`)

Todos requieren 👑 salvo donde se indique.

| Recurso | GET | POST | PUT | DELETE | PATCH |
|---------|-----|------|-----|--------|-------|
| `/dashboard` | ✓ | — | — | — | — |
| `/usuarios` | ✓ | ✓ | — | — | — |
| `/usuarios/{id}` | ✓ | — | ✓ | ✓ | — |
| `/usuarios/{id}/activo` | — | — | — | — | ✓ |
| `/usuarios/{id}/roles` | — | — | ✓ | — | — |
| `/dependencias` | ✓ | ✓ | — | — | — |
| `/lineas-tematicas` | ✓ | ✓ | ✓ | — | — |
| `/indicadores` | ✓ | ✓ | ✓ | — | — |
| `/areas-tematicas` | ✓ | ✓ | — | — | — |
| `/areas-tematicas/importar-excel` | — | ✓ | — | — | — |
| `/roles` | ✓ | ✓ | ✓ | ✓ | — |
| `/plantillas` | ✓ | ✓ | ✓ | — | — |
| `/plantillas/{id}/campos` | ✓ | ✓ | — | — | — |
| `/plantillas/campos/{campoId}` | — | — | — | ✓ | — |

---

## 10. Auditoría

| Método | Ruta | Auth |
|--------|------|------|
| GET | `/api/auditoria` | 🔒 ADMIN o AUDITOR |

---

## 11. API pública (integraciones)

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| GET | `/api/public/indicadores/prostata` | 🔓 | Mortalidad próstata para Looker |

Parámetros opcionales: `codigoDane`, `territorio`, `regional`, `ano`, `area`, `pagina`, `tamanoPagina`.

> Solo los endpoints de **consulta de indicadores** son públicos. Login, cargues y administración siempre requieren JWT.

---

## 12. Códigos de respuesta

| Código | Significado |
|--------|-------------|
| 200 | OK |
| 400 | Validación / datos incorrectos |
| 401 | No autenticado o token expirado |
| 403 | Sin permiso para la acción |
| 404 | Recurso no encontrado |
| 502 | Error SQL Server |

---

## 13. Swagger

Documentación interactiva: `http://localhost:5289/swagger` (desarrollo).

---

*Referencia API — Observatorio OSD Casanare.*
