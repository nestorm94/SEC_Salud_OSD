# Ejecutar la API desde la RAÍZ del repositorio (obligatorio --project).
# Si en la raíz solo pone "dotnet run", la API NO arranca y el navegador dará HTTP 404.
$ErrorActionPreference = "Continue"
if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
    sqllocaldb start MSSQLLocalDB 2>$null | Out-Null
}
$proj = Join-Path $PSScriptRoot "backend\Observatorios.Api\Observatorios.Api.csproj"
if (-not (Test-Path $proj)) {
    Write-Error "No se encuentra el proyecto: $proj"
    exit 1
}
Write-Host "Iniciando API (puerto 5289 por defecto). Pruebe luego: http://localhost:5289/api/ping" -ForegroundColor Cyan
dotnet run --project $proj --launch-profile http @args
