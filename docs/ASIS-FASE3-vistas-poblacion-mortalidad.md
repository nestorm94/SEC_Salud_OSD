# ASIS Fase 3 — Vistas población y mortalidad

**Script:** `scripts/sql-refactor-fase7-asis-03-vistas-poblacion-mortalidad.sql`  
**Generador (columnas con acentos desde BD):** `scripts/build-asis-fase3-sql.ps1`  
**Ámbito:** Departamento Casanare (DANE `85`)  
**Ejecución:** `sqlcmd -S localhost\SQLEXPRESS2025 -d ObservatorioDB -E -u -i scripts\sql-refactor-fase7-asis-03-vistas-poblacion-mortalidad.sql`

## Vistas creadas

| Vista | Indicador | Origen principal |
|-------|-----------|------------------|
| `vw_ASIS_Poblacion_Total` | Población total por vigencia | `vw_Poblacion_Nacional_Casanare` |
| `vw_ASIS_Poblacion_Municipio` | Población por municipio | Idem + `dim_municipio` |
| `vw_ASIS_Poblacion_Sexo` | Población por sexo | Idem + `dim_sexo` |
| `vw_ASIS_Poblacion_Area` | Población por área | Idem + `dim_area_residencia` |
| `vw_ASIS_Poblacion_GrupoEdad` | Población por quinquenio | `vw_Reporte_Poblacion_Quinquenios_Unificado` |
| `vw_ASIS_Poblacion_CursoVida` | Población por curso de vida | `vw_Reporte_Poblacion_CursoVida_Unificado` |
| `vw_ASIS_Piramide_Poblacional` | Pirámide (edad simple, sexo, municipio) | `PPED-AreaSexoEdadMun-2018-2042_VP` |
| `vw_ASIS_Mortalidad_Total` | Defunciones totales por vigencia | `fact_defunciones_casanare_normalizada` |
| `vw_ASIS_Mortalidad_Municipio` | Defunciones por municipio | Idem + `dim_municipio` |
| `vw_ASIS_Mortalidad_Sexo` | Defunciones por sexo | Idem + `dim_sexo` |
| `vw_ASIS_Mortalidad_Area` | Defunciones por área | Idem + `dim_area_residencia` |
| `vw_ASIS_Mortalidad_GrupoEdad` | Defunciones por grupo de edad | Idem + `dim_grupo_edad` |
| `vw_ASIS_Mortalidad_CursoVida` | Defunciones por curso de vida | Idem + `dim_curso_vida` |
| `vw_ASIS_Tasa_Bruta_Mortalidad` | TBM departamento y municipio | Población + defunciones ASIS |
| `vw_ASIS_Serie_Mortalidad` | Serie 2005–2025 (defunciones y TBM) | Vistas total ASIS |
| `vw_ASIS_Comparativo_Poblacion_Mortalidad` | Población vs defunciones por municipio | Municipio ASIS (FULL JOIN) |

## Criterios de agregación

- **Población desagregada:** `Sexo = Total` y `Área = Total` (evita doble conteo).
- **Población por sexo:** solo `Hombres` / `Mujeres`, mapeados a `dim_sexo` (MASCULINO / FEMENINO).
- **Mortalidad:** filtro `codigo_departamento = '85'`; joins `INNER` a dimensiones (FK válidas).
- **TBM:** `(defunciones / población) × 1000`; municipio solo cuando hay código DANE en defunciones y población.
- **Pirámide:** formato largo (UNPIVOT); edades 0–99 y 100+; áreas PPED: Cabecera Municipal, Centros Poblados, Total.

## Errores corregidos (jun 2026)

1. **Codificación:** nombres con acento (`Código DANE`, `Área`, `Año`, etc.) leídos vía `SqlClient` en el generador; script en UTF-16 para `sqlcmd -u`.
2. **Sintaxis:** paréntesis faltantes en `LEN(LTRIM(RTRIM(...)))`.
3. **`regional`:** no existe en `dim_municipio`; se toma de la vista de población o `NULL` en mortalidad/pirámide.
4. **UNPIVOT pirámide:** columnas territoriales referenciadas sin alias de tabla tras el unpivot.

## Fase 4 — API y UI (mismo Observatorio)

- **API:** `GET /api/asis/vistas`, `GET /api/asis/catalogos/vigencias`, `GET /api/asis/indicadores/{clave}?pagina=&tamanoPagina=&vigencia=&codigoMunicipio=&nivelTerritorio=` (el código DANE del municipio va en query, no en la ruta)
- **Backend:** `AsisRepository.cs`, `AsisEndpoints.cs`
- **Frontend:** ruta `/asis`, menú **ASIS Departamental**, módulo `frontend/src/app/modules/asis/`

## Fuera de alcance (fases posteriores)

Nacimientos, natalidad, fecundidad, mortalidad infantil/neonatal/materna, causas de muerte, exportación Word/PDF.
