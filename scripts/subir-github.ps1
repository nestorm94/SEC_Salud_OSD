# Sube el proyecto al repositorio: https://github.com/nestorm94/SEC_Salud_OSD
# Requisitos: Git instalado (https://git-scm.com/download/win) y acceso a GitHub (HTTPS o SSH).

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent

Set-Location $repoRoot
Write-Host "Directorio del proyecto: $repoRoot" -ForegroundColor Cyan

$git = Get-Command git -ErrorAction SilentlyContinue
if (-not $git) {
    Write-Error @"
Git no está instalado o no está en el PATH.
1. Instale Git: https://git-scm.com/download/win
2. Cierre y abra PowerShell
3. Vuelva a ejecutar: .\scripts\subir-github.ps1
"@
}

$remoteUrl = "https://github.com/nestorm94/SEC_Salud_OSD.git"

if (-not (Test-Path ".git")) {
    git init
    git branch -M main
}

$currentRemote = git remote get-url origin 2>$null
if ($LASTEXITCODE -ne 0) {
    git remote add origin $remoteUrl
    Write-Host "Remote 'origin' agregado: $remoteUrl" -ForegroundColor Green
} elseif ($currentRemote -ne $remoteUrl) {
    git remote set-url origin $remoteUrl
    Write-Host "Remote actualizado a: $remoteUrl" -ForegroundColor Yellow
}

git add -A
$status = git status --porcelain
if (-not $status) {
    Write-Host "No hay cambios nuevos para commitear." -ForegroundColor Yellow
} else {
    git commit -m @"
Publicación inicial Observatorio Salud Departamental Casanare.

- API ASP.NET Core (JWT, cargas Excel, SQL Server)
- Frontend Angular (dashboard, archivos, validaciones, población, administración)
- Scripts de arranque y documentación
"@
    Write-Host "Commit creado." -ForegroundColor Green
}

Write-Host ""
Write-Host "Enviando a GitHub (rama main)..." -ForegroundColor Cyan
Write-Host "Si pide credenciales, use su usuario de GitHub y un Personal Access Token como contraseña." -ForegroundColor Gray
git push -u origin main

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Listo. Repositorio: https://github.com/nestorm94/SEC_Salud_OSD" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Si el push falló por autenticación:" -ForegroundColor Yellow
    Write-Host "  1. Cree un token en GitHub -> Settings -> Developer settings -> Personal access tokens"
    Write-Host "  2. Vuelva a ejecutar este script e ingrese el token como contraseña"
    Write-Host "  O configure SSH: https://docs.github.com/en/authentication/connecting-to-github-with-ssh"
}
