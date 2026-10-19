@echo off
setlocal

if "%~1"=="" (
    echo Uso: Start-BitacoraManual.cmd ^<SERVER_IP^>
    echo Ejemplo: Start-BitacoraManual.cmd ^<SERVER_IP^>
    exit /b 1
)

set "SERVER_IP=%~1"
set "HTTPS_PORT=5067"
set "DEPLOY_ROOT=C:\BitacoraEvidencias\deploy"
set "APP_DIR=%DEPLOY_ROOT%\current"
set "APP_EXE=%APP_DIR%\BitacoraEvidencias.Web.exe"
set "APP_URL=https://%SERVER_IP%:%HTTPS_PORT%/Login"
set "LOG_DIR=%DEPLOY_ROOT%\logs"

if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo [%DATE% %TIME%] Inicio manual solicitado para %APP_URL% >> "%LOG_DIR%\manual-start.log"

if not exist "%APP_EXE%" (
    echo No se encontro la aplicacion publicada: %APP_EXE%
    echo Ejecuta primero scripts\deploy\Publish-Bitacora.ps1.
    pause
    exit /b 1
)

netstat -ano | findstr /R /C:":%HTTPS_PORT% .*LISTENING" >nul
if %ERRORLEVEL% EQU 0 goto abrir

rem Conservar otras instancias/versiones; no matar procesos por nombre.

set "ASPNETCORE_URLS=https://%SERVER_IP%:%HTTPS_PORT%"
set "ASPNETCORE_ENVIRONMENT=Production"
set "ASPNETCORE_Kestrel__Certificates__Default__Path=%BITACORA_HTTPS_CERT_PATH%"
set "ASPNETCORE_Kestrel__Certificates__Default__Password=%BITACORA_HTTPS_CERT_PASSWORD%"
set "ConnectionStrings__DefaultConnection=Data Source=C:\BitacoraEvidencias\Data\bitacora-evidencias.db"
set "Storage__RootPath=C:\BitacoraEvidencias\Evidencias"
set "BITACORA_DATA_PROTECTION_KEY=%BITACORA_DATA_PROTECTION_KEY%"
set "Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning"

cd /d "%APP_DIR%"
start "Bitacora Evidencias Servidor" /MIN cmd /c ""%APP_EXE%" >> "%LOG_DIR%\manual-app.out.log" 2>> "%LOG_DIR%\manual-app.err.log""

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "for ($i=0; $i -lt 90; $i++) { $listening = netstat -ano | Select-String ':%HTTPS_PORT%.*LISTENING'; if ($listening) { Start-Process $env:APP_URL; exit 0 }; Start-Sleep -Seconds 1 }; Write-Host 'La app no levanto el puerto %HTTPS_PORT% despues de 90 segundos. Revisa %LOG_DIR%\manual-app.err.log'; pause"
goto fin

:abrir
start "" "%APP_URL%"

:fin
echo [%DATE% %TIME%] Launcher manual termino >> "%LOG_DIR%\manual-start.log"
exit /b 0
