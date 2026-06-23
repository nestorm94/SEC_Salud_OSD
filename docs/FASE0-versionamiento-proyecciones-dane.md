# FASE 0 — Versionamiento proyecciones DANE

Ambiente: **ObservatorioDB_ASIS_Test** únicamente.

## Objetivo

Conservar **múltiples versiones** de proyección DANE (2025, 2027, 2030…) sin sobrescribir cargas anteriores. Cada fila en `fact_poblacion_proyeccion` queda ligada a una vigencia de proyección.

## Modelo

```
dim_proyeccion_dane (1) ──< fact_poblacion_proyeccion (N)
```

### `dim_proyeccion_dane`

| Columna | Descripción |
|---------|-------------|
| `id_proyeccion_dane` | PK técnica |
| `nombre_proyeccion` | Ej. `Proyeccion DANE 2025` |
| `anio_publicacion` | Ej. `2025` |
| `fuente` | Origen del archivo / lote |
| `descripcion` | Notas opcionales |
| `fecha_cargue` | Timestamp de registro |
| `estado` | 1 = activa |

**Unicidad:** `(nombre_proyeccion, anio_publicacion)` — no se duplica la misma proyección.

### `fact_poblacion_proyeccion`

Columna agregada:

- **`id_proyeccion_dane INT NOT NULL`** → FK a `dim_proyeccion_dane`

**Índice único** (grano por proyección):

`id_proyeccion_dane`, `nivel_territorial`, `tipo_registro`, códigos territorio, `anio`, `id_area`, `id_sexo`, `edad_simple`, `fuente_tabla`

## Semántica de consulta

| Campo | Significado |
|-------|-------------|
| `anio` | Año del dato demográfico (ej. 2030) |
| `id_proyeccion_dane` | Versión de proyección usada |
| `anio_publicacion` | Año en que DANE publicó esa proyección |

**Ejemplo:** `anio = 2030`, `nombre_proyeccion = 'Proyeccion DANE 2025'` → población estimada para 2030 según la proyección publicada en 2025.

Comparar escenarios:

```sql
SELECT id_proyeccion_dane, nombre_proyeccion, anio_publicacion, anio, SUM(poblacion) AS pob
FROM dbo.vw_ASIS_Poblacion_Departamental
WHERE cod_departamento = '85' AND anio = 2025
GROUP BY id_proyeccion_dane, nombre_proyeccion, anio_publicacion, anio;
```

## Procedimientos

| SP | Rol |
|----|-----|
| `usp_ASIS_CrearProyeccionDANE` | Crea o retorna `id_proyeccion_dane` |
| `usp_ASIS_Normalizar_Poblacion_Nacional` | `@id_proyeccion_dane` |
| `usp_ASIS_Normalizar_Poblacion_Departamental` | `@id_proyeccion_dane` |
| `usp_ASIS_Normalizar_Poblacion_Municipal` | `@id_proyeccion_dane` |
| `usp_ASIS_Normalizar_Poblacion_Todo` | Metadata + normalización completa |

### Carga típica

```sql
EXEC dbo.usp_ASIS_Normalizar_Poblacion_Todo
    @nombre_proyeccion = N'Proyeccion DANE 2025',
    @anio_publicacion = 2025,
    @fuente = N'DANE PPED staging',
    @descripcion = N'Primera carga versionada';
```

Nueva versión (no borra la anterior):

```sql
EXEC dbo.usp_ASIS_Normalizar_Poblacion_Todo
    @nombre_proyeccion = N'Proyeccion DANE 2027',
    @anio_publicacion = 2027,
    @fuente = N'DANE PPED 2027',
    @descripcion = N'Segunda ronda proyeccion';
```

## Control de recarga — Opción A (activa)

Si se vuelve a ejecutar **la misma** proyección (`nombre` + `anio_publicacion`):

1. Se reutiliza el mismo `id_proyeccion_dane`.
2. Se **eliminan** todas las filas de fact con ese id.
3. Se recargan nacional + departamental + municipal.

**No afecta** otras proyecciones almacenadas.

### Opción B (futura, documentada)

Bloquear recarga si ya existen filas para ese `id_proyeccion_dane` (requiere parámetro `@permitir_recarga` o similar).

## Migración datos existentes

Script `14_proyeccion_dane_versionamiento.sql`:

- Crea `dim_proyeccion_dane`.
- Asigna filas previas a **`Proyeccion DANE carga inicial` / 2025**.
- Agrega FK e índice único con `id_proyeccion_dane`.
- Actualiza vistas `vw_ASIS_Poblacion_*`.

## Scripts

| Orden | Script |
|-------|--------|
| 1 | `14_proyeccion_dane_versionamiento.sql` |
| 2 | `08` … `11` (SPs actualizados) |
| 3 | `12_validacion_fact_poblacion.sql` |

## Vistas ASIS

Todas incluyen: `id_proyeccion_dane`, `nombre_proyeccion`, `anio_publicacion`, `anio`.

- `vw_ASIS_Poblacion_Nacional`
- `vw_ASIS_Poblacion_Departamental`
- `vw_ASIS_Poblacion_Municipal`
- `vw_ASIS_Poblacion_Piramide`
- `vw_ASIS_Poblacion_CursoVida`
- `vw_ASIS_Poblacion_GrupoEdad`
- `vw_ASIS_Poblacion_Total_Sexo_Area`

## Reglas respetadas

- Solo `ObservatorioDB_ASIS_Test`
- Tablas fuente intactas (staging)
- No sobrescribir proyecciones distintas
- Backend / frontend / connection strings sin cambios

## Verificación

```sql
SELECT d.nombre_proyeccion, d.anio_publicacion, COUNT(*) AS filas
FROM dbo.fact_poblacion_proyeccion f
JOIN dbo.dim_proyeccion_dane d ON d.id_proyeccion_dane = f.id_proyeccion_dane
GROUP BY d.nombre_proyeccion, d.anio_publicacion;
```

Debe listar **una fila por proyección** cargada; al agregar 2027, aparecen dos bloques sin perder 2025.
