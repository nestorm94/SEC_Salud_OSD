# Publica API + front estático al sitio IIS ObservatorioOSD (puerto 8081)
# Ejecutar PowerShell como Administrador si IIS bloquea archivos.
param(
    [string]$Destino = "C:\Hosting\ObservatorioOSD",
    [string]$Proyecto = "$PSScriptRoot\..\backend\Observatorios.Api\Observatorios.Api.csproj"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path "$PSScriptRoot\.."
$publicSrc = Join-Path $repo "public"
$publishDir = Join-Path $env:TEMP "ObservatorioOSD_publish"

Write-Host "Compilando y publicando API..." -ForegroundColor Cyan
dotnet publish $Proyecto -c Release -o $publishDir --no-self-contained

if (-not (Test-Path $Destino)) {
    New-Item -ItemType Directory -Path $Destino -Force | Out-Null
}

$offline = Join-Path $Destino "app_offline.htm"
@"
<!DOCTYPE html><html><body><p>Actualizando observatorio...</p></body></html>
"@ | Set-Content -Path $offline -Encoding UTF8
Start-Sleep -Seconds 2

Write-Host "Copiando API a $Destino ..." -ForegroundColor Cyan
# /XD uploads: no borrar archivos subidos por los usuarios al publicar
robocopy $publishDir $Destino /MIR /XF appsettings.Development.json /XD uploads /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy API falló con código $LASTEXITCODE" }

$uploadsDir = Join-Path $Destino "uploads"
New-Item -ItemType Directory -Path $uploadsDir -Force | Out-Null
Write-Host "Carpeta uploads: $uploadsDir" -ForegroundColor Cyan

$www = Join-Path $Destino "wwwroot"
if (-not (Test-Path $www)) { New-Item -ItemType Directory -Path $www -Force | Out-Null }
Write-Host "Copiando public/ a wwwroot ..." -ForegroundColor Cyan
robocopy $publicSrc $www /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy wwwroot falló con código $LASTEXITCODE" }

Remove-Item $offline -Force -ErrorAction SilentlyContinue

& "$PSScriptRoot\reciclar-sitio-iis.ps1"
Write-Host "`nListo. Pruebe: http://localhost:8081/index.html" -ForegroundColor Green
