# SQL Refactor Fase 5 — Escrituras transaccionales (cargas y archivos)

## Objetivo
Mover INSERT/UPDATE/DELETE y cargas masivas a stored procedures con TVP, manteniendo validación Excel y BCrypt en C#.

## Script SQL

`scripts/sql-refactor-fase5-cargas-archivos-writes.sql`

### Tipos tabla (TVP)
- `dbo.Tvp_CampoDiccionario`
- `dbo.Tvp_DatosCargados`
- `dbo.Tvp_ErrorValidacion`

### Vistas
- `vw_Archivos_Listado`
- `vw_Carga_Detalle`

### Procedimientos — Archivos
| SP | Uso |
|----|-----|
| `usp_Archivo_Insertar` | Alta metadata |
| `usp_Archivo_ActualizarValidacion` | Resultado prevalidación |
| `usp_Archivo_MarcarEnviado` | Envío a validación |
| `usp_Archivo_ObtenerEstado` | Estado puntual |
| `usp_Archivo_Obtener` | Detalle |
| `usp_Archivo_Listar` | Listado **sin TOP** |
| `usp_Archivo_Eliminar` | Borrado |

### Procedimientos — Cargas (escritura)
| SP | Uso |
|----|-----|
| `usp_Carga_Crear` | Nueva carga |
| `usp_Carga_ActualizarEstado` | Flujo de estados |
| `usp_Carga_RegistrarHistorial` | Auditoría de carga |
| `usp_Carga_Obtener` | Detalle |
| `usp_Carga_Errores_Listar` | Errores de validación |
| `usp_Carga_LimpiarResultadosValidacion` | Reintento (transacción) |
| `usp_Carga_GuardarDiccionario` | Diccionario + campos (TVP, transacción) |
| `usp_Carga_GuardarDatosBulk` | Filas válidas (TVP) |
| `usp_Carga_GuardarErroresBulk` | Errores (TVP) |

Los SP de listado/historial de fase 1 (`usp_Carga_Listar`, etc.) se reutilizan sin cambios.

## Backend

| Archivo | Cambio |
|---------|--------|
| `Data/SqlTvpHelper.cs` | Construcción de TVP desde DTOs |
| `Data/CargasRepository.cs` | Escrituras y lecturas puntuales vía SP + fallback |
| `Data/ArchivosRepository.cs` | CRUD vía SP + fallback; listado sin `TOP 300` |

## Despliegue

```powershell
sqlcmd -S "localhost\SQLEXPRESS2025" -d "ObservatorioDB" -E -b -i "scripts\sql-refactor-fase5-cargas-archivos-writes.sql"
.\scripts\publicar-iis.ps1
```

## Validación (8081)

| Prueba | Resultado |
|--------|-----------|
| `GET /api/cargas` | 4 cargas |
| `GET /api/archivos` | 13 archivos (sin límite TOP) |
| `GET /api/cargas/{id}` | Detalle OK |
| `GET /api/cargas/{id}/errores` | OK |
| `GET /api/cargas/historial` | 12 registros |

## Qué sigue en C#
- Validación de plantillas Excel (`OscPlantillaValidacionService`, `ExcelValidationService`)
- Reglas de autorización y flujo (`CargaArchivoService`, `ArchivoFlujoService`)
- Hash de contraseñas (BCrypt)
