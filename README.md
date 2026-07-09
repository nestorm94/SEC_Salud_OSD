# SEC Salud OSD — Observatorios Salud Departamental Casanare

Repositorio GitHub: [nestorm94/SEC_Salud_OSD](https://github.com/nestorm94/SEC_Salud_OSD)

# Observatorios Salud Departamental Casanare

Plataforma para la **Secretaría de Salud**: autenticación por dependencia, **carga y validación de archivos Excel**, consulta de proyección de población y base para futuros dashboards del observatorio.

- **Frontend Angular:** `frontend/` (recomendado) — `npm start` en puerto 4200 con proxy a la API
- **Frontend legacy:** HTML estático en `/public` (deprecado)
- **Backend:** ASP.NET Core minimal API (`/backend/Observatorios.Api`)
- **Datos:** SQL Server (LocalDB en desarrollo)
- **Seguridad:** JWT, contraseñas con BCrypt
- **Excel:** ClosedXML — hojas obligatorias `Diccionario_Datos` y `Datos`

## Inicio rápido

1. LocalDB (si aplica):

   ```powershell
   sqllocaldb start MSSQLLocalDB
   ```

2. Ejecutar la API desde la raíz del repositorio:

   ```powershell
   .\ejecutar-api.ps1
   ```

3. **Frontend Angular** (recomendado):

   ```bash
   cd frontend
   npm install
   npm start
   ```

   Abrir **http://localhost:4200/login** (proxy API → 5289).

   O bien el HTML legacy: **http://localhost:5289/login.html**

4. Credenciales iniciales (solo desarrollo):

   | Usuario / correo | Contraseña |
   |------------------|------------|
   | `admin@observatorio.gov.co` o `admin` | `Admin123*` |

   Cambie la contraseña del administrador en entornos reales.

Al arrancar en desarrollo, la API aplica `scripts/schema-bootstrap.sql` (idempotente), crea el usuario **ADMIN** si no existe e intenta importar áreas desde `data/Áreas temáticas OSC V.2.xlsx` si está presente. En producción/IIS use `Observatorio:SkipSchemaBootstrap: true` y despliegue SQL manual (ver `scripts/README-SQL-REFACTOR.md`).

**Monitoreo:** `GET /health` (API viva) · `GET /health/db` (conexión SQL).

**Pruebas:** `dotnet test backend/Observatorios.Api.Tests/Observatorios.Api.Tests.csproj`

**Secretos:** ver [docs/CONFIGURACION-SECRETOS.md](docs/CONFIGURACION-SECRETOS.md).

## Documentación de entrega final

Paquete completo para la Secretaría de Salud en **[docs/entrega/00-INDICE-ENTREGA.md](docs/entrega/00-INDICE-ENTREGA.md)** — memoria descriptiva, manuales de usuario/administrador/técnico, arquitectura, base de datos, API, seguridad, pruebas y acta de entrega.

**Guía para desarrolladores:** [docs/GUIA-LECTURA-CODIGO.md](docs/GUIA-LECTURA-CODIGO.md) — estructura del código, convenciones de comentarios y usuarios de prueba.

## Estructura del proyecto

| Carpeta | Contenido |
|---------|-----------|
| `public/` | Portal HTML: login, dashboard, cargas, validaciones, administración |
| `data/` | Excel de áreas temáticas (opcional, ver `data/README.md`) |
| `frontend/` | Cliente Angular (migración futura) |
| `backend/Observatorios.Api/` | API, servicios de validación Excel, repositorios |
| `backend/.../Auth/` | JWT y contexto de usuario |
| `backend/.../Services/` | Autenticación, validación Excel, proceso de cargue |
| `backend/.../Endpoints/` | Rutas `/api/*` |
| `uploads/` | Archivos físicos `.xlsx` |
| `scripts/` | Scripts SQL de referencia |

## Modelo funcional

```
Dependencia → Área temática → Responsable temático → Plantilla de carga → Archivo → Validación
```

Compatibilidad: se mantiene `dbo.Archivos` / `CargasArchivo` (evolución de `dbo.Archivo`) y `uploads/`.

## Modelo de datos (SQL Server)

| Tabla | Propósito |
|-------|-----------|
| `Dependencias`, `AreaTematica`, `UsuarioAreaTematica` | Organización territorial/temática |
| `Roles`, `Usuarios`, `UsuarioRol` | Autenticación (BCrypt) y autorización |
| `ResponsableTematico`, `PlantillaCarga`, `PlantillaCampo` | Plantillas por área |
| `Archivos`, `ArchivoCarga` | Archivo físico + contexto de cargue v2 |
| `CargasArchivo`, `DiccionarioArchivo`, `CamposDiccionario` | Flujo de validación Excel |
| `DatosCargados`, `ErroresValidacion`, `ValidacionArchivo` | Resultados |
| `HistorialCarga`, `AuditoriaSistema` | Trazabilidad |

Script idempotente completo: `scripts/schema-completo-areas-tematicas.sql`. La migración en runtime: `ObservatorioDbSchema.cs`.

**Estados de cargue:** `RECIBIDO`, `EN_VALIDACION`, `VALIDADO_CON_ERRORES`, `VALIDADO_EXITOSO`, `APROBADO`, `RECHAZADO`, `CARGADO_BD` (alias v1: `VALIDANDO`, `VALIDADO_OK`).

## Roles

| Rol | Alcance |
|-----|---------|
| `ADMIN` | Todo; menú administración |
| `COORDINADOR_DEPENDENCIA` | Su dependencia |
| `RESPONSABLE_TEMATICO` | Carga/consulta en sus áreas |
| `VALIDADOR` | Revisión y aprobación |
| `CONSULTA` | Solo lectura |
| `AUDITOR` | Historial y `/api/auditoria` |

## Formato Excel obligatorio

### Plantilla oficial OSC V.2 (`Diccionario_de_datos_OSC.V2.xlsx`)

Hoja **`Diccionario_Datos`** (encabezados en fila 5, tras metadatos de la tabla):

| Columna | Uso |
|---------|-----|
| Nombre de la variable | Nombre de columna en hoja Datos |
| Descripción de la variable | Texto descriptivo |
| Llave Primaria | SI / NO |
| Llave Foránea | SI / NO (infiere catálogo DIVIPOLA/municipios) |
| Campo obligatorio | SI / NO |
| Id. de la variable | Identificador interno |
| Tipo de datos | Numérico, Carácter, Fecha, etc. |
| Longitud | Ej. `5` o `p(10,2)` |
| Dominios (Categorías, valores) | Valores permitidos |
| Unidad de medida | Metadato |
| Campo calculado | SI / NO |
| Fórmula aplicada | Fórmula si aplica |

Hoja **`Datos`:** columnas con los mismos nombres que «Nombre de la variable» (ej. `CODIGO DIVIPOLA`, `MUNICIPIO`). La plantilla puede subirse solo con diccionario (Datos vacía).

Copia de referencia: `data/Diccionario_de_datos_OSC.V2.xlsx`

### Formato simplificado (legacy)

1. **Hoja `Diccionario_Datos`:** `Nombre_Campo`, `Tipo_Dato`, `Obligatorio`, `Descripcion`, `Longitud`, `Formato`, `Valores_Permitidos`, `Tabla_Referencia`, `Campo_Referencia`

2. **Hoja `Datos`:** columnas según el diccionario.

3. **Catálogos:** si `Tabla_Referencia` es `dim_departamentos` o `dim_municipios`, se valida contra tablas existentes; el municipio debe pertenecer al departamento de la misma fila.

4. **Tipos:** `texto`, `entero`, `decimal`, `fecha`, `booleano`.

## Permisos

- Usuario con **dependencia** y opcionalmente **áreas temáticas** (`UsuarioAreaTematica`).
- JWT incluye roles y claim `areas` (IDs).
- **ADMIN** ve todo; resto según rol (ver tabla arriba).

## API (requiere JWT salvo login y ping)

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/auth/login` | Token JWT |
| POST | `/api/usuarios` | Crear usuario (admin) |
| POST | `/api/dependencias` | Crear dependencia (admin) |
| GET | `/api/dependencias` | Listar dependencias |
| POST | `/api/cargas/upload` o `/api/cargas/excel` | Subir Excel (`archivo`, opc. `dependencia_id`, `area_tematica_id`, `plantilla_carga_id`) |
| GET | `/api/cargas/mis-cargas` | Cargues del usuario autenticado |
| POST | `/api/cargas/{id}/validar` | Revalidar (VALIDADOR/ADMIN) |
| GET | `/api/cargas/{id}/errores` | Errores de validación |
| POST | `/api/cargas/{id}/aprobar` | Aprobar (`VALIDADO_EXITOSO`) |
| POST | `/api/cargas/{id}/rechazar` | Rechazar |
| GET | `/api/cargas` | Listar cargues |
| GET | `/api/auditoria` | Auditoría (ADMIN/AUDITOR) |
| GET/POST | `/api/admin/usuarios` | Gestión usuarios |
| GET | `/api/admin/roles` | Roles |
| GET/POST | `/api/admin/dependencias` | Dependencias |
| GET/POST | `/api/admin/areas-tematicas` | Áreas temáticas |
| POST | `/api/admin/areas-tematicas/importar-excel` | Importar desde `data/` |
| GET/POST | `/api/admin/plantillas` | Plantillas de carga |
| GET | `/api/archivos` | Archivos por dependencia |
| GET | `/api/archivos/{id}/descargar` | Descarga |

**Proyección población** (vistas SQL en `ObservatorioDB`, también con JWT):

- `GET /api/proyeccion-poblacion/nacional-casanare`
- `GET /api/proyeccion-poblacion/curso-vida`
- `GET /api/proyeccion-poblacion/quinquenios`

## Configuración JWT

`backend/Observatorios.Api/appsettings.json`:

```json
"Jwt": {
  "Key": "clave-secreta-minimo-32-caracteres",
  "Issuer": "Observatorios.Api",
  "Audience": "Observatorios.Front",
  "ExpireMinutes": 480
}
```

## Pantallas web (HTML en `public/`)

| URL | Uso |
|-----|-----|
| `/login.html` | JWT (correo o usuario) |
| `/dashboard.html` | Panel y menú por rol |
| `/cargas.html` | Carga Excel e historial |
| `/validaciones.html` | Aprobar/rechazar (VALIDADOR) |
| `/admin/*.html` | Usuarios, roles, dependencias, áreas, plantillas (solo ADMIN) |
| `/index.html`, `/proyeccion-poblacion.html` | Legacy (siguen operativos) |

Preparado para migrar a **Angular** (`frontend/`) reutilizando los mismos endpoints.

## Conexión SQL

- Por defecto: `(localdb)\MSSQLLocalDB`, base `ObservatorioDB`.
- La consola al iniciar muestra el servidor efectivo.
- Script de referencia: `scripts/schema-seguridad-cargas.sql` (la migración real la hace la API).

## Evolución prevista

El código está organizado en capas (esquema, repositorios, servicios, endpoints) para extender después **dashboards e indicadores** del Observatorio de Salud sin reescribir el núcleo de cargues y seguridad.
