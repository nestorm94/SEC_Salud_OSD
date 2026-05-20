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

   | Usuario | Contraseña |
   |---------|------------|
   | `admin` | `Admin123!` |

   Cambie la contraseña del administrador en entornos reales.

Al arrancar, la API crea el esquema SQL (usuarios, roles, dependencias, cargas, validación) y el usuario `admin` si no existe.

## Estructura del proyecto

| Carpeta | Contenido |
|---------|-----------|
| `public/` | Login, carga Excel, historial de cargues, proyección población |
| `backend/Observatorios.Api/` | API, servicios de validación Excel, repositorios |
| `backend/.../Auth/` | JWT y contexto de usuario |
| `backend/.../Services/` | Autenticación, validación Excel, proceso de cargue |
| `backend/.../Endpoints/` | Rutas `/api/*` |
| `uploads/` | Archivos físicos `.xlsx` |
| `scripts/` | Scripts SQL de referencia |

## Modelo de datos (SQL Server)

| Tabla | Propósito |
|-------|-----------|
| `Dependencias` | Secretarías / áreas del observatorio |
| `Roles`, `Usuarios`, `UsuarioRol` | Autenticación y autorización |
| `Archivos` | Metadata del archivo por dependencia |
| `CargasArchivo` | Proceso de cargue y estado |
| `DiccionarioArchivo`, `CamposDiccionario` | Estructura leída de la hoja Diccionario_Datos |
| `DatosCargados` | Filas válidas de la hoja Datos (JSON por fila) |
| `ErroresValidacion` | Reporte fila / columna / mensaje |
| `HistorialCarga` | Auditoría del flujo (validación, aprobación, rechazo) |

**Estados de cargue:** `VALIDANDO`, `VALIDADO_OK`, `VALIDADO_CON_ERRORES`, `APROBADO`, `RECHAZADO`.

## Formato Excel obligatorio

1. **Hoja `Diccionario_Datos`** con columnas:  
   `Nombre_Campo`, `Tipo_Dato`, `Obligatorio`, `Descripcion`, `Longitud`, `Formato`, `Valores_Permitidos`

2. **Hoja `Datos`** con columnas alineadas al diccionario.

3. **Tipos de dato:** `texto`, `entero`, `decimal`, `fecha`, `booleano`.

4. Si hay errores → estado `VALIDADO_CON_ERRORES` + registros en `ErroresValidacion`.  
   Si es válido → `VALIDADO_OK` + diccionario y datos persistidos.

## Permisos por dependencia

- Cada usuario (excepto **Administrador**) pertenece a una **dependencia** y solo ve/gestiona archivos y cargues de esa dependencia.
- **Administrador:** acceso global; puede indicar `dependencia_id` al subir Excel.

## API (requiere JWT salvo login y ping)

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/auth/login` | Token JWT |
| POST | `/api/usuarios` | Crear usuario (admin) |
| POST | `/api/dependencias` | Crear dependencia (admin) |
| GET | `/api/dependencias` | Listar dependencias |
| POST | `/api/cargas/excel` | Subir y validar Excel |
| POST | `/api/cargas/{id}/validar` | Revalidar (opcional nuevo archivo) |
| GET | `/api/cargas/{id}/errores` | Reporte de errores |
| POST | `/api/cargas/{id}/aprobar` | Aprobar (solo `VALIDADO_OK`) |
| POST | `/api/cargas/{id}/rechazar` | Rechazar cargue |
| GET | `/api/cargas` | Listar cargues (filtrado por dependencia) |
| GET | `/api/cargas/historial` | Historial (`?carga_id=`) |
| GET | `/api/archivos` | Archivos por dependencia |
| GET | `/api/archivos/{id}/descargar` | Descarga con token |

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

## Pantallas web

| URL | Uso |
|-----|-----|
| `/login.html` | Inicio de sesión |
| `/index.html` | Subir Excel y listar archivos |
| `/cargas.html` | Historial, errores, aprobar/rechazar |
| `/proyeccion-poblacion.html` | Consulta de vistas |

## Conexión SQL

- Por defecto: `(localdb)\MSSQLLocalDB`, base `ObservatorioDB`.
- La consola al iniciar muestra el servidor efectivo.
- Script de referencia: `scripts/schema-seguridad-cargas.sql` (la migración real la hace la API).

## Evolución prevista

El código está organizado en capas (esquema, repositorios, servicios, endpoints) para extender después **dashboards e indicadores** del Observatorio de Salud sin reescribir el núcleo de cargues y seguridad.
