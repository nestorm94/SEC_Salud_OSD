# SQL Refactor Fase 1 (Proyecto actual OSD)

Este documento deja el diagnostico inicial y la estrategia de migracion incremental **sin romper funcionalidades existentes**.

## Resumen de la fase

- Se crea base para arquitectura `Controller -> Service -> Repository -> SQL (vw_/usp_)`.
- Se mantiene compatibilidad: si el `usp_` no existe aun, el backend sigue usando SQL legacy.
- No se crea proyecto ni solucion nueva.

## Diagnostico (inventario de SQL embebido)

Modulos/archivos principales con SQL en C#:

- `Data/UsuariosRepository.cs`
- `Data/RolesRepository.cs`
- `Data/DependenciasRepository.cs`
- `Data/LineaTematicaRepository.cs`
- `Data/IndicadorRepository.cs`
- `Data/IndicadoresRepository.cs`
- `Data/ArchivosRepository.cs`
- `Data/CargasRepository.cs`
- `Data/ArchivoCargaRepository.cs`
- `Data/PlantillasRepository.cs`
- `Data/AreaTematicaRepository.cs`
- `Data/DashboardRepository.cs`
- `Data/AuditoriaRepository.cs`
- `Data/PoblacionVistasRepository.cs`
- `Data/PoblacionCatalogosRepository.cs`

Servicios con logica de datos/validacion en backend:

- `Services/OscPlantillaValidacionService.cs`
- `Services/ExcelValidationService.cs`
- `Services/GeografiaValidacionService.cs`
- `Services/ArchivoFlujoService.cs`

## Regla adoptada en la migracion

- Lectura reutilizable: `vw_*`
- Lectura con filtros y/o paginacion: `usp_*`
- Modificacion de datos: `usp_*` obligatorio
- Indicadores: `vw_` validada + `usp_` filtrado

## Objetos SQL creados en Fase 1

Script: `scripts/sql-refactor-fase1-vistas-sp.sql`

- `vw_Cargas_Listado`
- `vw_Carga_Historial`
- `vw_Indicador_Prostata_Api`
- `usp_Carga_Listar`
- `usp_Carga_ListarPorUsuario`
- `usp_Carga_Historial_Listar`
- `usp_Indicador_Prostata_Listar`

## Backend refactorizado en Fase 1

- `Data/CargasRepository.cs`
  - `ListarAsync`: primero intenta `usp_Carga_Listar`, fallback a SQL actual.
  - `ListarPorUsuarioAsync`: primero intenta `usp_Carga_ListarPorUsuario`, fallback.
  - `ListarHistorialAsync`: primero intenta `usp_Carga_Historial_Listar`, fallback.

- `Data/IndicadoresRepository.cs`
  - `ListarProstataAsync`: primero intenta `usp_Indicador_Prostata_Listar`, fallback a vista legacy.

## Cobertura de endpoints impactados (fase 1)

- `GET /api/cargas`
- `GET /api/cargas/mis-cargas`
- `GET /api/cargas/historial`
- `GET /api/indicadores/prostata`
- `GET /api/public/indicadores/prostata`

## Validacion recomendada post-script

1. Ejecutar `scripts/sql-refactor-fase1-vistas-sp.sql` en `ObservatorioDB`.
2. Reiniciar API y probar:
   - `/api/cargas`
   - `/api/cargas/historial`
   - `/api/public/indicadores/prostata`
3. Confirmar que resultados coinciden con version legacy.

## Siguientes fases sugeridas

- Fase 2: usuarios/roles/dependencias (`usp_Usuario_*`, `usp_Roles_Listar`, `vw_Usuarios_Listado`).
- Fase 3: dashboard (`usp_Dashboard_Resumen` y vistas de soporte).
- Fase 4: proyeccion/catálogos con `usp_ProyeccionPoblacion_ConsultarPaginado` y vistas de catalogos.
- Fase 5: escrituras de cargas/archivos por `usp_` transaccionales.
