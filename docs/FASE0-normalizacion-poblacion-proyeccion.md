# FASE 0 — Normalización población en `fact_poblacion_proyeccion`

Ambiente: **ObservatorioDB_ASIS_Test** únicamente.  
Fecha ejecución: **2026-06-04**  
`ObservatorioDB` original: **sin cambios** (tabla `fact_poblacion_proyeccion` no existe allí).

## Objetivo

Consolidar población de **Nación**, **Departamento** y **Municipio** en una sola tabla normalizada, alimentada desde tres fuentes DANE staging, sin eliminar tablas origen.

## Fuentes (staging / histórico)

| Fuente | Nivel | Filas origen | Registros en fact |
|--------|-------|--------------|-------------------|
| `PPED_AreaSexoEdadNac_1950_2070` | NACION | 323 | 40 803 |
| `Poblacion_por_Departamento` | DEPARTAMENTO | 4 224 | 5 412 |
| `PPED-AreaSexoEdadMun-2018-2042_VP` | MUNICIPIO (Casanare) | 1 425 | 185 535 |

**Total fact:** **231 750** registros.

## Decisiones confirmadas

| Tema | Decisión |
|------|----------|
| `codigo_dane` nación | `'00000'` |
| Categoría DANE agregada rural | `id_area = 22` |
| Centro poblado explícito | `id_area = 21` solo si texto `2 - CENTRO POBLADO` |
| Municipio fuera de catálogo | `id_municipio = NULL`, conservar códigos DANE |
| Casanare | `id_municipio` resuelto en los 20 municipios |
| `id_grupo_edad` / `id_curso_vida` | NULL en ETL; resueltos en vistas |

## Scripts

| Script | Contenido |
|--------|-----------|
| `07_fact_poblacion_proyeccion.sql` | Tabla, `fn_ASIS_Resolver_IdArea`, 7 vistas ASIS |
| `08_usp_normalizar_poblacion_nacional.sql` | SP nacional |
| `09_usp_normalizar_poblacion_departamental.sql` | SP departamental |
| `10_usp_normalizar_poblacion_municipal.sql` | SP municipal (UNPIVOT dinámico) |
| `11_usp_normalizar_poblacion_todo.sql` | Orquestador + ejecución |
| `12_validacion_fact_poblacion.sql` | Validaciones y comparación con fuentes |

### Ejecución

```powershell
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\07_fact_poblacion_proyeccion.sql
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\08_usp_normalizar_poblacion_nacional.sql
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\09_usp_normalizar_poblacion_departamental.sql
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\10_usp_normalizar_poblacion_municipal.sql
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\11_usp_normalizar_poblacion_todo.sql
sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB_ASIS_Test -E -i scripts\asis-test-clone\12_validacion_fact_poblacion.sql
```

**Nota técnica:** scripts con columnas calculadas y SPs requieren `SET QUOTED_IDENTIFIER ON` (incluido en los `.sql`).

## Resultados de validación

### Conteos

| Métrica | Valor |
|---------|-------|
| Total registros | **231 750** |
| NACION | 40 803 |
| DEPARTAMENTO | 5 412 |
| MUNICIPIO | 185 535 |
| EDAD_SIMPLE | 226 338 |
| TOTAL_SEXO | 5 412 |

### Integridad

| Validación | Resultado |
|------------|-----------|
| Duplicados (grano lógico) | OK |
| `id_area` NULL | OK (ninguno) |
| Departamentos sin `id_departamento` | OK |
| Municipios Casanare sin `id_municipio` | OK |

### Comparación con fuentes

| Comparación | Fuente | Fact | Coincide |
|-------------|--------|------|----------|
| Casanare dept (85), sin área Total | 18 579 146 | 18 579 146 | Sí |
| Casanare mun, Total_H+M sin área Total | 12 204 112 | 12 204 112 | Sí |

La suma municipal **con** filas `Total` en área (24 408 224) no se compara con fact porque esas filas son agregados y se excluyen a propósito.

## Vistas ASIS (sobre fact)

- `vw_ASIS_Poblacion_Nacional`
- `vw_ASIS_Poblacion_Departamental`
- `vw_ASIS_Poblacion_Municipal`
- `vw_ASIS_Poblacion_Piramide`
- `vw_ASIS_Poblacion_CursoVida`
- `vw_ASIS_Poblacion_GrupoEdad`
- `vw_ASIS_Poblacion_Total_Sexo_Area`

**Anti-doble conteo:** filtrar `tipo_registro` — edad en `EDAD_SIMPLE`, totales por sexo en `TOTAL_SEXO`.

## Fuera de alcance (respetado)

- `ObservatorioDB` original
- Backend, frontend, connection strings
- Eliminación de tablas fuente

## Próximos pasos

1. Redirigir endpoints ASIS de población a `fact_poblacion_proyeccion` / vistas nuevas.
2. Normalizar `fact_defunciones_casanare_normalizada` con mismas llaves geo.
3. Reconciliar vistas ASIS legacy (`vw_ASIS_Poblacion_Area`, etc.) con las basadas en fact.
