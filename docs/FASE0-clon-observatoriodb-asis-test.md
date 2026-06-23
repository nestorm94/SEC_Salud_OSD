# FASE 0 — Clon de ObservatorioDB para pruebas ASIS

Ambiente seguro para normalización, estandarización de nombres y ajustes ASIS **sin afectar la base de producción local**.

## Resumen

| Campo | Valor |
|-------|-------|
| **Fecha del backup** | 2026-06-04 |
| **Instancia SQL Server** | `localhost\SQLEXPRESS2025` |
| **Base original** | `ObservatorioDB` |
| **Base de pruebas** | `ObservatorioDB_ASIS_Test` |
| **Estado restore** | Exitoso |
| **Fecha restore** | 2026-06-04 |

## Archivo de backup

| Campo | Valor |
|-------|-------|
| **Ruta .bak** | `C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS2025\MSSQL\Backup\ObservatorioDB_2026-06-04.bak` |
| **Tamaño aprox.** | ~516 MB (540 631 040 bytes) |
| **CHECKSUM** | Sí (generado en backup) |
| **VERIFYONLY** | OK (script `00`) |

## Archivos de datos (MDF / LDF)

La base de prueba usa **archivos distintos** a la original; `ObservatorioDB` no fue sobrescrita.

| Base | MDF | LDF |
|------|-----|-----|
| ObservatorioDB | `...\DATA\ObservatorioDB.mdf` | `...\DATA\ObservatorioDB_log.ldf` |
| ObservatorioDB_ASIS_Test | `...\DATA\ObservatorioDB_ASIS_Test.mdf` | `...\DATA\ObservatorioDB_ASIS_Test_log.ldf` |

Ambas bases están **ONLINE**.

## Scripts

Ubicación: `scripts/asis-test-clone/`

| Orden | Script | Propósito |
|-------|--------|-----------|
| 1 | `00_backup_observatoriodb.sql` | Backup completo de `ObservatorioDB` con fecha del día, CHECKSUM y VERIFYONLY |
| 2 | `01_restore_observatoriodb_asis_test.sql` | Restore como `ObservatorioDB_ASIS_Test` con `MOVE`; no reemplaza si ya existe (`@PermitirReemplazo = 0`) |
| 3 | `02_validacion_restore_observatoriodb_asis_test.sql` | Comparación de tablas, conteos y vistas entre original y clon |

### Ejecución (Windows, autenticación integrada)

```powershell
cd C:\Users\Asus\Projects\Observatorios_Salud_Departamental_Cas

# 1. Backup (solo si se necesita uno nuevo del día)
sqlcmd -S localhost\SQLEXPRESS2025 -E -i scripts\asis-test-clone\00_backup_observatoriodb.sql

# 2. Restore
sqlcmd -S localhost\SQLEXPRESS2025 -E -i scripts\asis-test-clone\01_restore_observatoriodb_asis_test.sql

# 3. Validación
sqlcmd -S localhost\SQLEXPRESS2025 -E -i scripts\asis-test-clone\02_validacion_restore_observatoriodb_asis_test.sql
```

### Reemplazar base de prueba existente

Si `ObservatorioDB_ASIS_Test` ya existe, el script `01` **aborta** por defecto. Solo después de confirmar explícitamente, editar en `01_restore_observatoriodb_asis_test.sql`:

```sql
DECLARE @PermitirReemplazo bit = 1;
```

## Resultado de validaciones (2026-06-04)

### Existencia

- `ObservatorioDB`: OK (ONLINE)
- `ObservatorioDB_ASIS_Test`: OK (ONLINE)

### Tablas de usuario

| Métrica | Original | Prueba | Resultado |
|---------|----------|--------|-----------|
| Número de tablas | 44 | 44 | OK |

### Conteos tablas principales

| Tabla | Original | Prueba | Resultado |
|-------|----------|--------|-----------|
| dim_departamento | 34 | 34 | OK |
| dim_municipio | 20 | 20 | OK |
| dim_sexo | 13 | 13 | OK |
| dim_area_residencia | 7 | 7 | OK |
| dim_grupo_edad | 8 | 8 | OK |
| dim_curso_vida | 6 | 6 | OK |
| fact_defunciones_casanare_normalizada | 94 623 | 94 623 | OK |

### Vistas principales (consulta TOP 1)

| Vista | Original | Prueba |
|-------|----------|--------|
| vw_Poblacion_Nacional_Casanare | OK | OK |
| vw_Reporte_Poblacion_CursoVida_Unificado | OK | OK |
| vw_Reporte_Poblacion_Quinquenios_Unificado | OK | OK |
| vw_Defunciones_Casanare_Por_Sexo | OK | OK |
| vw_Defunciones_Casanare_Por_Curso_Vida | OK | OK |
| vw_Defunciones_Casanare_Por_Area | OK | OK |

**Conclusión:** el clon es una copia consistente de `ObservatorioDB` al momento del backup.

## Reglas operativas

1. **No modificar** `ObservatorioDB` para trabajo ASIS / normalización.
2. **Todos los cambios** de estandarización, renombrado y scripts ASIS van sobre `ObservatorioDB_ASIS_Test`.
3. **No cambiar** `Program.cs`, `appsettings`, cadenas de conexión, frontend ni endpoints del sistema principal **todavía**.
4. Si la base de prueba ya existe, **preguntar antes** de reemplazarla (`@PermitirReemplazo`).
5. El sistema en producción local sigue apuntando a `ObservatorioDB`.

## Observaciones

- SQL Server Express no admite compresión en backup; el `.bak` es sin `COMPRESSION`.
- El backup del 2026-06-04 ya existía; se reutilizó para el restore (misma fecha que la ejecución).
- Restore procesó 65 985 páginas en ~13 s.
- Las 16 vistas `vw_ASIS_*` (Fase 3) también están presentes en el clon al ser parte del backup.
- Permisos de lectura del folder `Backup` desde PowerShell pueden fallar; SQL Server accede sin problema.

## Próximos pasos

1. **Diagnóstico de nombres** (solo en `ObservatorioDB_ASIS_Test`): inventario de tablas, columnas, acentos, inconsistencias de casing y prefijos.
2. **Plan de estandarización**: convenciones objetivo (snake_case, sin acentos en identificadores, prefijos `dim_` / `fact_` / `vw_`).
3. **Scripts ASIS iterativos** contra el clon; validar con `02_validacion_*` tras cada cambio mayor.
4. **Cuando el clon esté estable**: decidir cambio de connection string / feature flag para apuntar la API ASIS a `ObservatorioDB_ASIS_Test` (fuera de alcance Fase 0).
5. **Backup periódico** del clon antes de cambios destructivos (renombrados masivos, DROP, etc.).
