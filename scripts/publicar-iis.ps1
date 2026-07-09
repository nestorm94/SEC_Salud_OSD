# Publica API + front estático al sitio IIS ObservatorioOSD (puerto 8081)
# Ejecutar PowerShell como Administrador si IIS bloquea archivos.

# Parámetros de publicación
param(
    # Carpeta física del sitio IIS de destino
    [string]$Destino = "C:\Hosting\ObservatorioOSD",
    # Ruta al proyecto .csproj de la API
    [string]$Proyecto = "$PSScriptRoot\..\backend\Observatorios.Api\Observatorios.Api.csproj"
)

# Configuración inicial y rutas del repositorio
$ErrorActionPreference = "Stop"
$repo = Resolve-Path "$PSScriptRoot\.."
$publicSrc = Join-Path $repo "public"
$frontendDir = Join-Path $repo "frontend"
$angularDist = Join-Path $frontendDir "dist\frontend\browser"
$publishDir = Join-Path $env:TEMP "ObservatorioOSD_publish"

# Compilación del frontend Angular (si existe package.json)
if (Test-Path (Join-Path $frontendDir "package.json")) {
    Write-Host "Compilando Angular (frontend)..." -ForegroundColor Cyan
    Push-Location $frontendDir
    try {
        if (-not (Test-Path "node_modules")) {
            npm install --no-audit --no-fund --legacy-peer-deps
            if ($LASTEXITCODE -ne 0) { throw "npm install falló" }
        }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build falló" }
    } finally {
        Pop-Location
    }
}

# Compilación y publicación de la API .NET
Write-Host "Compilando y publicando API..." -ForegroundColor Cyan
dotnet publish $Proyecto -c Release -o $publishDir --no-self-contained

# Creación del directorio de destino si no existe
if (-not (Test-Path $Destino)) {
    New-Item -ItemType Directory -Path $Destino -Force | Out-Null
}

# Página offline temporal para liberar archivos en IIS durante la copia
$offline = Join-Path $Destino "app_offline.htm"
@"
<!DOCTYPE html><html><body><p>Actualizando observatorio...</p></body></html>
"@ | Set-Content -Path $offline -Encoding UTF8
Start-Sleep -Seconds 2

# Copia de binarios de la API al sitio IIS
Write-Host "Copiando API a $Destino ..." -ForegroundColor Cyan
# /XD uploads: no borrar archivos subidos por los usuarios al publicar
robocopy $publishDir $Destino /MIR /XF appsettings.Development.json /XD uploads /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy API falló con código $LASTEXITCODE" }

# Asegurar carpeta uploads para archivos cargados por usuarios
$uploadsDir = Join-Path $Destino "uploads"
New-Item -ItemType Directory -Path $uploadsDir -Force | Out-Null
Write-Host "Carpeta uploads: $uploadsDir" -ForegroundColor Cyan

# Copia del frontend estático a wwwroot (Angular dist o public/ como respaldo)
$www = Join-Path $Destino "wwwroot"
if (-not (Test-Path $www)) { New-Item -ItemType Directory -Path $www -Force | Out-Null }
if (Test-Path $angularDist) {
    Write-Host "Copiando Angular dist a wwwroot ..." -ForegroundColor Cyan
    robocopy $angularDist $www /MIR /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy Angular wwwroot falló con código $LASTEXITCODE" }
} else {
    Write-Host "Copiando public/ a wwwroot (sin build Angular) ..." -ForegroundColor Yellow
    robocopy $publicSrc $www /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy wwwroot falló con código $LASTEXITCODE" }
}

# Quitar página offline y reciclar el sitio IIS
Remove-Item $offline -Force -ErrorAction SilentlyContinue

& "$PSScriptRoot\reciclar-sitio-iis.ps1"
Write-Host "`nListo. Pruebe: http://localhost:8081/ (Angular) o http://localhost:8081/api/ping" -ForegroundColor Green
