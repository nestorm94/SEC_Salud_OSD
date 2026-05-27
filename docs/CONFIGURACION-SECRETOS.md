# Configuración de secretos — Observatorio OSD

No versionar contraseñas reales ni claves JWT de producción en Git.

## Desarrollo local (User Secrets)

Desde `backend/Observatorios.Api`:

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost\SQLEXPRESS2025;Database=ObservatorioDB;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet user-secrets set "Jwt:Key" "SuClaveMinimo32CaracteresParaDesarrollo!!"
```

El proyecto usa `UserSecretsId`: `observatorios-api-dev`.

## IIS / producción

Variables de entorno (doble guion bajo `__` = sección anidada):

| Variable | Ejemplo |
|----------|---------|
| `ConnectionStrings__Default` | Cadena SQL Server |
| `Jwt__Key` | Clave JWT ≥ 32 caracteres |
| `Observatorio__SkipSchemaBootstrap` | `true` |
| `Observatorio__SkipStartupSeeds` | `true` |

Archivo opcional fuera del repo: `C:\Hosting\ObservatorioOSD\appsettings.local.json` (no commitear).

## Plantilla

Copiar `backend/Observatorios.Api/appsettings.Development.example.json` y ajustar valores locales.
