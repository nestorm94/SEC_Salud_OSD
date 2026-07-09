# Alias: ejecutar la API. Mejor use ..\ejecutar-api.ps1 desde la raíz del repo.

# Configuración de errores y arranque de SQL LocalDB si está disponible
$ErrorActionPreference = "Continue"
if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
    sqllocaldb start MSSQLLocalDB 2>$null | Out-Null
}

# Resolución de rutas al proyecto de la API
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "backend\Observatorios.Api\Observatorios.Api.csproj"

# Ejecución de la API con perfil http (acepta argumentos adicionales vía @args)
Write-Host "dotnet run --project $proj --launch-profile http" -ForegroundColor DarkGray
dotnet run --project $proj --launch-profile http @args
