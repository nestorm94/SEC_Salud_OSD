# ASIS Departamental — Fase 1: Matriz de reutilización

**Proyecto:** Observatorio de Salud Departamental Casanare  
**Base de datos:** `ObservatorioDB` (SQL Server)  
**Alcance Fase 1:** inventario y reutilización (sin Word/PDF, sin app nueva)  
**Fecha inventario:** junio 2026 — consulta directa a BD + scripts del repositorio

---

## 1. Resumen ejecutivo

| Bloque ASIS | ¿Datos en BD? | ¿Expuesto en Observatorio (API/UI)? | Acción principal |
|-------------|---------------|-------------------------------------|------------------|
| **1. Población** | Sí (vistas + tablas PPED) | Parcial (`/poblacion`, catálogos) | Reutilizar vistas; exponer pirámide y grupos de edad |
| **2. Nacimientos** | **No** (sin tabla/hecho) | No | **Nueva estructura** (Fase 2) |
| **3. Mortalidad** | Sí (hecho + vistas agregadas) | No (solo próstata / morbilidad RIPS) | Vistas ASIS + pantallas (Fases 3–4) |
| **4. Fecundidad** | **No** (depende de nacimientos) | No | Derivado tras Fase 2 |
| **5. Indicadores combinados** | Parcial (población + defunciones) | No | Vistas calculadas (Fase 3) |

**Dimensiones reutilizables (no duplicar):** `dim_departamento` / `dim_departamentos`, `dim_municipio` / `dim_municipios`, `dim_sexo`, `dim_area_residencia`, `dim_grupo_edad`, `dim_curso_vida`.

**Módulos existentes a reutilizar:** carga/validación (`Archivos`, `CargasArchivo`, plantillas OSC), catálogos (`usp_Catalogo_*`), proyección (`usp_ProyeccionPoblacion_ConsultarPaginado`), menú Angular (`/poblacion`).

---

## 2. Inventario de objetos relevantes en ObservatorioDB

### 2.1 Dimensiones (catálogos territoriales y demográficos)

| Objeto | Uso actual | Notas |
|--------|------------|-------|
| `dim_departamento` | `usp_Catalogo_Departamentos_Listar` | 34 departamentos; preferir este en nuevos SP ASIS |
| `dim_departamentos` | Bootstrap / validación Excel | Duplicado semántico; **no crear tercera tabla** |
| `dim_municipio` | Catálogos + filtros proyección | `codigo_dane`, `regional`, `nombre_municipio` |
| `dim_municipios` | Bootstrap | Duplicado; unificar referencias en SP nuevos |
| `dim_sexo` | Catálogo proyección | |
| `dim_area_residencia` | Defunciones normalizadas | `area_original`, `area_normalizada` (urbano/rural) |
| `dim_grupo_edad` | `fact_defunciones_*` | 8 grupos; `edad_minima`/`edad_maxima` |
| `dim_curso_vida` | Defunciones + vista curso de vida población | |

### 2.2 Población — tablas y vistas

| Objeto | Tipo | Contenido |
|--------|------|-----------|
| `vw_Poblacion_Nacional_Casanare` | Vista | DANE, territorio, regional, área, sexo, año, población |
| `vw_Reporte_Poblacion_CursoVida_Unificado` | Vista | + curso de vida |
| `vw_Reporte_Poblacion_Quinquenios_Unificado` | Vista | + quinquenios (grupo edad amplio) |
| `PPED-AreaSexoEdadMun-2018-2042_VP` | Tabla | Pirámide: edad simple 0–100+ por sexo, municipio, año, área |
| `PPED_AreaSexoEdadNac_1950_2070` | Tabla | Proyección nacional similar |
| `proyeccion_poblacion_quinquenio` | Tabla | Fuente agregada quinquenios |
| `Proyeccion_por_Municipio` | Tabla | Proyección municipal |
| `Poblacion_por_Departamento` | Tabla | Agregado departamental |

**API/UI:** `GET /api/proyeccion-poblacion/{nacional-casanare|curso-vida|quinquenios}`, `GET /api/catalogos/*`, pantalla Angular **Proyección población**.

### 2.3 Mortalidad — tablas y vistas

| Objeto | Tipo | Contenido |
|--------|------|-----------|
| `[Defunciones Casanare]` | Tabla cruda | Depto, muni, quinquenio, curso vida, área, sexo, año, número defunciones |
| `fact_defunciones_casanare_normalizada` | Hecho | FK a dims; ~94 623 filas (2005–2025) |
| `vw_Defunciones_Casanare_Normalizada` | Vista | Detalle para consulta |
| `vw_Defunciones_Casanare_Por_Area` | Vista | Agregado área + sexo + año |
| `vw_Defunciones_Casanare_Por_Sexo` | Vista | Agregado sexo |
| `vw_Defunciones_Casanare_Por_Curso_Vida` | Vista | Agregado curso de vida |
| `vw_Tasa_Mortalidad_Prostata_Validada` | Vista | Indicador específico línea temática (no ASIS general) |
| `Morbilidad_RIPS_ASIS` / `_V2` | Tabla | **Morbilidad** por gran causa / subgrupo (años en columnas); no sustituye defunciones por causa |

**No existe en BD:** defunciones por **causa CIE** / grandes grupos de causa de muerte en tablas de defunción (solo en morbilidad RIPS).

### 2.4 Nacimientos y fecundidad

| Objeto | Estado |
|--------|--------|
| Tablas `fact_nacimientos_*`, vistas nacimientos | **No existen** |
| Campos nacimiento en validación Excel | Solo DIVIPOLA (`municipio_nacimiento`, etc.) en plantilla OSC |

### 2.5 Módulos operativos del Observatorio (no duplicar)

| Módulo | Tablas / SP | Rol para ASIS |
|--------|-------------|---------------|
| Carga archivos | `Archivos`, `usp_Archivo_*` | Futuro: cargue plantillas nacimientos |
| Cargas / validación | `CargasArchivo`, `usp_Carga_*` | Mismo flujo para series ASIS |
| Indicadores app | `Indicador`, `LineaTematica` | Metadatos; no reemplaza hechos demográficos |
| Auditoría | `AuditoriaSistema` | Trazabilidad cargas ASIS |

---

## 3. Matriz técnica — requerimiento ASIS vs sistema actual

Leyenda: **Existe** = dato en BD; **UI** = consultable hoy en Observatorio; **Calc** = se puede calcular con datos actuales; **Nueva** = requiere tabla/carga nueva; **Ajuste** = vista/SP/columnas.

### 3.1 Población

| # | Requerimiento ASIS | Existe | Tabla / vista / SP | UI Observatorio | Ajuste | Nueva tabla | Calc | Capítulo ASIS |
|---|-------------------|--------|-------------------|-----------------|--------|-------------|------|---------------|
| P1 | Población total por año | Sí | `vw_Poblacion_Nacional_Casanare` | Sí (filtro año) | — | — | Agregación SUM | Demografía |
| P2 | Población por municipio | Sí | Misma vista (territorio/DANE) | Sí | — | — | SUM | Demografía |
| P3 | Población por sexo | Sí | Misma vista | Sí | — | — | SUM | Demografía |
| P4 | Población por área | Sí | Misma vista (`Área`) | Sí | — | — | SUM | Demografía |
| P5 | Población por grupo de edad | Parcial | `vw_Reporte_Poblacion_Quinquenios_Unificado` (quinquenios) | Sí (tab quinquenios) | Vista unificada edad simple | — | SUM | Demografía |
| P6 | Población por curso de vida | Sí | `vw_Reporte_Poblacion_CursoVida_Unificado` | Sí (tab curso vida) | — | — | SUM | Demografía |
| P7 | Pirámide poblacional | Sí (dato) | `PPED-AreaSexoEdadMun-2018-2042_VP` | **No** | SP + vista `vw_ASIS_Piramide_Poblacion` | — | Pivot edad/sexo | Demografía |
| P8 | Distribución urbano/rural | Sí | `dim_area_residencia` + vistas población | Sí (filtro área) | — | — | % | Demografía |
| P9 | Población diferencial | No identificado | — | No | Definir fuente (étnica, discapacidad, etc.) | Según fuente oficial | — | Demografía |

### 3.2 Nacimientos

| # | Requerimiento ASIS | Existe | Tabla / vista / SP | UI | Ajuste | Nueva tabla | Calc | Capítulo ASIS |
|---|-------------------|--------|-------------------|-----|--------|-------------|------|---------------|
| N1 | Nacimientos por año | **No** | — | No | — | `fact_nacimientos` + dims | — | Natalidad |
| N2 | Por municipio | **No** | — | No | — | Idem | — | Natalidad |
| N3 | Por sexo del nacido vivo | **No** | — | No | — | Idem | — | Natalidad |
| N4 | Por área | **No** | — | No | — | Idem | — | Natalidad |
| N5 | Por edad de la madre | **No** | — | No | — | Idem + `dim_grupo_edad` madre | — | Natalidad |
| N6 | Madres adolescentes (10–19) | **No** | — | No | — | Idem | % sobre N | Natalidad |
| N7 | Bajo peso al nacer | **No** | — | No | — | Campo en hecho | % | Natalidad |
| N8 | Tasa bruta de natalidad | **No** | Población sí | No | Vista ASIS | — | Nac / Pob × 1000 | Natalidad |
| N9 | Tasa general de fecundidad | **No** | — | No | — | Hecho nacimientos + MEF | Fórmula estándar | Fecundidad |
| N10 | Fecundidad específica por edad | **No** | — | No | — | Idem | Por grupo edad | Fecundidad |

### 3.3 Mortalidad

| # | Requerimiento ASIS | Existe | Tabla / vista / SP | UI | Ajuste | Nueva tabla | Calc | Capítulo ASIS |
|---|-------------------|--------|-------------------|-----|--------|-------------|------|---------------|
| M1 | Defunciones por año | Sí | `fact_defunciones_casanare_normalizada` | No | `vw_ASIS_Defunciones_*` | — | SUM | Mortalidad |
| M2 | Por municipio | Sí | Hecho + dims | No | Vista agregada | — | SUM | Mortalidad |
| M3 | Por sexo | Sí | `vw_Defunciones_Casanare_Por_Sexo` | No | Reutilizar / unificar | — | SUM | Mortalidad |
| M4 | Por área | Sí | `vw_Defunciones_Casanare_Por_Area` | No | Reutilizar | — | SUM | Mortalidad |
| M5 | Por grupo de edad | Sí | Hecho + `dim_grupo_edad` | No | Vista | — | SUM | Mortalidad |
| M6 | Por curso de vida | Sí | `vw_Defunciones_Casanare_Por_Curso_Vida` | No | Reutilizar | — | SUM | Mortalidad |
| M7 | Mortalidad general (tasa) | Parcial | Población + defunciones | No | `vw_ASIS_Tasa_Mortalidad` | — | Def/Pob × 100 000 | Mortalidad |
| M8 | Por grandes causas | **No** (defunciones) | `Morbilidad_RIPS_ASIS_V2` es morbilidad | No | Cargar defunciones por causa o vincular fuente DANE | Hecho causa | Agrupar CIE | Mortalidad |
| M9 | Mortalidad específica por causa | **No** | Morbilidad RIPS | No | Idem M8 | Idem | Tasa específica | Mortalidad |
| M10 | Transmisibles / NET / externas | **No** | — | No | Clasificación CIE en hecho | — | SUM por grupo | Mortalidad |
| M11 | Mortalidad materna | **No** | — | No | Campo/causa en hecho | — | — | Mortalidad |
| M12 | Mortalidad infantil (<1) | Parcial | Grupos edad en dim (verificar <1) | No | Vista filtro edad | — | Def<1 / Nac | Mortalidad |
| M13 | Mortalidad neonatal | Parcial | Idem | No | Vista | — | Def neonatal / Nac | Mortalidad |
| M14 | Mortalidad <5 años | Parcial | `dim_grupo_edad` | No | Vista | — | Def<5 / Nac | Mortalidad |

### 3.4 Fecundidad (sin nacimientos aún)

| # | Requerimiento ASIS | Existe | Depende de | Nueva | Calc (cuando haya N) | Capítulo |
|---|-------------------|--------|------------|-------|----------------------|----------|
| F1 | Mujeres edad fértil (15–49) | Sí (población) | `PPED` o vistas población | — | SUM pob M 15–49 | Fecundidad |
| F2–F4 | Nacimientos 10–14, 15–19, 10–19 | No | `fact_nacimientos` | Sí | COUNT | Fecundidad |
| F5 | Tasa fecundidad adolescente | No | N + MEF | — | Fórmula | Fecundidad |
| F6 | Tasa global fecundidad | No | N + MEF | — | Fórmula | Fecundidad |
| F7 | Razón niños/mujer | No | N + pob | — | Opcional | Fecundidad |

### 3.5 Indicadores combinados

| # | Indicador | Insumos en BD | Estado | Vista propuesta | Capítulo |
|---|-----------|---------------|--------|-----------------|----------|
| C1 | Mortalidad infantil | Defunciones sí; nacimientos **no** | Bloqueado | `vw_ASIS_Indicador_MortalidadInfantil` | Combinados |
| C2 | Mortalidad neonatal | Parcial def; nac **no** | Bloqueado | `vw_ASIS_Indicador_MortalidadNeonatal` | Combinados |
| C3 | Mortalidad perinatal | No | Nueva fuente | — | Combinados |
| C4 | Razón mortalidad materna | No materna; nac **no** | Bloqueado | — | Combinados |
| C5 | Tasa bruta mortalidad | Def + pob | **Calc** | `vw_ASIS_TasaBrutaMortalidad` | Combinados |
| C6 | Tasa bruta natalidad | Nac **no** + pob | Bloqueado | `vw_ASIS_TasaBrutaNatalidad` | Combinados |
| C7 | Tasa general fecundidad | Nac **no** + MEF | Bloqueado | `vw_ASIS_TasaGeneralFecundidad` | Combinados |

---

## 4. Mapa de reutilización (sin duplicar)

```
┌─────────────────────────────────────────────────────────────┐
│  CAPA EXISTENTE (reutilizar)                                 │
├─────────────────────────────────────────────────────────────┤
│  dim_*  ──►  fact_defunciones_casanare_normalizada           │
│           └► vw_Defunciones_* (agregados)                    │
│  dim_*  ──►  vw_Poblacion_* / PPED (pirámide)                │
│  usp_Catalogo_*  ──►  filtros ASIS (mismo patrón)            │
│  usp_ProyeccionPoblacion_*  ──►  base consulta población     │
│  Archivos / Cargas  ──►  futura carga nacimientos (Fase 2)   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  CAPA NUEVA ASIS (scripts sql-refactor-fase7-asis-*.sql)     │
├─────────────────────────────────────────────────────────────┤
│  cat_asis_indicador, cat_asis_capitulo (metadatos)           │
│  fact_nacimientos (+ FK dim_*)                               │
│  vw_ASIS_* / usp_ASIS_ConsultarIndicadorPaginado             │
│  API /api/asis/*  +  menú «ASIS Departamental» (Angular)     │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. Plan de cambios por fases (sin implementar aún)

### Fase 1 — Entregable (este documento)
- Matriz de reutilización ✓
- Inventario BD ✓
- Plan de fases ✓

### Fase 2 — Nacimientos
**Scripts:** `scripts/sql-refactor-fase7-asis-nacimientos.sql`  
- Tabla `fact_nacimientos` (FK: municipio, sexo, área, grupo edad madre, año, flags bajo peso, etc.)
- `usp_Nacimiento_*` (insert bulk, listar, validar)
- Plantilla Excel + extensión `ArchivoFlujoService` o indicador ASIS dedicado
- **Reutilizar:** todas las `dim_*` existentes

### Fase 3 — Vistas e indicadores ASIS
**Scripts:** `scripts/sql-refactor-fase7-asis-vistas.sql`  
- Catálogo `cat_asis_capitulo`, `cat_asis_indicador` (código, fórmula, dependencias)
- Vistas: tasas brutas, agregados defunciones, pirámide desde PPED
- Vistas combinadas (cuando exista `fact_nacimientos`)
- Extender defunciones por **causa** si se incorpora fuente (tabla staging + hecho, o carga Excel)

### Fase 4 — Pantallas «Indicadores base ASIS»
**Sin nuevo frontend:** módulo Angular bajo ruta `/asis`  
- Menú: **ASIS Departamental** → **Indicadores base ASIS**
- Filtros: vigencia, municipio, sexo, área, grupo edad, curso de vida, capítulo, tipo indicador
- Backend: `AsisRepository` → `usp_ASIS_ConsultarPaginado`
- Reutilizar `TablePaginatorComponent`, `CatalogoService`

### Fase 5 — Soportes ASIS
**Scripts:** `scripts/sql-refactor-fase7-asis-soportes.sql`  
- Tabla `asis_indicador_soporte` (tabla/gráfica/mapa, fuente, archivo, análisis, observaciones)
- Enlace a `Archivos` para documentos soporte
- UI: pestañas por indicador (sin generar Word)

### Fase 6 — Preparación documento Word/PDF
- Contrato de datos: `asis_informe_seccion`, plantilla placeholders
- Endpoint reservado `POST /api/asis/informe/generar` (501 Not Implemented)
- Sin implementar generación

---

## 6. Archivos del repositorio que se tocarían (después de aprobación)

| Área | Archivos previstos |
|------|-------------------|
| SQL | `scripts/sql-refactor-fase7-asis-*.sql` (nuevos, no modificar fases 1–6) |
| Backend | `Data/AsisRepository.cs`, `Endpoints/ApiEndpoints.cs` (MapAsisApi), modelos DTO |
| Frontend | `modules/asis/*`, `sidebar.component.ts`, `app.routes.ts` |
| Docs | Este archivo + actualización `SQL-SERVER-CATALOGO-OBJETOS.md` |

**No se modificarán** en Fase 1: `Program.cs`, esquema bootstrap, flujo OSC existente.

---

## 7. Riesgos y decisiones pendientes

1. **Doble catálogo geo:** `dim_departamento` vs `dim_departamentos` — los SP ASIS deben usar solo el que ya consume `usp_Catalogo_*`.
2. **Mortalidad por causa:** hoy solo en `Morbilidad_RIPS_ASIS_V2`; confirmar fuente oficial de defunciones por CIE para ASIS.
3. **Pirámide:** datos en `PPED-*` con nombres de columna anchos; conviene vista normalizada long format (año, edad, sexo, valor).
4. **Nacimientos:** bloquean la mayoría de indicadores combinados y fecundidad hasta Fase 2.

---

## 8. Próximo paso recomendado

1. Validar con usted esta matriz (¿fuente de nacimientos? ¿defunciones por causa en Excel DANE?).
2. Aprobar plan de fases.
3. Implementar **Fase 2** (SQL nacimientos) como primer desarrollo, en paralelo diseño catálogo `cat_asis_indicador`.

---

*Documento generado como entregable Fase 1. No implica cambios desplegados en producción hasta ejecutar scripts y publicar API.*
