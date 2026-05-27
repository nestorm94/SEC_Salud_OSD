# SQL Refactor Fase 4 — Proyección población y catálogos

## Objetivo
Centralizar consultas de catálogos y proyección paginada en SQL Server (`vw_` / `usp_` / `ufn_`).

## Script SQL

`scripts/sql-refactor-fase4-proyeccion-catalogos.sql`

### Función
- `ufn_Proyeccion_VistaDefault`: primera vista de población disponible.

### Procedimientos de catálogo
- `usp_Catalogo_Departamentos_Listar`
- `usp_Catalogo_Municipios_Listar` (`@CodigoDepartamento`)
- `usp_Catalogo_Regionales_Listar`
- `usp_Catalogo_Areas_Listar`
- `usp_Catalogo_Sexos_Listar`
- `usp_Catalogo_Anios_Listar`

### Proyección
- `usp_ProyeccionPoblacion_ConsultarPaginado`
  - Result set 1: `TotalFilas`
  - Result set 2: filas de la vista (`SELECT *` paginado)
  - Parámetros: clave de vista, paginación y filtros (territorio, regional, área, sexo, año, códigos DANE).

## Backend modificado

| Archivo | Cambio |
|---------|--------|
| `Data/PoblacionCatalogosRepository.cs` | Consume `usp_Catalogo_*` con fallback legacy |
| `Data/PoblacionVistasRepository.cs` | Consume `usp_ProyeccionPoblacion_ConsultarPaginado` con fallback legacy |

## Endpoints impactados

- `GET /api/catalogos/proyeccion`
- `GET /api/catalogos/departamentos`
- `GET /api/catalogos/municipios*`
- `GET /api/catalogos/regionales`, `/areas`, `/sexos`, `/anios`
- `GET /api/proyeccion-poblacion/*`

## Despliegue

```powershell
sqlcmd -S "localhost\SQLEXPRESS2025" -d "ObservatorioDB" -E -b -i "scripts\sql-refactor-fase4-proyeccion-catalogos.sql"
.\scripts\publicar-iis.ps1
```

## Validación (8081)

| Prueba | Resultado |
|--------|-----------|
| `usp_Catalogo_Departamentos_Listar` | 34 departamentos |
| `usp_Catalogo_Regionales_Listar` | Valores desde vista (ej. «No aplica») |
| `usp_ProyeccionPoblacion_ConsultarPaginado` (Casanare 85) | 10 440 filas, paginación OK |
| `GET /api/catalogos/proyeccion` | 34 departamentos |
| `GET /api/proyeccion-poblacion/nacional-casanare?codigoDepartamento=85` | `totalFilas=10440`, filas paginadas OK |

## Ajustes aplicados en el script

- **Regionales desde `dim_*`:** SQL dinámico (`sp_executesql`) para no fallar la compilación del SP cuando la tabla existe sin columna `regional`.
- **Proyección paginada:** variables locales renombradas (`@pTerritorio`, etc.) para evitar colisión con parámetros (`@Territorio` ≡ `@territorio` en SQL Server).
