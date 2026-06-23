# FASE 0 — Normalización catálogos geográficos

Ambiente: **ObservatorioDB_ASIS_Test** únicamente.  
Fecha ejecución: **2026-06-04**  
`ObservatorioDB` original: **sin cambios** (verificado: `cod_departamento` sigue `5`/`8`/`0`, sin columna `estado` en `dim_area_residencia`, sin `map_area_residencia_fuente`).

## Objetivo

Estandarizar catálogos y llaves geográficas base para que ASIS, población, mortalidad y nacimientos compartan la misma estructura, antes de normalizar tablas de hechos.

## Decisiones de arquitectura

| Rol | Llave | Uso |
|-----|-------|-----|
| Técnica departamento | `dim_departamento.id_departamento` | JOINs internos, FKs |
| Técnica municipio | `dim_municipio.id_municipio` | JOINs internos, FKs |
| Natural departamento | `dim_departamento.cod_departamento` | Cargas Excel, filtros DANE (2 dígitos) |
| Natural municipio | `dim_municipio.codigo_dane` | Código DANE completo (5 dígitos) |
| Natural compuesta | `cod_departamento` + `cod_municipio` | Validación DIVIPOLA (`85001` = `85` + `001`) |

Las tablas finales podrán almacenar **ids técnicos y códigos naturales** para trazabilidad.

### Estándar NACIONAL

Se adoptó **`00`** para el departamento NACIONAL (antes `0`), alineado con padding de 2 dígitos DIVIPOLA.

## Scripts ejecutados

| Script | Etapa | Resultado |
|--------|-------|-----------|
| `scripts/asis-test-clone/04_normalizacion_catalogos_geograficos.sql` | 1–2 | OK |
| `scripts/asis-test-clone/05_map_area_residencia_fuente.sql` | 3 | OK (16 mapeos) |
| `scripts/asis-test-clone/06_validacion_post_normalizacion.sql` | 4 | OK |

### Ejecución

```powershell
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\04_normalizacion_catalogos_geograficos.sql
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\05_map_area_residencia_fuente.sql
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\06_validacion_post_normalizacion.sql
```

Todos los scripts abortan si `DB_NAME() <> 'ObservatorioDB_ASIS_Test'`.

---

## ETAPA 1 — Catálogos geográficos

### dim_departamento

- **Padding aplicado:** `0` → `00`, `5` → `05`, `8` → `08` (3 filas en primera ejecución).
- **Conteo:** 34 departamentos.
- **Restricción creada:** `UQ_dim_departamento_cod_departamento` sobre `cod_departamento`.
- **Duplicados:** ninguno.

### dim_municipio

- **Validaciones OK:**
  - `cod_departamento`: 2 dígitos.
  - `cod_municipio`: 3 dígitos.
  - `codigo_dane`: 5 dígitos.
  - `codigo_dane = cod_departamento + cod_municipio` (20/20).
  - `cod_departamento` existe en `dim_departamento`.
- **Conteo:** 20 municipios (Casanare).
- **Restricciones creadas:**
  - `UQ_dim_municipio_codigo_dane`
  - `UQ_dim_municipio_cod_departamento_cod_municipio`

### dim_departamentos (legacy)

- `codigo_departamento` sincronizado a 2 dígitos si tenía filas (1 fila Casanare).

---

## ETAPA 2 — dim_area_residencia

`dim_area_residencia` **no tenía** columna `estado`. Se agregó:

```sql
estado bit NOT NULL DEFAULT 1  -- 1 = activo, 0 = inactivo
```

| id_area | codigo_area | area_normalizada | estado | Nota |
|--------|-------------|------------------|--------|------|
| 20 | 1 | Urbano | 1 | Activo |
| 21 | 2 | Rural (centro poblado) | 1 | Activo |
| 22 | 3 | Rural (dispersa) | 1 | Activo |
| 23 | INDETERMINADO | Indeterminado | 1 | Activo |
| 24 | FEMENINO | FEMENINO | **0** | Contaminación — pertenece a `dim_sexo` |
| 25 | MASCULINO | MASCULINO | **0** | Contaminación — pertenece a `dim_sexo` |
| 26 | SIN INFORMACION | SIN INFORMACION | 1 | Activo |

- **Filas desactivadas:** 2 (FEMENINO, MASCULINO).
- **Activas:** 5 de 7 totales.
- **No eliminadas físicamente** (preserva FK históricas si existieran; `fact_defunciones` no las usa).

---

## ETAPA 3 — map_area_residencia_fuente

Tabla creada con FK opcional a `dim_area_residencia(id_area)`.

**16 equivalencias** (`fuente_tabla = '*'` = aplica a cualquier tabla con esa columna):

| Categoría | valor_origen (ejemplos) | id_area | area_normalizada |
|-----------|-------------------------|---------|------------------|
| Urbano | Cabecera, Cabecera Municipal, Urbano, Urbana, 1 - CABECERA | 20 | Urbano |
| Rural (centro) | 2 - CENTRO POBLADO | 21 | Rural |
| Rural (dispersa/DANE) | Centros Poblados y Rural Disperso, Rural, Rural disperso, 3 - AREA RURAL DISPERSA | 22 | Rural |
| Total | Total (AREA_GEOGRAFICA, Area) | NULL | Total |
| Sin info | SIN INFORMACION, SIN INFORMACIÓN, NO REPORTADO, INDETERMINADO | 26 / 23 | — |

**Lookup recomendado (próxima fase):**

```sql
SELECT m.id_area, m.codigo_area, m.area_normalizada
FROM dbo.map_area_residencia_fuente m
WHERE m.vigente = 1
  AND m.columna_origen = @columna
  AND m.valor_origen = @valor
   OR UPPER(LTRIM(RTRIM(m.valor_origen))) = UPPER(LTRIM(RTRIM(@valor)))
```

Variantes en mayúsculas duplicadas se omitieron en la tabla por índice único case-insensitive; usar `UPPER()` en joins.

---

## ETAPA 4 — Validación posterior

| Validación | Resultado |
|------------|-----------|
| Conteo `dim_departamento` | 34 |
| Conteo `dim_municipio` | 20 |
| `dim_area_residencia` activas | 5 |
| Duplicados `cod_departamento` | OK |
| Duplicados `codigo_dane` | OK |
| `codigo_dane` ≠ dep + mun | OK (0 filas) |
| Mapeos sin `id_area` | Solo **Total** (2 filas) — esperado |

---

## Fuera de alcance (no ejecutado)

- Tablas de hechos (`fact_defunciones_casanare_normalizada`, proyección, población cruda).
- Columnas `id_municipio` / `id_departamento` en hechos.
- Vistas `vw_ASIS_*` y legacy.
- Backend, frontend, connection strings.

---

## Próximos pasos

1. **Fase hechos — mortalidad:** agregar `id_departamento`, `id_municipio` a `fact_defunciones_casanare_normalizada` (mantener códigos DANE).
2. **Fase hechos — población:** normalizar `Poblacion_por_Departamento`, `Proyeccion_por_Municipio`, `PPED-*` usando `map_area_residencia_fuente` y dims.
3. **Vistas ASIS:** reemplazar joins por texto (`area_geografica`) por `id_area` vía mapa.
4. **Función helper:** `fn_resuelve_id_area(@columna, @valor)` centralizada.
5. **Deprecar** `dim_departamentos` / `dim_municipios` en SP nuevos.

---

## Observaciones

- `dim_municipio` contiene solo Casanare; es correcto para ASIS departamental.
- El padding de departamentos en tablas fuente (`Poblacion_por_Departamento.CODIGO_DANE = '05'`) ahora coincide con `dim_departamento.cod_departamento = '05'`.
- Las filas FEMENINO/MASCULINO en `dim_area_residencia` quedan inactivas hasta una limpieza física planificada en fase posterior.
