# SQL Refactor Fase 3 — Dashboard

## Objetivo
Centralizar KPIs del endpoint `GET /api/dashboard/resumen` en SQL Server con objetos `vw_`/`usp_`.

## Objetos SQL

Script: `scripts/sql-refactor-fase3-dashboard.sql`

- `vw_Dashboard_UltimosCargues`: base de “Últimos cargues” (join CargasArchivo + Dependencias + Archivos + Usuarios) y `Fecha = COALESCE(FechaFin, FechaInicio)`.
- `usp_Dashboard_Resumen` (2 result sets):
  - Result set 1: `TotalArchivos`, `CargasPendientes`, `CargasConError`, `CargasAprobadas`
  - Result set 2: últimos cargues (`TOP 10`) con orden por `Fecha DESC, Id DESC`

## Backend

Archivo: `backend/Observatorios.Api/Data/DashboardRepository.cs`

Comportamiento:
- Si existe `dbo.usp_Dashboard_Resumen`, se usa.
- Si no existe (BD no migrada aún), se cae a la lógica legacy embebida en C#.

## Validación

- Endpoint probado en IIS `8081`: `GET /api/dashboard/resumen` con login.
- Respuesta OK y `ultimos_cargues` ordenados por `Fecha` (última actividad).

