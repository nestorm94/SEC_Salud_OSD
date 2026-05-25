# Ejecutar PowerShell COMO ADMINISTRADOR
# Recicla el sitio ObservatorioOSD en IIS (puerto 8081)

$poolName = "ObservatorioOSDPool"
$siteName = "ObservatorioOSD"

Import-Module WebAdministration -ErrorAction Stop

Write-Host "Reciclando App Pool: $poolName" -ForegroundColor Cyan
Restart-WebAppPool -Name $poolName

Write-Host "Sitio: $siteName" -ForegroundColor Cyan
Get-Website -Name $siteName | Format-List Name, State, PhysicalPath

Write-Host "`nPruebe: http://localhost:8081/api/ping" -ForegroundColor Green
Write-Host "Login:  http://localhost:8081/login.html" -ForegroundColor Green
