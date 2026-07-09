# Ejecutar PowerShell COMO ADMINISTRADOR
# Recicla el sitio ObservatorioOSD en IIS (puerto 8081)

# Nombres del Application Pool y sitio web en IIS
$poolName = "ObservatorioOSDPool"
$siteName = "ObservatorioOSD"

# Carga del módulo de administración web de IIS
Import-Module WebAdministration -ErrorAction Stop

# Reinicio del Application Pool para aplicar cambios publicados
Write-Host "Reciclando App Pool: $poolName" -ForegroundColor Cyan
Restart-WebAppPool -Name $poolName

# Verificación del estado del sitio tras el reciclaje
Write-Host "Sitio: $siteName" -ForegroundColor Cyan
Get-Website -Name $siteName | Format-List Name, State, PhysicalPath

# URLs de prueba tras el reciclaje
Write-Host "`nPruebe: http://localhost:8081/api/ping" -ForegroundColor Green
Write-Host "Login:  http://localhost:8081/login.html" -ForegroundColor Green
