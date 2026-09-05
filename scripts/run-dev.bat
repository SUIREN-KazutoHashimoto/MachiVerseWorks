@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"
set "TOOLS_DIR=%REPO_ROOT%\.tools"
set "GLOBAL_JSON=%REPO_ROOT%\global.json"
set "NODE_VERSION_FILE=%REPO_ROOT%\src\view\.node-version"
set "WEB_DIR=%REPO_ROOT%\src\view"
set "SERVER_PROJECT=%REPO_ROOT%\src\server\MachiVerseWorks.Server.csproj"

where powershell.exe >nul 2>&1
if errorlevel 1 (
    echo [ERROR] powershell.exe was not found.
    exit /b 1
)

if not exist "%GLOBAL_JSON%" (
    echo [ERROR] global.json was not found.
    exit /b 1
)

if not exist "%NODE_VERSION_FILE%" (
    echo [ERROR] .node-version was not found.
    exit /b 1
)

for /f "usebackq delims=" %%V in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; (Get-Content -Raw '%GLOBAL_JSON%' | ConvertFrom-Json).sdk.version"`) do set "DOTNET_VERSION=%%V"
set /p "NODE_VERSION="<"%NODE_VERSION_FILE%"

if not defined DOTNET_VERSION (
    echo [ERROR] Failed to read the .NET SDK version from global.json.
    exit /b 1
)

if not defined NODE_VERSION (
    echo [ERROR] Failed to read the Node.js version from .node-version.
    exit /b 1
)

set "ARCH=x64"
set "NODE_ARCH=x64"
if /I "%PROCESSOR_ARCHITECTURE%"=="ARM64" (
    set "ARCH=arm64"
    set "NODE_ARCH=arm64"
)
if /I "%PROCESSOR_ARCHITEW6432%"=="ARM64" (
    set "ARCH=arm64"
    set "NODE_ARCH=arm64"
)

set "DOTNET_DIR=%TOOLS_DIR%\dotnet\%DOTNET_VERSION%-%ARCH%"
set "NODE_DIR=%TOOLS_DIR%\node\node-v%NODE_VERSION%-win-%NODE_ARCH%"
set "DOTNET_EXE=%DOTNET_DIR%\dotnet.exe"
set "NPM_CMD=%NODE_DIR%\npm.cmd"

if not exist "%DOTNET_EXE%" (
    echo [ERROR] Repository-local .NET SDK was not found.
    echo Run scripts\setup-dev.bat first.
    exit /b 1
)

if not exist "%NPM_CMD%" (
    echo [ERROR] Repository-local Node.js was not found.
    echo Run scripts\setup-dev.bat first.
    exit /b 1
)

set "DOTNET_ROOT=%DOTNET_DIR%"
set "PATH=%DOTNET_DIR%;%NODE_DIR%;%PATH%"

if /I "%~1"=="--server" goto server
if /I "%~1"=="--web" goto web

echo.
echo ============================================================
echo MachiVerseWorks Development Launcher
echo ============================================================
echo Server : http://127.0.0.1:5080
echo Web    : http://127.0.0.1:5173
echo.

cd /d "%REPO_ROOT%"

echo [1/3] Starting Server...
start "MachiVerseWorks Server" cmd.exe /d /k scripts\run-dev.bat --server

call :wait_for_url "http://127.0.0.1:5080/health" 30
if errorlevel 1 (
    echo [ERROR] Server health check did not succeed within 30 seconds.
    echo Check the "MachiVerseWorks Server" window for details.
    exit /b 1
)

echo [2/3] Starting Web Client...
start "MachiVerseWorks Web" cmd.exe /d /k scripts\run-dev.bat --web

call :wait_for_url "http://127.0.0.1:5173" 30
if errorlevel 1 (
    echo [ERROR] Web Client did not respond within 30 seconds.
    echo Check the "MachiVerseWorks Web" window for details.
    exit /b 1
)

echo [3/3] Opening browser...
start "" "http://127.0.0.1:5173"

echo.
echo MachiVerseWorks is running.
echo Press Ctrl+C in each Server/Web window to stop it.
exit /b 0

:server
title MachiVerseWorks Server
cd /d "%REPO_ROOT%"
echo Starting MachiVerseWorks.Server...
"%DOTNET_EXE%" run --project "%SERVER_PROJECT%"
exit /b %ERRORLEVEL%

:web
title MachiVerseWorks Web
cd /d "%WEB_DIR%"
echo Starting MachiVerseWorks Web Client...
call "%NPM_CMD%" run dev -- --host 127.0.0.1 --port 5173 --strictPort
exit /b %ERRORLEVEL%

:wait_for_url
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ErrorActionPreference='SilentlyContinue'; $url='%~1'; $deadline=[DateTime]::UtcNow.AddSeconds(%~2); do { try { $response=Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 2; if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) { exit 0 } } catch {}; Start-Sleep -Milliseconds 500 } while ([DateTime]::UtcNow -lt $deadline); exit 1"
exit /b %ERRORLEVEL%
