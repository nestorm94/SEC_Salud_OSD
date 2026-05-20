# Ejecute ESTE script en PowerShell (ventana normal, no desde Cursor sin interaccion).
# Abrira el asistente de inicio de sesion de GitHub para completar el push.

$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
Set-Location (Split-Path $PSScriptRoot -Parent)

Write-Host "Repositorio local listo. Enviando a GitHub..." -ForegroundColor Cyan
Write-Host "Si aparece ventana de inicio de sesion, inicie sesion con su cuenta nestorm94." -ForegroundColor Yellow
Write-Host ""

git push -u origin main

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "OK: https://github.com/nestorm94/SEC_Salud_OSD" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Si fallo, use token como contraseña:" -ForegroundColor Yellow
    Write-Host "  GitHub -> Settings -> Developer settings -> Personal access tokens -> Generate (permiso repo)"
}
