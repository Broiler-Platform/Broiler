@echo off
rem ---------------------------------------------------------------------------------------------
rem  Start-BOSS.cmd - run BOSS (the Broiler Office Standalone Server) in the foreground.
rem
rem    Start-BOSS.cmd                                 listens on http://0.0.0.0:5555
rem    Start-BOSS.cmd --urls http://127.0.0.1:8080    any argument the server accepts
rem
rem  Convenience only - to serve the Writer across reboots, install it as a Windows service:
rem    powershell -ExecutionPolicy Bypass -File service\Install-BossService.ps1
rem ---------------------------------------------------------------------------------------------
setlocal

set "BOSS_DIR=%~dp0"
set "BOSS_EXEC=%BOSS_DIR%Broiler.Office.Server.exe"
if not defined BOSS_URLS set "BOSS_URLS=http://0.0.0.0:5555"
if not defined ASPNETCORE_ENVIRONMENT set "ASPNETCORE_ENVIRONMENT=Production"
set "DOTNET_NOLOGO=1"

if not exist "%BOSS_EXEC%" (
    echo Start-BOSS.cmd: %BOSS_EXEC% not found - run this from the extracted package folder. 1>&2
    exit /b 1
)
if not exist "%BOSS_DIR%wwwroot" (
    echo Start-BOSS.cmd: %BOSS_DIR%wwwroot is missing - the server has no Writer bundle to serve. 1>&2
    exit /b 1
)

rem Caller arguments come last so they win (--urls is last-wins in the configuration provider).
if not "%~1"=="" (
    "%BOSS_EXEC%" --urls "%BOSS_URLS%" %*
    exit /b %ERRORLEVEL%
)

echo BOSS - Broiler Office Standalone Server
echo   Writer:  %BOSS_URLS%/
echo   Health:  %BOSS_URLS%/healthz
echo   Stop:    Ctrl+C
echo.
"%BOSS_EXEC%" --urls "%BOSS_URLS%"
exit /b %ERRORLEVEL%
