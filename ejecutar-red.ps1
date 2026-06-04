# Inicia API + UI (Angular dist) escuchando en todas las interfaces (LAN / IP pública con reenvío de puerto).
# Uso: .\ejecutar-red.ps1
#      .\ejecutar-red.ps1 -Puerto 5289 -SinBuild
param(
    [int]$Puerto = 5289,
    [switch]$SinBuild
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$frontend = Join-Path $root "frontend"
$proj = Join-Path $root "backend\Observatorios.Api\Observatorios.Api.csproj"

if (-not $SinBuild -and (Test-Path (Join-Path $frontend "package.json"))) {
    Write-Host "Compilando Angular..." -ForegroundColor Cyan
    Push-Location $frontend
    try {
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build falló" }
    } finally {
        Pop-Location
    }
}

function Get-Ipv4Local {
    Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -notlike "127.*" -and $_.PrefixOrigin -ne "WellKnown" } |
        Select-Object -ExpandProperty IPAddress -Unique
}

$localIps = @(Get-Ipv4Local)
if ($localIps.Count -eq 0) {
    $localIps = @(
        (Get-NetIPConfiguration -ErrorAction SilentlyContinue |
            Where-Object { $_.IPv4DefaultGateway -and $_.IPv4Address } |
            ForEach-Object { $_.IPv4Address.IPAddress }) | Select-Object -Unique
    )
}

$publicIp = $null
try {
    $publicIp = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 5).Trim()
} catch {
    Write-Host "No se pudo obtener IP pública automática (sin internet o bloqueado)." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Observatorio — acceso por red ===" -ForegroundColor Green
Write-Host "Puerto: $Puerto (todas las interfaces: 0.0.0.0)" -ForegroundColor Cyan
Write-Host ""
Write-Host "En este equipo:" -ForegroundColor White
Write-Host "  http://localhost:$Puerto/" -ForegroundColor Gray
foreach ($ip in $localIps) {
    Write-Host "  http://${ip}:$Puerto/" -ForegroundColor Yellow
}
if ($publicIp) {
    Write-Host ""
    Write-Host "IP pública (Internet, si el router reenvía el puerto $Puerto a este PC):" -ForegroundColor White
    Write-Host "  http://${publicIp}:$Puerto/" -ForegroundColor Magenta
    Write-Host "  API: http://${publicIp}:$Puerto/api/ping" -ForegroundColor Magenta
}
Write-Host ""
Write-Host "Firewall (PowerShell como Administrador, una vez):" -ForegroundColor DarkGray
Write-Host "  New-NetFirewallRule -DisplayName 'Observatorio OSD $Puerto' -Direction Inbound -Protocol TCP -LocalPort $Puerto -Action Allow" -ForegroundColor DarkGray
Write-Host ""
Write-Host "IIS (si ya publicó en 8081): http://localhost:8081/ o http://<su-IP>:8081/" -ForegroundColor DarkGray
Write-Host ""

if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
    sqllocaldb start MSSQLLocalDB 2>$null | Out-Null
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://0.0.0.0:$Puerto"
Write-Host "Iniciando API..." -ForegroundColor Cyan
dotnet run --project $proj --no-launch-profile
