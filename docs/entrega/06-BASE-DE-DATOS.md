# 06 — Base de datos

**Observatorio OSD — SQL Server**  
Versión 1.0 — Julio 2026

---

## 1. Motor y convenciones

| Aspecto | Valor |
|---------|-------|
| Motor | Microsoft SQL Server 2019/2022 |
| Base operativa | `ObservatorioDB` |
| Base laboratorio ASIS | `ObservatorioDB_ASIS_Test` |
| Vistas | Prefijo `vw_` |
| Procedimientos | Prefijo `usp_` |
| Funciones | Prefijo `ufn_` |
| Tipos tabla (TVP) | Prefijo `Tvp_` |

**Regla:** el backend C# invoca SPs; no duplica lógica SQL en repositorios.

Catálogo detallado: [docs/SQL-SERVER-CATALOGO-OBJETOS.md](../SQL-SERVER-CATALOGO-OBJETOS.md)

---

## 2. Bases de datos

### ObservatorioDB (producción)

- Cargues, usuarios, catálogos app, vistas proyección legacy, vistas `vw_ASIS_*` (si desplegadas).
- **No modificar** para experimentos ASIS.

### ObservatorioDB_ASIS_Test (laboratorio)

- Clon de `ObservatorioDB` para normalización, `fact_*`, ETL y comparación legacy vs fact.
- Scripts en `scripts/asis-test-clone/` validan `DB_NAME() = 'ObservatorioDB_ASIS_Test'`.
- Documentación: [docs/FASE0-clon-observatoriodb-asis-test.md](../FASE0-clon-observatoriodb-asis-test.md)

| Criterio | ObservatorioDB | ObservatorioDB_ASIS_Test |
|----------|----------------|--------------------------|
| Operación diaria | Sí | No |
| `fact_poblacion_proyeccion` | Solo si se migra | Sí |
| ETL nacimientos | Opcional | Sí |

---

## 3. Cadenas de conexión

| Archivo | Servidor | Base |
|---------|----------|------|
| `appsettings.json` | LocalDB | ObservatorioDB |
| `appsettings.Production.json` | SQLEXPRESS2025 | ObservatorioDB |
| `appsettings.Development.json` | SQLEXPRESS2025 | ObservatorioDB_ASIS_Test |

---

## 4. Modelo de datos — Seguridad y organización

| Tabla | Propósito |
|-------|-----------|
| `Dependencias` | Entidades que cargan datos |
| `Usuarios` | Cuentas (hash BCrypt en app) |
| `Roles`, `UsuarioRol` | RBAC |
| `UsuarioAreaTematica` | Áreas por usuario |
| `LineaTematica`, `Indicador` | Clasificación temática |
| `AreaTematica`, `ResponsableTematico` | Modelo v2 por dependencia |
| `AuditoriaSistema` | Trazabilidad admin |

---

## 5. Modelo de datos — Cargas

| Tabla | Propósito |
|-------|-----------|
| `Archivos` | Metadatos archivo físico |
| `CargasArchivo` | Instancia de cargue y estado |
| `DiccionarioArchivo`, `CamposDiccionario` | Diccionario persistido |
| `DatosCargados` | Filas DATA (JSON) |
| `ErroresValidacion` | Errores por fila/columna |
| `HistorialCarga` | Eventos del flujo |
| `PlantillasCarga`, `CamposPlantilla` | Plantillas de validación |

### Estados del flujo

**Archivo:** `PendienteValidacion` → `Validado` | `Rechazado` → `Enviado`

**Cargue:** `RECIBIDO` → `EN_VALIDACION` → `VALIDADO_*` → `APROBADO` | `RECHAZADO` → `CARGADO_BD`

---

## 6. Dimensiones (`dim_*`)

| Tabla | Uso |
|-------|-----|
| `dim_departamento`, `dim_municipio` | Catálogo geo DANE |
| `dim_departamentos`, `dim_municipios` | Validación Excel (bootstrap) |
| `dim_sexo`, `dim_area_residencia` | Demografía |
| `dim_curso_vida`, `dim_grupo_edad` | Agrupaciones etarias |
| `dim_proyeccion_dane` | Versiones proyección DANE |
| `dim_peso_al_nacer`, `dim_semanas_gestacion` | Nacimientos |
| `dim_nivel_educativo`, `dim_pertenencia_etnica` | Sociodemografía nacimientos |

**Mapeos:** `map_area_residencia_fuente`, `map_peso_al_nacer_fuente`, etc.

---

## 7. Hechos (`fact_*`)

| Tabla | Descripción |
|-------|-------------|
| `fact_defunciones_casanare_normalizada` | Mortalidad normalizada (~94k filas) |
| `fact_poblacion_proyeccion` | Proyección DANE unificada (ASIS Test) |
| `fact_nacimientos_casanare_normalizada` | Nacimientos normalizados |

**Fuentes legacy (no eliminar):** `PPED_AreaSexoEdadNac_1950_2070`, `Poblacion_por_Departamento`, `PPED-AreaSexoEdadMun-2018-2042_VP`, `vw_Poblacion_Nacional_Casanare`, `vw_Reporte_Poblacion_*`.

---

## 8. Vistas principales

### Aplicación (fases 1–6)

`vw_Cargas_Listado`, `vw_Carga_Historial`, `vw_Usuarios_Listado`, `vw_Dashboard_UltimosCargues`, `vw_Archivos_Listado`, `vw_LineaTematica_Listado`, `vw_Indicador_Listado`, `vw_Plantilla_Listado`, etc.

### ASIS (`vw_ASIS_*`)

**Población:** `vw_ASIS_Poblacion_Total`, `_Municipio`, `_Sexo`, `_Area`, `_GrupoEdad`, `_CursoVida`, `_Piramide_Poblacional`

**Mortalidad:** `vw_ASIS_Mortalidad_Total`, `_Municipio`, `_Detalle`, `_Sexo`, `_Area`, `_GrupoEdad`, `_CursoVida`, `vw_ASIS_Tasa_Bruta_Mortalidad`, `vw_ASIS_Serie_Mortalidad`

**Nacimientos:** vistas en `scripts/asis-test-clone/22_vistas_asis_nacimientos.sql`

---

## 9. Procedimientos por dominio

| Dominio | Ejemplos |
|---------|----------|
| Cargas | `usp_Carga_Listar`, `usp_Carga_Crear`, `usp_Carga_GuardarDatosBulk` |
| Archivos | `usp_Archivo_Insertar`, `usp_Archivo_Listar` |
| Usuarios | `usp_Usuario_ObtenerPorId`, `usp_Usuario_Crear` |
| Dashboard | `usp_Dashboard_Resumen` |
| Catálogos | `usp_Catalogo_Departamentos`, `usp_Catalogo_Municipios` |
| Proyección | `usp_ProyeccionPoblacion_ConsultarPaginado` |
| Admin | `usp_LineaTematica_*`, `usp_Indicador_*`, `usp_Plantilla_*` |
| ASIS ETL | `usp_ASIS_Normalizar_Poblacion_*` (clon) |

### TVP (fase 5)

`Tvp_CampoDiccionario`, `Tvp_DatosCargados`, `Tvp_ErrorValidacion`

### Función escalar

`ufn_Proyeccion_VistaDefault()` — vista de población disponible.

---

## 10. Scripts — Orden de despliegue

### Producción (ObservatorioDB)

```
1. schema-bootstrap.sql
2. schema-seed-minimo.sql
3. sql-refactor-fase1-vistas-sp.sql
4. sql-refactor-fase2-usuarios-roles-dependencias.sql
5. sql-refactor-fase3-dashboard.sql
6. sql-refactor-fase4-proyeccion-catalogos.sql
7. sql-refactor-fase5-cargas-archivos-writes.sql
8. sql-refactor-fase6-admin-catalogos.sql
9. sql-refactor-fase7-asis-03-vistas-poblacion-mortalidad.sql (opcional)
```

Ver: `scripts/README-SQL-REFACTOR.md`

### Laboratorio ASIS (ObservatorioDB_ASIS_Test)

```
00_backup → 01_restore → 02_validacion
04–06 catálogos geo
07–16 población fact + comparación
17–25 mortalidad y nacimientos
```

---

## 11. Mapa repositorio C# ↔ SQL

| Repositorio | Fase SQL |
|-------------|----------|
| `CargasRepository`, `ArchivosRepository` | 1, 5 |
| `UsuariosRepository`, `RolesRepository`, `DependenciasRepository` | 2 |
| `DashboardRepository` | 3 |
| `PoblacionCatalogosRepository`, `PoblacionVistasRepository` | 4 |
| `LineaTematicaRepository`, `IndicadorRepository`, `PlantillasRepository`, … | 6 |
| `AsisRepository` | Vistas `vw_ASIS_*` |
| `IndicadoresRepository` | `usp_Indicador_Prostata_Listar` |

**Permanece en C#:** validación Excel, BCrypt, autorización, archivos disco, caché.

---

## 12. Consultas de verificación

```sql
-- Objetos del refactor
SELECT name, type_desc FROM sys.objects
WHERE schema_id = SCHEMA_ID('dbo')
  AND (name LIKE 'vw_%' OR name LIKE 'usp_%')
ORDER BY type_desc, name;

-- Tablas dim/fact
SELECT name FROM sys.tables
WHERE name LIKE 'dim_%' OR name LIKE 'fact_%'
ORDER BY name;
```

---

*Base de datos — Observatorio de Salud Departamental Casanare.*
