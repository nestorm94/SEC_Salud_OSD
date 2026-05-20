# Alias: ejecutar la API. Mejor use ..\ejecutar-api.ps1 desde la raíz del repo.
$ErrorActionPreference = "Continue"
if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
    sqllocaldb start MSSQLLocalDB 2>$null | Out-Null
}
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "backend\Observatorios.Api\Observatorios.Api.csproj"
Write-Host "dotnet run --project $proj --launch-profile http" -ForegroundColor DarkGray
dotnet run --project $proj --launch-profile http @args
