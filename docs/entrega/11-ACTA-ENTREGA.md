# 11 — Acta de entrega

**Observatorio de Salud Departamental — Casanare (OSD)**

---

## Datos de la entrega

| Campo | Valor |
|-------|-------|
| **Nombre del sistema** | Observatorios Salud Departamental Casanare |
| **Versión entregada** | 1.0 |
| **Fecha de entrega** | 8 de julio de 2026 |
| **Repositorio** | https://github.com/nestorm94/SEC_Salud_OSD |
| **Commit / tag** | `8c1b8e9` (rama `cursor/asis-nacimientos-looker`) |
| **Entrega realizada por** | Equipo desarrollo OSD |
| **Recibido por** | ______________________ |
| **Entidad** | Gobernación de Casanare — Secretaría de Salud Departamental |

---

## 1. Objeto de la entrega

Se hace entrega formal de la plataforma web **Observatorio OSD**, incluyendo código fuente, documentación, scripts de base de datos y componentes necesarios para su operación en el ambiente de la Secretaría de Salud de Casanare.

---

## 2. Componentes entregados

| # | Componente | Entregado | Observaciones |
|---|------------|-----------|---------------|
| 1 | Código fuente (API .NET) | ☑ Sí ☐ No | `backend/Observatorios.Api/` |
| 2 | Código fuente (Angular) | ☑ Sí ☐ No | `frontend/` |
| 3 | Scripts SQL | ☑ Sí ☐ No | `scripts/` |
| 4 | Documentación entrega | ☑ Sí ☐ No | `docs/entrega/` |
| 5 | Manual de usuario | ☑ Sí ☐ No | Doc. 02 |
| 6 | Manual técnico | ☑ Sí ☐ No | Doc. 04 |
| 7 | Base de datos configurada | ☑ Sí ☐ No | `ObservatorioDB` / ASIS Test |
| 8 | Sitio IIS operativo | ☑ Sí ☐ No | Puerto **8081** |
| 9 | Usuarios iniciales | ☑ Sí ☐ No | Admin creado |
| 10 | Pruebas de aceptación | ☑ Sí ☐ No | Doc. 09 (smoke 2026-07-08) |

---

## 3. Ambiente de entrega

| Parámetro | Valor |
|-----------|-------|
| **Servidor** | localhost (desarrollo / pre-producción) |
| **URL aplicación** | http://localhost:8081/ |
| **Instancia SQL Server** | localhost\SQLEXPRESS2025 |
| **Base de datos** | ObservatorioDB / ObservatorioDB_ASIS_Test |
| **Ruta IIS** | C:\Hosting\ObservatorioOSD |

---

## 4. Alcance funcional entregado

| Módulo | Incluido | Notas |
|--------|----------|-------|
| Autenticación JWT + renovación sesión | ☑ | Refresh token + keepalive |
| Dashboard | ☑ | |
| Cargue y validación Excel OSC | ☑ | |
| Flujo aprobación cargues | ☑ | |
| Proyección población | ☑ | |
| Módulo ASIS departamental | ☑ | |
| Exportación Excel ASIS | ☑ | Nacimientos y mortalidad |
| Indicador próstata + API pública | ☑ | |
| Administración (usuarios, roles, plantillas) | ☑ | |
| Auditoría | ☑ | |

---

## 5. Fuera de alcance / pendientes

| Ítem | Estado | Comentario |
|------|--------|------------|
| Generación documento ASIS Word/PDF | Pendiente | |
| Notificaciones por correo | Pendiente | |
| Migración capa `fact` población a producción | Pendiente | En ASIS Test |
| App móvil | No contemplado | |

---

## 6. Pruebas de aceptación

| Resultado | ☑ Aprobado ☐ Aprobado con observaciones ☐ Rechazado |
|-----------|-----------------------------------------------------|

**Matriz de pruebas:** ver [09-PRUEBAS-Y-VALIDACION.md](09-PRUEBAS-Y-VALIDACION.md)

**Observaciones de pruebas:**

```
Smoke test 2026-07-08: dotnet test 13/13, health/ping OK, login+refresh OK, export Excel ASIS OK.
Casos manuales pendientes (A2, A6, A8, AS3, C*, V*, P*) marcados ⏸ en matriz.
Pendiente firma de recepción por Secretaría de Salud.
```

---

## 7. Capacitación

| Tema | Fecha | Asistentes | Instructor |
|------|-------|------------|------------|
| Uso operativo (cargues) | | | |
| Validación y aprobación | | | |
| Administración | | | |
| Soporte técnico | | | |

---

## 8. Documentación entregada

| Documento | Ubicación |
|-----------|-----------|
| Índice de entrega | `docs/entrega/00-INDICE-ENTREGA.md` |
| Memoria descriptiva | `docs/entrega/01-MEMORIA-DESCRIPTIVA.md` |
| Manual usuario | `docs/entrega/02-MANUAL-USUARIO.md` |
| Manual administrador | `docs/entrega/03-MANUAL-ADMINISTRADOR.md` |
| Manual técnico | `docs/entrega/04-MANUAL-TECNICO.md` |
| Arquitectura | `docs/entrega/05-ARQUITECTURA.md` |
| Base de datos | `docs/entrega/06-BASE-DE-DATOS.md` |
| API | `docs/entrega/07-API-ENDPOINTS.md` |
| Seguridad | `docs/entrega/08-SEGURIDAD.md` |
| Pruebas | `docs/entrega/09-PRUEBAS-Y-VALIDACION.md` |
| Operación | `docs/entrega/10-OPERACION-Y-SOPORTE.md` |

---

## 9. Compromisos post-entrega

| Compromiso | Responsable | Plazo |
|------------|-------------|-------|
| Soporte correctivo (defectos) | | _____ días |
| Ajustes menores documentados | | |
| Transferencia conocimiento DBA | | |

---

## 10. Firmas

### Entrega

| | |
|---|---|
| **Nombre:** | |
| **Cargo:** | |
| **Firma:** | |
| **Fecha:** | |

### Recepción — Secretaría de Salud de Casanare

| | |
|---|---|
| **Nombre:** | |
| **Cargo:** | |
| **Firma:** | |
| **Fecha:** | |

### Vo.Bo. — Líder técnico / TI

| | |
|---|---|
| **Nombre:** | |
| **Cargo:** | |
| **Firma:** | |
| **Fecha:** | |

---

*Acta de entrega — Observatorio de Salud Departamental Casanare.*
