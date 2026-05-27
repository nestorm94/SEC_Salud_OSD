# Catálogo SQL Server — Observatorio OSD

Documentación de referencia para **desarrolladores** que mantengan o extiendan el backend. Describe vistas (`vw_`), funciones (`ufn_`), tipos tabla (`Tvp_`), procedimientos (`usp_`), scripts de despliegue y su relación con el código C#.

**Base de datos:** `ObservatorioDB`  
**Convención:** lecturas sin filtros → vista; lecturas con filtros → `usp_`; escrituras → `usp_` obligatorio. Los repositorios del refactor **solo invocan SP** (sin SQL inline duplicado en C#). Los scripts de las fases 1–6 deben estar aplicados en cada entorno.

---

## Índice

1. [Arquitectura](#arquitectura)
2. [Despliegue de scripts](#despliegue-de-scripts)
3. [Inventario por fase](#inventario-por-fase)
4. [Vistas (`vw_`)](#vistas-vw_)
5. [Función escalar](#función-escalar)
6. [Tipos tabla TVP](#tipos-tabla-tvp)
7. [Procedimientos (`usp_`)](#procedimientos-usp_)
8. [Mapa C# ↔ SQL](#mapa-c--sql)
9. [Endpoints API relacionados](#endpoints-api-relacionados)
10. [Qué permanece en C#](#qué-permanece-en-c)
11. [Documentación por fase](#documentación-por-fase)

---

## Arquitectura

```
Cliente (public/ o Angular)
    → API ASP.NET Core (Controllers / Minimal APIs)
        → Services (reglas de negocio, Excel, BCrypt)
            → Repositories (Data/*Repository.cs)
                → EXEC dbo.usp_...
```

| Tipo de operación | Objeto SQL preferido |
|-------------------|----------------------|
| Listado simple, sin parámetros | `vw_*` consumida por `usp_*` o directamente |
| Consulta con filtros / paginación | `usp_*` |
| INSERT / UPDATE / DELETE | `usp_*` (+ transacción en SQL cuando hay varias tablas) |
| Carga masiva (filas, errores, campos) | `usp_*` + **TVP** (`Tvp_*`) |

**Helpers en backend:**

| Archivo | Rol |
|---------|-----|
| `Data/SqlProcHelper.cs` | Utilidades CSV roles/áreas para SP de usuarios |
| `Data/SqlTvpHelper.cs` | Construye `DataTable` para TVP de cargas |
| `Data/DimCatalogSql.cs` | Constantes (ej. código DANE Casanare `85`) |

---

## Despliegue de scripts

Ejecutar **en orden** sobre `ObservatorioDB` (ajustar servidor según entorno):

```powershell
$S = "localhost\SQLEXPRESS2025"
$D = "ObservatorioDB"
$R = "c:\Users\Asus\Projects\Observatorios_Salud_Departamental_Cas\scripts"

sqlcmd -S $S -d $D -E -b -i "$R\sql-refactor-fase1-vistas-sp.sql"
sqlcmd -S $S -d $D -E -b -i "$R\sql-refactor-fase2-usuarios-roles-dependencias.sql"
sqlcmd -S $S -d $D -E -b -i "$R\sql-refactor-fase3-dashboard.sql"
sqlcmd -S $S -d $D -E -b -i "$R\sql-refactor-fase4-proyeccion-catalogos.sql"
sqlcmd -S $S -d $D -E -b -i "$R\sql-refactor-fase5-cargas-archivos-writes.sql"
```

Publicar API:

```powershell
.\scripts\publicar-iis.ps1
```

Verificar objetos creados:

```sql
SELECT name, type_desc FROM sys.objects
WHERE schema_id = SCHEMA_ID('dbo')
  AND (name LIKE 'vw_%' OR name LIKE 'usp_%' OR name LIKE 'ufn_%' OR name LIKE 'Tvp_%')
ORDER BY type_desc, name;
```

---

## Inventario por fase

| Fase | Script | Vistas | Funciones | TVP | SP |
|------|--------|--------|-----------|-----|-----|
| 1 | `sql-refactor-fase1-vistas-sp.sql` | 2 | — | — | 4 |
| 2 | `sql-refactor-fase2-usuarios-roles-dependencias.sql` | 4 | — | — | 14 |
| 3 | `sql-refactor-fase3-dashboard.sql` | 1 | — | — | 1 |
| 4 | `sql-refactor-fase4-proyeccion-catalogos.sql` | — | 1 | — | 7 |
| 5 | `sql-refactor-fase5-cargas-archivos-writes.sql` | 2 | — | 3 | 16 |
| **Total** | | **9** | **1** | **3** | **42** |

---

## Vistas (`vw_`)

| Vista | Fase | Descripción |
|-------|------|-------------|
| `vw_Cargas_Listado` | 1 | Listado de cargas con dependencia, archivo, usuario y conteo de errores |
| `vw_Carga_Historial` | 1 | Historial de acciones por carga con `DependenciaId` |
| `vw_Usuarios_Auth` | 2 | Usuario para login (incluye `PasswordHash`) |
| `vw_Usuarios_Listado` | 2 | Usuarios admin con `RolesCsv` agregado |
| `vw_Roles_Listado` | 2 | Catálogo de roles |
| `vw_Dependencias_Listado` | 2 | Dependencias activas/inactivas |
| `vw_Dashboard_UltimosCargues` | 3 | Base para “últimos cargues” del dashboard |
| `vw_Archivos_Listado` | 5 | Archivos con joins a dependencia, línea, indicador, usuario |
| `vw_Carga_Detalle` | 5 | Detalle de una carga (cabecera + archivo) |

**Nota:** El indicador de próstata **no** usa vista fija (`vw_Indicador_Prostata_Api` se descartó por columnas con tildes); `usp_Indicador_Prostata_Listar` resuelve nombres de columna por `column_id`.

---

## Función escalar

| Función | Fase | Retorno | Uso |
|---------|------|---------|-----|
| `ufn_Proyeccion_VistaDefault()` | 4 | `nvarchar(256)` | Primera vista de población disponible (`vw_Poblacion_Nacional_Casanare`, curso de vida o quinquenios) |

---

## Tipos tabla TVP

Solo fase 5. Se crean una vez (`IF TYPE_ID IS NULL`); **no** se pueden alterar fácilmente — crear tipo nuevo si cambia el esquema.

| Tipo | Columnas | SP que lo consume |
|------|----------|-------------------|
| `Tvp_CampoDiccionario` | NombreCampo, TipoDato, Obligatorio, Descripcion, Longitud, Formato, ValoresPermitidos, Orden | `usp_Carga_GuardarDiccionario` |
| `Tvp_DatosCargados` | NumeroFila, DatosJson | `usp_Carga_GuardarDatosBulk` |
| `Tvp_ErrorValidacion` | NumeroFila, NombreColumna, Mensaje, TipoError | `usp_Carga_GuardarErroresBulk` |

Construcción en C#: `SqlTvpHelper.CampoDiccionario`, `.DatosCargados`, `.ErroresValidacion`.

---

## Procedimientos (`usp_`)

### Fase 1 — Cargas e indicador próstata

| Procedimiento | Parámetros principales | Resultado | Repositorio C# |
|---------------|------------------------|-----------|----------------|
| `usp_Carga_Listar` | `@DependenciaId` | Filas de `vw_Cargas_Listado` | `CargasRepository.ListarAsync` |
| `usp_Carga_ListarPorUsuario` | `@UsuarioId`, `@DependenciaId` | Idem filtrado por usuario | `CargasRepository.ListarPorUsuarioAsync` |
| `usp_Carga_Historial_Listar` | `@CargaId`, `@DependenciaId` | Filas de `vw_Carga_Historial` | `CargasRepository.ListarHistorialAsync` |
| `usp_Indicador_Prostata_Listar` | `@CodigoDane`, `@Territorio`, `@Regional`, `@Anio`, `@Area`, `@MaxRows` | Filas indicador (SQL dinámico, coma decimal) | `IndicadoresRepository` |

### Fase 2 — Usuarios, roles, dependencias

| Procedimiento | Parámetros | Tipo | Repositorio C# |
|---------------|------------|------|----------------|
| `usp_Usuario_ObtenerPorNombre` | `@NombreUsuario` | Lectura | `UsuariosRepository` |
| `usp_Usuario_ObtenerPorEmail` | `@Email` | Lectura | `UsuariosRepository` |
| `usp_Usuario_ObtenerPorId` | `@Id` | Lectura | `UsuariosRepository` |
| `usp_Usuario_Listar` | — | Lectura | `UsuariosRepository` |
| `usp_Usuario_ObtenerRoles` | `@UsuarioId` | Lectura | `UsuariosRepository` |
| `usp_Usuario_ObtenerAreasTematicas` | `@UsuarioId` | Lectura | `UsuariosRepository` |
| `usp_Usuario_Crear` | datos usuario + `@PasswordHash` | Escritura | `UsuariosRepository` |
| `usp_Usuario_Actualizar` | datos usuario | Escritura | `UsuariosRepository` |
| `usp_Usuario_SetActivo` | `@Id`, `@Activo` | Escritura | `UsuariosRepository` |
| `usp_Usuario_ActualizarRoles` | `@UsuarioId`, `@RolesCsv` | Escritura | `UsuariosRepository` |
| `usp_Usuario_ActualizarAreasTematicas` | `@UsuarioId`, `@AreaIdsCsv` | Escritura | `UsuariosRepository` |
| `usp_Roles_Listar` | — | Lectura | `RolesRepository` |
| `usp_Dependencia_Listar` | — | Lectura | `DependenciasRepository` |
| `usp_Dependencia_ObtenerPorId` | `@Id` | Lectura | `DependenciasRepository` |
| `usp_Dependencia_Crear` | `@Codigo`, `@Nombre` | Escritura | `DependenciasRepository` |
| `usp_Dependencia_ObtenerOCrear` | `@Codigo`, `@Nombre` | Escritura | `DependenciasRepository` |

**Importante:** BCrypt se calcula en C#; el SP recibe `@PasswordHash` ya hasheado.

### Fase 3 — Dashboard

| Procedimiento | Parámetros | Result sets | Repositorio C# |
|---------------|------------|-------------|----------------|
| `usp_Dashboard_Resumen` | `@DependenciaId`, `@SubidoPorUsuarioId` | 1) KPIs: TotalArchivos, CargasPendientes, CargasConError, CargasAprobadas — 2) TOP 10 últimos cargues | `DashboardRepository` (`NextResultAsync`) |

### Fase 4 — Proyección y catálogos

| Procedimiento | Parámetros | Repositorio C# |
|---------------|------------|----------------|
| `usp_Catalogo_Departamentos_Listar` | — | `PoblacionCatalogosRepository` |
| `usp_Catalogo_Municipios_Listar` | `@CodigoDepartamento` | `PoblacionCatalogosRepository` |
| `usp_Catalogo_Regionales_Listar` | — | `PoblacionCatalogosRepository` |
| `usp_Catalogo_Areas_Listar` | — | `PoblacionCatalogosRepository` |
| `usp_Catalogo_Sexos_Listar` | — | `PoblacionCatalogosRepository` |
| `usp_Catalogo_Anios_Listar` | — | `PoblacionCatalogosRepository` |
| `usp_ProyeccionPoblacion_ConsultarPaginado` | `@Clave`, `@Pagina`, `@TamanoPagina`, filtros territorio/regional/área/sexo/año, `@CodigoDepartamento`, `@CodigoMunicipio` | `PoblacionVistasRepository` |

**`@Clave` válidas:** `nacional-casanare`, `curso-vida`, `quinquenios`.

**Result sets de proyección:** (1) `TotalFilas` bigint — (2) filas paginadas `SELECT *` de la vista.

**Filtro departamento/municipio:** subconsultas `IN` contra `dim_municipio` / `dim_departamento` (nombres de territorio).

### Fase 5 — Escrituras cargas y archivos

#### Archivos

| Procedimiento | Parámetros | Repositorio C# |
|---------------|------------|----------------|
| `usp_Archivo_Insertar` | metadata archivo; `@Estado` default `PendienteValidacion` | `ArchivosRepository.InsertAsync` |
| `usp_Archivo_ActualizarValidacion` | `@Id`, `@Estado`, `@ErroresValidacionJson` | `ArchivosRepository.ActualizarResultadoValidacionAsync` |
| `usp_Archivo_MarcarEnviado` | `@Id`, `@Estado` | `ArchivosRepository.MarcarEnviadoAsync` |
| `usp_Archivo_ObtenerEstado` | `@Id` | `ArchivosRepository.GetEstadoAsync` |
| `usp_Archivo_Obtener` | `@Id` | `ArchivosRepository.GetAsync` |
| `usp_Archivo_Listar` | `@DependenciaId`, `@LineaTematicaId`, `@SubidoPorUsuarioId` | `ArchivosRepository.ListAsync` (**sin TOP**) |
| `usp_Archivo_Eliminar` | `@Id` → `FilasAfectadas` | `ArchivosRepository.DeleteAsync` |

#### Cargas (escritura y detalle)

| Procedimiento | Parámetros | Repositorio C# |
|---------------|------------|----------------|
| `usp_Carga_Crear` | `@ArchivoId`, `@DependenciaId`, `@UsuarioId`, `@Estado` → `Id` | `CargasRepository.CrearCargaAsync` |
| `usp_Carga_ActualizarEstado` | `@Id`, `@Estado`, `@Observaciones` | `CargasRepository.ActualizarEstadoAsync` |
| `usp_Carga_RegistrarHistorial` | `@CargaId`, `@UsuarioId`, `@Accion`, `@Detalle` | `CargasRepository.RegistrarHistorialAsync` |
| `usp_Carga_Obtener` | `@Id` | `CargasRepository.GetCargaAsync` |
| `usp_Carga_Errores_Listar` | `@CargaId` | `CargasRepository.ListarErroresAsync` |
| `usp_Carga_LimpiarResultadosValidacion` | `@CargaId` (transacción) | `CargasRepository.LimpiarResultadosValidacionAsync` |
| `usp_Carga_GuardarDiccionario` | `@CargaId`, `@Campos` TVP, OUTPUT `@DiccionarioId` | `CargasRepository.GuardarDiccionarioAsync` |
| `usp_Carga_GuardarDatosBulk` | `@CargaId`, `@Filas` TVP | `CargasRepository.GuardarDatosAsync` |
| `usp_Carga_GuardarErroresBulk` | `@CargaId`, `@Errores` TVP | `CargasRepository.GuardarErroresAsync` |

### Fase 6 — Administración, ArchivoCarga y auditoría

#### Vistas
| Vista | Tablas base |
|-------|-------------|
| `vw_LineaTematica_Listado` | `LineaTematica` |
| `vw_Indicador_Listado` | `Indicador` + `LineaTematica` |
| `vw_Plantilla_Listado` | `PlantillasCarga` + `Dependencias` |
| `vw_AreaTematica_Listado` | `AreaTematica` + `Dependencias` |

#### Línea temática e indicador (app)
| Procedimiento | Parámetros | Repositorio C# |
|---------------|------------|----------------|
| `usp_LineaTematica_Listar` | `@SoloActivas` | `LineaTematicaRepository` |
| `usp_LineaTematica_Obtener` | `@Id` | `LineaTematicaRepository` |
| `usp_LineaTematica_Crear` / `Actualizar` | datos línea | `LineaTematicaRepository` |
| `usp_LineaTematica_ContarIndicadores` | `@Id` | `LineaTematicaRepository` |
| `usp_Indicador_Listar` | `@LineaTematicaId`, `@SoloActivas` | `IndicadorRepository` |
| `usp_Indicador_Obtener` | `@Id` | `IndicadorRepository` |
| `usp_Indicador_ObtenerColumnasObligatoriasJson` | `@Id` | `IndicadorRepository` |
| `usp_Indicador_PerteneceALinea` | `@IndicadorId`, `@LineaTematicaId` | `IndicadorRepository` |
| `usp_Indicador_Crear` / `Actualizar` | datos indicador | `IndicadorRepository` |

**Nota:** `usp_Indicador_Prostata_Listar` (fase 1) es distinto; lo usa `IndicadoresRepository` (API pública).

#### Plantillas, áreas, ArchivoCarga, auditoría
| Procedimiento | Repositorio C# |
|---------------|----------------|
| `usp_Plantilla_Listar`, `Crear`, `Actualizar` | `PlantillasRepository` |
| `usp_Plantilla_Campos_Listar`, `usp_Plantilla_Campo_Crear`, `usp_Plantilla_Campo_Eliminar` | `PlantillasRepository` |
| `usp_AreaTematica_Listar`, `usp_AreaTematica_Crear` | `AreaTematicaRepository` |
| `usp_ArchivoCarga_Sincronizar`, `usp_ArchivoCarga_ActualizarEstadoPorCarga` | `ArchivoCargaRepository` |
| `usp_Auditoria_Registrar`, `usp_Auditoria_Listar` | `AuditoriaRepository` |

**Catálogo validación Excel:** `CatalogoRepository` → `usp_Catalogo_Departamentos_Listar` + `usp_Catalogo_Municipios_Listar` (sin SP nuevo; reutiliza fase 4).

---

## Mapa C# ↔ SQL

| Archivo repositorio | Objetos SQL usados |
|---------------------|-------------------|
| `CargasRepository.cs` | Fase 1 listados/historial + Fase 5 escrituras y detalle |
| `ArchivosRepository.cs` | Fase 5 completo |
| `DashboardRepository.cs` | `usp_Dashboard_Resumen` |
| `UsuariosRepository.cs` | Fase 2 usuarios |
| `RolesRepository.cs` | `usp_Roles_Listar` |
| `DependenciasRepository.cs` | Fase 2 dependencias |
| `IndicadoresRepository.cs` | `usp_Indicador_Prostata_Listar` |
| `PoblacionCatalogosRepository.cs` | `usp_Catalogo_*` |
| `PoblacionVistasRepository.cs` | `usp_ProyeccionPoblacion_ConsultarPaginado` |
| `LineaTematicaRepository.cs` | Fase 6 líneas temáticas |
| `IndicadorRepository.cs` | Fase 6 indicadores app |
| `PlantillasRepository.cs` | Fase 6 plantillas y campos |
| `AreaTematicaRepository.cs` | Fase 6 áreas temáticas |
| `ArchivoCargaRepository.cs` | Fase 6 vínculo ArchivoCarga |
| `AuditoriaRepository.cs` | Fase 6 auditoría |
| `CatalogoRepository.cs` | `usp_Catalogo_Departamentos_Listar`, `usp_Catalogo_Municipios_Listar` (validación Excel) |

**Servicios que orquestan (sin SQL directo):**

- `CargaArchivoService` — flujo Excel → `CargasRepository` + `ArchivosRepository` + `CatalogoRepository` + `ArchivoCargaRepository`
- `ArchivoFlujoService` — prevalidación y envío
- `CatalogoService` — caché 6 h sobre catálogos de proyección
- `ExcelValidationService` / `OscPlantillaValidacionService` — validación en memoria

**Controlador público:**

- `Controllers/PublicIndicadoresController.cs` → `GET /api/public/indicadores/prostata`

---

## Endpoints API relacionados

| Método | Ruta | Autenticación | SQL / repositorio |
|--------|------|---------------|-------------------|
| GET | `/api/cargas` | JWT | `usp_Carga_Listar` |
| GET | `/api/cargas/mis-cargas` | JWT | `usp_Carga_ListarPorUsuario` |
| GET | `/api/cargas/{id}` | JWT | `usp_Carga_Obtener` |
| GET | `/api/cargas/{id}/errores` | JWT | `usp_Carga_Errores_Listar` |
| GET | `/api/cargas/historial` | JWT | `usp_Carga_Historial_Listar` |
| GET | `/api/archivos` | JWT | `usp_Archivo_Listar` |
| GET | `/api/dashboard/resumen` | JWT | `usp_Dashboard_Resumen` |
| GET | `/api/catalogos/*` | JWT | `usp_Catalogo_*` |
| GET | `/api/proyeccion-poblacion/*` | JWT | `usp_ProyeccionPoblacion_ConsultarPaginado` |
| GET | `/api/public/indicadores/prostata` | Público | `usp_Indicador_Prostata_Listar` |
| POST | `/api/auth/login` | Público | `usp_Usuario_ObtenerPorNombre` + BCrypt en C# |

Rutas admin de usuarios/roles/dependencias: ver `Endpoints/ApiEndpoints.cs` y repositorios de fase 2.

---

## Qué permanece en C#

No migrar a SQL (por diseño):

| Responsabilidad | Ubicación |
|-----------------|-----------|
| Validación de plantillas Excel (.xlsx) | `ExcelValidationService`, `OscPlantillaValidacionService`, `DiccionarioOscV2Reader` |
| Hash y verificación de contraseñas | BCrypt en servicios de auth |
| Autorización por rol / dependencia / línea | `AuthorizationService`, `UserContext` |
| Almacenamiento de archivos en disco | rutas bajo `uploads/`, IIS |
| Caché en memoria | `CatalogoService`, `PoblacionVistasRepository` (3 min), catálogos (6 h) |
| Registro de auditoría en BD | `AuditoriaRepository` → `usp_Auditoria_*` |
| DDL inicial y seed mínimo | `scripts/schema-bootstrap.sql`, `scripts/schema-seed-minimo.sql` (también vía `ObservatorioDbSchema` en dev) |
| Usuario admin al arrancar | `ObservatorioDbSchema` → `usp_Usuario_*` si fase 2 aplicada; si no, SQL parametrizado + BCrypt en C# |

**Bootstrap:** en **producción** (`appsettings.Production.json`) `Observatorio:SkipSchemaBootstrap: true` — el esquema se despliega solo con `sqlcmd`. En **desarrollo** la API ejecuta los scripts SQL al iniciar.

---

## Convenciones para nuevos desarrollos

1. **Nuevo listado:** crear `vw_MiEntidad_Listado` + `usp_MiEntidad_Listar` con filtros como parámetros.
2. **Nueva escritura:** solo `usp_`; transacción en SQL si toca varias tablas.
3. **Bulk:** preferir TVP + un SP; en C# usar `SqlTvpHelper` como plantilla.
4. **Backend:** añadir método en el repositorio que llame al nuevo `usp_` (sin duplicar SQL en C#).
5. **Sin `TOP` arbitrario** en listados de negocio (usar paginación explícita).
6. **Nombres de parámetros SP:** evitar colisión con variables locales (`@Territorio` ≡ `@territorio` en SQL Server — usar prefijo `@pTerritorio` en SQL dinámico).
7. **Columnas opcionales en `dim_*`:** usar `sp_executesql` en el SP para no fallar la compilación si la tabla existe sin esa columna.

---

## Documentación por fase

Diagnósticos y notas de validación (más detalle operativo):

| Fase | Documento |
|------|-----------|
| 1 | [sql-refactor-diagnostico-fase1.md](./sql-refactor-diagnostico-fase1.md) |
| 2 | [sql-refactor-diagnostico-fase2.md](./sql-refactor-diagnostico-fase2.md) |
| 3 | [sql-refactor-diagnostico-fase3-dashboard.md](./sql-refactor-diagnostico-fase3-dashboard.md) |
| 4 | [sql-refactor-diagnostico-fase4-proyeccion-catalogos.md](./sql-refactor-diagnostico-fase4-proyeccion-catalogos.md) |
| 5 | [sql-refactor-diagnostico-fase5-cargas-archivos.md](./sql-refactor-diagnostico-fase5-cargas-archivos.md) |
| 6 | [sql-refactor-diagnostico-fase6.md](./sql-refactor-diagnostico-fase6.md) |

Arquitectura general del producto: [ARQUITECTURA.md](./ARQUITECTURA.md).

---

## Archivos modificados en el repositorio (resumen)

### Scripts SQL (`scripts/`)

- `sql-refactor-fase1-vistas-sp.sql`
- `sql-refactor-fase2-usuarios-roles-dependencias.sql`
- `sql-refactor-fase3-dashboard.sql`
- `sql-refactor-fase4-proyeccion-catalogos.sql`
- `sql-refactor-fase5-cargas-archivos-writes.sql`
- `sql-refactor-fase6-admin-catalogos.sql`
- `schema-bootstrap.sql`, `schema-seed-minimo.sql`

### Backend (`backend/Observatorios.Api/`)

- `Data/CargasRepository.cs`, `ArchivosRepository.cs`, `DashboardRepository.cs`
- `Data/UsuariosRepository.cs`, `RolesRepository.cs`, `DependenciasRepository.cs`
- `Data/IndicadoresRepository.cs`, `PoblacionCatalogosRepository.cs`, `PoblacionVistasRepository.cs`
- `Data/LineaTematicaRepository.cs`, `IndicadorRepository.cs`, `PlantillasRepository.cs`, `AreaTematicaRepository.cs`
- `Data/ArchivoCargaRepository.cs`, `AuditoriaRepository.cs`, `CatalogoRepository.cs`
- `Data/SqlProcHelper.cs`, `SqlTvpHelper.cs`, `DimCatalogSql.cs`
- `Services/CatalogoService.cs`, `GeografiaValidacionService.cs`, …
- `Controllers/PublicIndicadoresController.cs`
- `Endpoints/ApiEndpoints.cs`, `Program.cs`

### Frontend / portal

- `public/proyeccion-poblacion.html`, `public/js/proyeccion-poblacion.js`
- `frontend/.../poblacion/*` (Angular)

---

*Última actualización: refactor fases 1–6 (mayo 2026).*
