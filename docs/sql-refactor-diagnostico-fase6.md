# SQL Refactor Fase 6 — Administración y catálogo validación

## Objetivo
Migrar líneas temáticas, indicadores de aplicación, plantillas, áreas temáticas, vínculo `ArchivoCarga` y auditoría a vistas + stored procedures. Los repositorios C# quedan **solo con llamadas a SP** (sin SQL embebido ni comprobación de existencia de tablas).

El catálogo para validación Excel reutiliza `usp_Catalogo_Departamentos_Listar` y `usp_Catalogo_Municipios_Listar` (fase 4).

## Script SQL

`scripts/sql-refactor-fase6-admin-catalogos.sql`

### Vistas
- `vw_LineaTematica_Listado`
- `vw_Indicador_Listado`
- `vw_Plantilla_Listado`
- `vw_AreaTematica_Listado`

### Procedimientos — Línea temática
| SP | Uso |
|----|-----|
| `usp_LineaTematica_Listar` | `@SoloActivas` |
| `usp_LineaTematica_Obtener` | Por id |
| `usp_LineaTematica_Crear` / `Actualizar` | ABM |
| `usp_LineaTematica_ContarIndicadores` | Antes de desactivar |

### Procedimientos — Indicador (app)
| SP | Uso |
|----|-----|
| `usp_Indicador_Listar` | Filtro línea + activos (incluye línea activa) |
| `usp_Indicador_Obtener` | Detalle |
| `usp_Indicador_ObtenerColumnasObligatoriasJson` | Plantilla OSC |
| `usp_Indicador_PerteneceALinea` | Validación carga |
| `usp_Indicador_Crear` / `Actualizar` | ABM admin |

### Procedimientos — Plantillas
| SP | Uso |
|----|-----|
| `usp_Plantilla_Listar` / `Crear` / `Actualizar` | ABM plantillas |
| `usp_Plantilla_Campos_Listar` | Campos por plantilla |
| `usp_Plantilla_Campo_Crear` / `Eliminar` | Campos |

### Procedimientos — Área temática
| SP | Uso |
|----|-----|
| `usp_AreaTematica_Listar` | `@DependenciaId` opcional |
| `usp_AreaTematica_Crear` | Alta |

### Procedimientos — ArchivoCarga y auditoría
| SP | Uso |
|----|-----|
| `usp_ArchivoCarga_Sincronizar` | Insert/update vínculo área/plantilla |
| `usp_ArchivoCarga_ActualizarEstadoPorCarga` | Estado según `CargasArchivo.Id` |
| `usp_Auditoria_Registrar` | Log de acciones |
| `usp_Auditoria_Listar` | `@Top` (1–5000) |

## Backend (solo SP)

| Archivo | SP principales |
|---------|----------------|
| `LineaTematicaRepository.cs` | `usp_LineaTematica_*` |
| `IndicadorRepository.cs` | `usp_Indicador_*` |
| `PlantillasRepository.cs` | `usp_Plantilla_*` |
| `AreaTematicaRepository.cs` | `usp_AreaTematica_*` |
| `ArchivoCargaRepository.cs` | `usp_ArchivoCarga_*` |
| `AuditoriaRepository.cs` | `usp_Auditoria_*` |
| `CatalogoRepository.cs` | `usp_Catalogo_Departamentos_Listar`, `usp_Catalogo_Municipios_Listar` |

**Endpoints:** `AdminEndpoints.cs`, `ApiEndpoints.cs` (`/lineas-tematicas`, `/indicadores`, `/auditoria`).

**Servicios:** `CargaArchivoService` (catálogo + archivo carga + indicador + auditoría), `ArchivoFlujoService`, `ArchivoPrevalidacionService`, `AreasTematicasSeedService`.

## Despliegue

```powershell
sqlcmd -S "localhost\SQLEXPRESS2025" -d "ObservatorioDB" -E -b -i "scripts\sql-refactor-fase6-admin-catalogos.sql"
dotnet build backend\Observatorios.Api\Observatorios.Api.csproj
.\scripts\publicar-iis.ps1   # opcional
```

## Validación sugerida (8081)

| Prueba | Ruta |
|--------|------|
| Líneas activas | `GET /api/lineas-tematicas` |
| Indicadores por línea | `GET /api/indicadores?linea_tematica_id=1` |
| Admin líneas | `GET /api/admin/lineas-tematicas` |
| Plantillas | `GET /api/admin/plantillas` |
| Áreas | `GET /api/admin/areas-tematicas` |
| Auditoría | `GET /api/auditoria` |
| Carga Excel | `POST /api/cargas/procesar` (usa catálogo dim) |

## Qué sigue en C#
- Validación Excel (`ExcelValidationService`, `OscPlantillaValidacionService`)
- Autorización y flujos (`CargaArchivoService`, `ArchivoFlujoService`)
- BCrypt y bootstrap (`ObservatorioDbSchema` en arranque, si aplica)
