# Guía de lectura del código — Observatorio OSD Casanare

Esta guía orienta a cualquier desarrollador que revise el repositorio por primera vez.

## Estructura del repositorio

| Carpeta | Contenido |
|---------|-----------|
| `backend/Observatorios.Api/` | API REST .NET 10 (minimal APIs) |
| `frontend/` | Aplicación Angular 19 |
| `scripts/` | SQL, despliegue IIS, carga de datos ASIS |
| `docs/entrega/` | Documentación oficial de entrega a la Secretaría |
| `public/` | Sitio estático legacy (fallback sin build Angular) |

## Convención de comentarios

- **Backend C#**: comentarios XML `/// <summary>` en clases y métodos públicos. Las consultas SQL embebidas indican vista, tabla o procedimiento usado.
- **Frontend TypeScript**: JSDoc `/** ... */` en servicios, componentes, layout, shared y métodos exportados.
- **Portal legacy** (`public/js/`): JSDoc `@fileoverview` y funciones principales del sitio HTML estático.
- **Scripts SQL**: bloque de encabezado con propósito, BD destino, dependencias y orden de ejecución; comentarios `--` en vistas, SPs y filtros geográficos (Casanare = código `85`).
- **Scripts PowerShell** (`scripts/*.ps1`): comentarios `#` por sección, parámetros y pasos de despliegue/carga.
- **Tests** (`Observatorios.Api.Tests/`): documentación de cada suite de pruebas automatizadas.

> Cobertura de documentación en código: **v1.0.2** — backend, frontend Angular, SQL ASIS, PowerShell, portal legacy y tests.

## Flujo de arranque (backend)

1. `Program.cs` — configuración DI, JWT, CORS, health checks, seeds.
2. `ObservatorioDbSchema.EnsureAllAsync()` — tablas mínimas y usuario admin.
3. Seeds — líneas temáticas, áreas, usuarios de prueba (`UsuariosPruebaSeedService`).
4. `ApiEndpoints`, `AdminEndpoints`, `AsisEndpoints` — registro de rutas.

## Capas backend

```
Endpoints/     → Rutas HTTP (validación, autorización, respuesta)
Services/      → Lógica de negocio (auth, cargues, Excel, ASIS export)
Data/          → Acceso SQL Server (repositorios, helpers TVP/SP)
Auth/          → JWT, UserContext, extensiones
Models/        → DTOs, constantes de estados y roles
```

## Capas frontend

```
core/auth/         → Login, refresh JWT, keepalive de sesión
core/guards/       → Protección de rutas (auth, admin)
core/interceptors/ → Token Bearer y reintento ante 401
modules/           → Pantallas por dominio (archivos, ASIS, admin…)
shared/models/     → Tipos compartidos con la API
```

## Módulos funcionales principales

| Módulo | Backend | Frontend |
|--------|---------|----------|
| Autenticación | `AuthService`, `ApiEndpoints` `/auth/*` | `auth.service.ts`, `login.component` |
| Cargues Excel | `CargaArchivoService`, `ExcelValidationService` | `archivos/` |
| Validaciones | `ArchivoFlujoService`, `CargasRepository` | `validaciones/` |
| Población | `PoblacionVistasRepository` | `poblacion/` |
| ASIS | `AsisRepository`, `AsisEndpoints`, `AsisExcelExportService` | `asis/` |
| Administración | `AdminEndpoints`, repos CRUD | `administracion/` |
| API pública | `PublicIndicadoresController` | `prostata/` (consulta interna) |

## Base de datos y scripts ASIS

- BD desarrollo ASIS: `ObservatorioDB_ASIS_Test`
- BD producción: `ObservatorioDB`
- Pipeline ASIS en `scripts/asis-test-clone/` (numerados 00–25)
- Vistas analíticas: prefijo `vw_ASIS_*`
- Normalización: `usp_normalizar_*` y tablas `fact_*`

## Usuarios de prueba (desarrollo)

| Usuario | Contraseña | Rol |
|---------|------------|-----|
| `admin` | `Admin123*` | ADMIN |
| `validador` | `Prueba123*` | VALIDADOR |
| `coordinador` | `Prueba123*` | COORDINADOR_DEPENDENCIA |
| `prueba.aseg` … `prueba.econ` | `Prueba123*` | RESPONSABLE_TEMATICO |

> Cambiar contraseñas y `Jwt:Key` antes de producción. Ver `docs/entrega/08-SEGURIDAD.md`.

## Cómo ejecutar

```powershell
# API
cd backend/Observatorios.Api
dotnet run

# Frontend (proxy a API)
cd frontend
npm start
```

## Versión de entrega

- **Tag estable:** `v1.0.0` — entrega funcional ASIS + documentación
- **Tag documentado:** `v1.0.1` — comentarios en código funcional principal
- **Tag completo:** `v1.0.2` — layout, shared, SQL restante, PowerShell, portal legacy y tests
- **Rama principal:** `main`
