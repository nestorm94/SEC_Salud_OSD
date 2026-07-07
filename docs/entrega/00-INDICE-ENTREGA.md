# Índice de entrega — Observatorio de Salud Departamental Casanare (OSD)

| Campo | Valor |
|-------|-------|
| **Proyecto** | Observatorios Salud Departamental Casanare |
| **Cliente** | Gobernación de Casanare — Secretaría de Salud Departamental |
| **Repositorio** | [nestorm94/SEC_Salud_OSD](https://github.com/nestorm94/SEC_Salud_OSD) |
| **Versión documentación** | 1.0 |
| **Fecha** | Julio 2026 |

---

## Paquete de entrega

Este directorio contiene la documentación oficial para la **entrega final** del Observatorio OSD. Los documentos están organizados por audiencia: institucional, operativa y técnica.

| # | Documento | Audiencia | Descripción |
|---|-----------|-----------|-------------|
| 01 | [Memoria descriptiva](01-MEMORIA-DESCRIPTIVA.md) | Comité, dirección, contratación | Qué es el sistema, alcance, beneficios |
| 02 | [Manual de usuario](02-MANUAL-USUARIO.md) | Operadores, validadores | Uso diario: cargues, consultas, descargas |
| 03 | [Manual de administrador](03-MANUAL-ADMINISTRADOR.md) | Administradores TI / OSD | Usuarios, roles, catálogos, plantillas |
| 04 | [Manual técnico](04-MANUAL-TECNICO.md) | Equipo de sistemas | Instalación, despliegue, configuración |
| 05 | [Arquitectura](05-ARQUITECTURA.md) | Desarrolladores, arquitectos | Capas, tecnologías, flujos |
| 06 | [Base de datos](06-BASE-DE-DATOS.md) | DBA, desarrolladores backend | Tablas, vistas, SPs, scripts SQL |
| 07 | [API — Endpoints](07-API-ENDPOINTS.md) | Integradores, desarrolladores | Contratos REST, ejemplos |
| 08 | [Seguridad](08-SEGURIDAD.md) | TI, auditoría | JWT, roles, permisos, buenas prácticas |
| 09 | [Pruebas y validación](09-PRUEBAS-Y-VALIDACION.md) | QA, recepción | Matriz de pruebas, evidencias |
| 10 | [Operación y soporte](10-OPERACION-Y-SOPORTE.md) | Operaciones, soporte N2 | Backups, monitoreo, incidentes |
| 11 | [Acta de entrega](11-ACTA-ENTREGA.md) | Formalización | Checklist firmable de recepción |

### Anexos

| Carpeta | Contenido |
|---------|-----------|
| [imagenes/](imagenes/README.md) | Capturas de pantalla para manuales |
| [postman/](postman/README.md) | Colección Postman importable |

---

## Documentación complementaria (repositorio)

| Ubicación | Contenido |
|-----------|-----------|
| [README.md](../../README.md) | Inicio rápido para desarrolladores |
| [docs/ARQUITECTURA.md](../ARQUITECTURA.md) | Arquitectura técnica (versión anterior) |
| [docs/SQL-SERVER-CATALOGO-OBJETOS.md](../SQL-SERVER-CATALOGO-OBJETOS.md) | Catálogo detallado vw_/usp_ |
| [scripts/README-SQL-REFACTOR.md](../../scripts/README-SQL-REFACTOR.md) | Orden de despliegue SQL fases 1–7 |
| [docs/CONFIGURACION-SECRETOS.md](../CONFIGURACION-SECRETOS.md) | Secretos y variables sensibles |
| [frontend/DESIGN-SYSTEM.md](../../frontend/DESIGN-SYSTEM.md) | Sistema de diseño Angular |
| [docs/ASIS-FASE1-matriz-reutilizacion.md](../ASIS-FASE1-matriz-reutilizacion.md) | Matriz módulo ASIS |
| [docs/FASE0-clon-observatoriodb-asis-test.md](../FASE0-clon-observatoriodb-asis-test.md) | Ambiente de pruebas ASIS |

---

## Componentes entregados

| Componente | Ubicación | Estado |
|------------|-----------|--------|
| API ASP.NET Core | `backend/Observatorios.Api/` | Operativo |
| Frontend Angular 19 | `frontend/` | Operativo (recomendado) |
| Portal HTML legacy | `public/` | Deprecado, respaldo |
| Scripts SQL | `scripts/` | Documentados |
| Pruebas automatizadas | `backend/Observatorios.Api.Tests/` | Disponible |
| Despliegue IIS | `scripts/publicar-iis.ps1` | Documentado |

---

## Ambientes

| Ambiente | URL típica | Base de datos |
|----------|------------|---------------|
| Desarrollo (API) | `http://localhost:5289` | `ObservatorioDB` o `ObservatorioDB_ASIS_Test` |
| Desarrollo (Angular) | `http://localhost:4200` | Proxy → API |
| Producción / IIS | `http://localhost:8081` | `ObservatorioDB` |
| Laboratorio ASIS | Misma API, BD distinta | `ObservatorioDB_ASIS_Test` |

---

## Cómo exportar a PDF

1. Abrir cada archivo `.md` en VS Code / Cursor con vista previa.
2. Imprimir → Guardar como PDF, **o**
3. Usar [Pandoc](https://pandoc.org/): `pandoc 01-MEMORIA-DESCRIPTIVA.md -o memoria.pdf`
4. Para entrega formal: unir 01 + 02 + 03 en **Manual operativo** y 04 + 05 + 06 + 07 + 08 en **Anexo técnico**.

---

## Control de versiones del documento

| Versión | Fecha | Cambios |
|---------|-------|---------|
| 1.0 | Jul 2026 | Entrega inicial consolidada |
