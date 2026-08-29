@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"
set "TOOLS_DIR=%REPO_ROOT%\.tools"
set "GLOBAL_JSON=%REPO_ROOT%\global.json"
set "NODE_VERSION_FILE=%REPO_ROOT%\src\web\.node-version"
set "SOLUTION=%REPO_ROOT%\MachiVerseWorks.slnx"
set "WEB_DIR=%REPO_ROOT%\src\web"

where powershell.exe >nul 2>&1
if errorlevel 1 (
    echo [ERROR] powershell.exe was not found.
    echo Windows PowerShell 5.1 or later is required.
    exit /b 1
)

if not exist "%GLOBAL_JSON%" (
    echo [ERROR] global.json was not found: "%GLOBAL_JSON%"
    exit /b 1
)

if not exist "%NODE_VERSION_FILE%" (
    echo [ERROR] .node-version was not found: "%NODE_VERSION_FILE%"
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
set "NODE_PARENT=%TOOLS_DIR%\node"
set "NODE_DIR=%NODE_PARENT%\node-v%NODE_VERSION%-win-%NODE_ARCH%"
set "DOTNET_EXE=%DOTNET_DIR%\dotnet.exe"
set "NODE_EXE=%NODE_DIR%\node.exe"
set "NPM_CMD=%NODE_DIR%\npm.cmd"

echo.
echo ============================================================
echo MachiVerseWorks Windows Development Setup
echo ============================================================
echo Repository : %REPO_ROOT%
echo .NET SDK   : %DOTNET_VERSION% (%ARCH%)
echo Node.js    : %NODE_VERSION% (%NODE_ARCH%)
echo Tool cache : %TOOLS_DIR%
echo.

if not exist "%TOOLS_DIR%" mkdir "%TOOLS_DIR%"

if exist "%DOTNET_EXE%" (
    echo [1/5] .NET SDK %DOTNET_VERSION% is already available.
) else (
    echo [1/5] Downloading .NET SDK %DOTNET_VERSION%...
    if not exist "%DOTNET_DIR%" mkdir "%DOTNET_DIR%"

    set "DOTNET_INSTALL=%TEMP%\machiverseworks-dotnet-install-!RANDOM!!RANDOM!.ps1"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
        "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile '!DOTNET_INSTALL!'"
    if errorlevel 1 (
        echo [ERROR] Failed to download dotnet-install.ps1.
        exit /b 1
    )

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "!DOTNET_INSTALL!" ^
        -Version "%DOTNET_VERSION%" ^
        -InstallDir "%DOTNET_DIR%" ^
        -Architecture "%ARCH%" ^
        -NoPath
    set "INSTALL_EXIT=!ERRORLEVEL!"
    del /q "!DOTNET_INSTALL!" >nul 2>&1

    if not "!INSTALL_EXIT!"=="0" (
        echo [ERROR] Failed to install the .NET SDK.
        exit /b !INSTALL_EXIT!
    )
)

if exist "%NODE_EXE%" (
    echo [2/5] Node.js %NODE_VERSION% is already available.
) else (
    echo [2/5] Downloading Node.js %NODE_VERSION%...
    if not exist "%NODE_PARENT%" mkdir "%NODE_PARENT%"

    if exist "%NODE_DIR%" rmdir /s /q "%NODE_DIR%"

    set "NODE_ZIP=%TEMP%\machiverseworks-node-v%NODE_VERSION%-%NODE_ARCH%-!RANDOM!!RANDOM!.zip"
    set "NODE_URL=https://nodejs.org/dist/v%NODE_VERSION%/node-v%NODE_VERSION%-win-%NODE_ARCH%.zip"

    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
        "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing '!NODE_URL!' -OutFile '!NODE_ZIP!'"
    if errorlevel 1 (
        echo [ERROR] Failed to download Node.js from:
        echo !NODE_URL!
        exit /b 1
    )

    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
        "$ErrorActionPreference='Stop'; Expand-Archive -LiteralPath '!NODE_ZIP!' -DestinationPath '%NODE_PARENT%' -Force"
    set "EXTRACT_EXIT=!ERRORLEVEL!"
    del /q "!NODE_ZIP!" >nul 2>&1

    if not "!EXTRACT_EXIT!"=="0" (
        echo [ERROR] Failed to extract the Node.js archive.
        exit /b !EXTRACT_EXIT!
    )
)

if not exist "%DOTNET_EXE%" (
    echo [ERROR] dotnet.exe was not found after setup:
    echo "%DOTNET_EXE%"
    exit /b 1
)

if not exist "%NODE_EXE%" (
    echo [ERROR] node.exe was not found after setup:
    echo "%NODE_EXE%"
    exit /b 1
)

set "DOTNET_ROOT=%DOTNET_DIR%"
set "PATH=%DOTNET_DIR%;%NODE_DIR%;%PATH%"

for /f "usebackq delims=" %%V in (`"%DOTNET_EXE%" --version`) do set "ACTUAL_DOTNET_VERSION=%%V"
for /f "usebackq delims=" %%V in (`"%NODE_EXE%" --version`) do set "ACTUAL_NODE_VERSION=%%V"

if /I not "%ACTUAL_DOTNET_VERSION%"=="%DOTNET_VERSION%" (
    echo [ERROR] Unexpected .NET SDK version.
    echo Expected: %DOTNET_VERSION%
    echo Actual  : %ACTUAL_DOTNET_VERSION%
    exit /b 1
)

if /I not "%ACTUAL_NODE_VERSION%"=="v%NODE_VERSION%" (
    echo [ERROR] Unexpected Node.js version.
    echo Expected: v%NODE_VERSION%
    echo Actual  : %ACTUAL_NODE_VERSION%
    exit /b 1
)

echo [3/5] Restoring .NET dependencies...
pushd "%REPO_ROOT%"
"%DOTNET_EXE%" restore "%SOLUTION%"
if errorlevel 1 (
    popd
    echo [ERROR] dotnet restore failed.
    exit /b 1
)

echo [4/5] Building the .NET solution in Release mode...
"%DOTNET_EXE%" build "%SOLUTION%" --configuration Release --no-restore
if errorlevel 1 (
    popd
    echo [ERROR] dotnet build failed.
    exit /b 1
)
popd

echo [5/5] Restoring and building Web Client dependencies...
pushd "%WEB_DIR%"
call "%NPM_CMD%" ci
if errorlevel 1 (
    popd
    echo [ERROR] npm ci failed.
    exit /b 1
)

call "%NPM_CMD%" run build
if errorlevel 1 (
    popd
    echo [ERROR] npm run build failed.
    exit /b 1
)
popd

echo.
echo ============================================================
echo Setup completed successfully.
echo ============================================================
echo Start MachiVerseWorks with:
echo   scripts\run-dev.bat
echo.
exit /b 0
