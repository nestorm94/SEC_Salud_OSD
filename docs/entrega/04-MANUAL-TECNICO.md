# 04 — Manual técnico

**Observatorio OSD — Instalación, configuración y despliegue**  
Versión 1.0 — Julio 2026

---

## 1. Requisitos previos

| Componente | Versión recomendada |
|------------|---------------------|
| Windows | Server 2019+ o Windows 10/11 |
| SQL Server | 2019 / 2022 (Express o superior) |
| .NET SDK / Runtime | Según `Observatorios.Api.csproj` |
| Node.js | 20 LTS (solo para compilar Angular) |
| IIS | 10+ con **ASP.NET Core Hosting Bundle** |
| Git | Para clonar el repositorio |

---

## 2. Clonar el repositorio

```powershell
git clone https://github.com/nestorm94/SEC_Salud_OSD.git
cd SEC_Salud_OSD
```

---

## 3. Base de datos

### 3.1 Crear base operativa

1. Crear base `ObservatorioDB` en SQL Server.
2. Ejecutar scripts en orden (ver [06-BASE-DE-DATOS.md](06-BASE-DE-DATOS.md) y `scripts/README-SQL-REFACTOR.md`):

```powershell
$S = "localhost\SQLEXPRESS2025"
$D = "ObservatorioDB"
$R = "c:\ruta\al\repo\scripts"

sqlcmd -S $S -d $D -E -b -i "$R\schema-bootstrap.sql"
sqlcmd -S $S -d $D -E -b -i "$R\schema-seed-minimo.sql"
# Fases 1 a 6 (+ fase 7 ASIS si aplica)
```

### 3.2 Permisos IIS

```powershell
sqlcmd -S $S -d ObservatorioDB -E -i scripts\grant-iis-observatorio-sql.sql
```

### 3.3 Ambiente ASIS de pruebas (opcional)

```powershell
sqlcmd -S $S -E -i scripts\asis-test-clone\00_backup_observatoriodb.sql
sqlcmd -S $S -E -i scripts\asis-test-clone\01_restore_observatoriodb_asis_test.sql
```

---

## 4. Configuración de la API

### 4.1 Archivos de configuración

| Archivo | Uso |
|---------|-----|
| `appsettings.json` | Valores base |
| `appsettings.Development.json` | Desarrollo local (puede apuntar a ASIS Test) |
| `appsettings.Production.json` | Producción IIS |

### 4.2 Cadena de conexión

```json
"ConnectionStrings": {
  "Default": "Server=localhost\\SQLEXPRESS2025;Database=ObservatorioDB;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

### 4.3 JWT

```json
"Jwt": {
  "Key": "<clave-secreta-minimo-32-caracteres>",
  "Issuer": "Observatorios.Api",
  "Audience": "Observatorios.Front",
  "ExpireMinutes": 720
}
```

> En producción use `dotnet user-secrets` o variables de entorno. Ver [docs/CONFIGURACION-SECRETOS.md](../CONFIGURACION-SECRETOS.md).

### 4.4 Bootstrap automático

| Entorno | Configuración |
|---------|---------------|
| Desarrollo | `SkipSchemaBootstrap: false` — crea tablas y admin al arrancar |
| Producción | `SkipSchemaBootstrap: true` — SQL manual |

### 4.5 Módulo ASIS

```json
"Asis": {
  "CapaPoblacion": "legacy",
  "IdProyeccionDaneDefault": 1
}
```

Valores: `legacy` (vistas DANE originales) o `fact` (tabla normalizada, requiere ETL en BD).

---

## 5. Desarrollo local

### 5.1 API

```powershell
# Desde la raíz del repositorio
.\ejecutar-api.ps1
```

- URL: `http://localhost:5289`
- Ping: `http://localhost:5289/api/ping`
- Swagger: `http://localhost:5289/swagger`

### 5.2 Frontend Angular

```bash
cd frontend
npm install
npm start
```

- URL: `http://localhost:4200`
- Proxy API: `frontend/proxy.conf.json` → `localhost:5289`

### 5.3 Acceso por red local

```powershell
.\ejecutar-red.ps1
```

Expone la API en `0.0.0.0:5289` para pruebas en LAN.

---

## 6. Compilar para producción

### 6.1 Frontend

```bash
cd frontend
npm run build
```

Salida: `frontend/dist/frontend/browser/`

### 6.2 API

```powershell
dotnet publish backend\Observatorios.Api\Observatorios.Api.csproj -c Release -o C:\Hosting\ObservatorioOSD
```

### 6.3 Script automatizado IIS

```powershell
.\scripts\publicar-iis.ps1
```

- Publica API + Angular en `C:\Hosting\ObservatorioOSD`
- Preserva carpeta `uploads/`
- Usa `app_offline.htm` durante el despliegue

### 6.4 Reciclar sitio

```powershell
.\scripts\reciclar-sitio-iis.ps1
```

---

## 7. IIS — Configuración

| Parámetro | Valor típico |
|-----------|--------------|
| Sitio | `ObservatorioOSD` |
| App pool | `ObservatorioOSDPool` |
| Puerto | `8081` |
| Ruta física | `C:\Hosting\ObservatorioOSD` |
| `web.config` | Incluido en publicación (ANCM in-process) |

**wwwroot:** Angular compilado o `public/` legacy según publicación.

---

## 8. Estructura de carpetas relevante

```
Observatorios_Salud_Departamental_Cas/
├── backend/Observatorios.Api/    # API, servicios, repositorios
│   ├── Auth/                     # JWT, contexto usuario
│   ├── Data/                     # Repositorios SQL
│   ├── Endpoints/                # Rutas minimal API
│   ├── Services/                 # Lógica de negocio
│   └── Program.cs
├── frontend/                     # Angular 19
├── public/                       # HTML legacy (deprecado)
├── scripts/                      # SQL y despliegue
├── uploads/                      # Archivos .xlsx físicos
├── docs/entrega/                 # Esta documentación
└── ejecutar-api.ps1
```

---

## 9. Monitoreo y salud

| Endpoint | Propósito |
|----------|-----------|
| `GET /health` | API viva |
| `GET /health/db` | Conexión SQL |
| `GET /api/ping` | Ping JSON |

---

## 10. Pruebas automatizadas

```powershell
dotnet test backend\Observatorios.Api.Tests\Observatorios.Api.Tests.csproj
```

---

## 11. Actualización en producción (orden recomendado)

1. **Backup** de `ObservatorioDB` y carpeta `uploads/`.
2. Ejecutar **scripts SQL** nuevos (si hay migraciones).
3. **Publicar** API + frontend (`publicar-iis.ps1`).
4. **Reciclar** app pool IIS.
5. Verificar `/health/db` y login.
6. Prueba funcional: cargue de prueba + consulta población.

---

## 12. Solución de problemas

| Síntoma | Causa | Solución |
|---------|-------|----------|
| HTTP 404 en login | API no arrancó o ruta incorrecta | Verificar `ejecutar-api.ps1` o IIS |
| 401 en todas las peticiones | JWT Key distinta entre despliegues | Unificar `Jwt:Key` |
| Error SQL al arrancar | BD inexistente o sin permisos | Verificar connection string y grant IIS |
| Angular sin datos | Proxy mal configurado | `npm start` con proxy a 5289 |
| Sesión expira rápido | Token sin renovar | Reiniciar API con `/auth/refresh`; ver [08-SEGURIDAD.md](08-SEGURIDAD.md) |
| DataReader abierto | Conexión SQL concurrente | Actualizar a última versión del código |

---

## 13. Referencias

- [05-ARQUITECTURA.md](05-ARQUITECTURA.md)
- [06-BASE-DE-DATOS.md](06-BASE-DE-DATOS.md)
- [07-API-ENDPOINTS.md](07-API-ENDPOINTS.md)
- [10-OPERACION-Y-SOPORTE.md](10-OPERACION-Y-SOPORTE.md)

---

*Manual técnico — Observatorio de Salud Departamental Casanare.*
