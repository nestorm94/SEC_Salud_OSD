@echo off
cd /d "%~dp0"
echo Si el puerto 5289 esta ocupado, cierre el otro programa o use: dotnet run --project backend\Observatorios.Api\Observatorios.Api.csproj --launch-profile http5290
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ejecutar-api.ps1"
pause
