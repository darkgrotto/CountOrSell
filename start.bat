@echo off
:: CountOrSell startup script (Windows native)
:: Starts the ASP.NET Core API (port 5000) and the Vite dev server (port 5173)
:: Each service opens in its own console window.

setlocal enabledelayedexpansion

:: ── Paths ────────────────────────────────────────────────────────────────────

set "SCRIPT_DIR=%~dp0"
:: Remove trailing backslash
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"

set "API_DIR=%SCRIPT_DIR%\src\MtgHelper.Api"
set "WEB_DIR=%SCRIPT_DIR%\src\mtghelper-web"
set "SLN_FILE=%SCRIPT_DIR%\src\MtgHelper.sln"
set "API_PORT=5000"

:: ── Preflight checks ─────────────────────────────────────────────────────────

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] 'dotnet' not found. Please install the .NET SDK.
    pause & exit /b 1
)

where npm >nul 2>&1
if errorlevel 1 (
    echo [ERROR] 'npm' not found. Please install Node.js.
    pause & exit /b 1
)

echo [%time:~0,8%] CountOrSell -- starting services
echo ----------------------------------------

:: ── Port cleanup ─────────────────────────────────────────────────────────────

echo [%time:~0,8%] Checking port %API_PORT%...
powershell -NoProfile -Command ^
    "Get-NetTCPConnection -LocalPort %API_PORT% -State Listen -EA SilentlyContinue | Select-Object -First 1 | ForEach-Object { Write-Host ('[' + (Get-Date -Format HH:mm:ss) + '] Port %API_PORT% in use by PID ' + $_.OwningProcess + ' -- killing it...'); Stop-Process -Id $_.OwningProcess -Force -EA SilentlyContinue; Start-Sleep 1 }"

:: ── Vite cache ───────────────────────────────────────────────────────────────
:: Clear cache to avoid EPERM errors on OneDrive-backed paths

if exist "%WEB_DIR%\node_modules\.vite" (
    echo [%time:~0,8%] Clearing Vite cache...
    rmdir /s /q "%WEB_DIR%\node_modules\.vite" 2>nul
)

:: ── Restore .NET packages ────────────────────────────────────────────────────

echo [%time:~0,8%] Restoring .NET packages...
dotnet restore "%SLN_FILE%" --nologo -v q
if errorlevel 1 (
    echo [ERROR] dotnet restore failed.
    pause & exit /b 1
)

:: ── Start API in its own window ───────────────────────────────────────────────
:: Write a temp launcher to avoid nested-quote issues with start + cmd /k

echo [%time:~0,8%] Starting API on http://localhost:%API_PORT% ...

> "%TEMP%\cos_api.bat" (
    echo @echo off
    echo title CountOrSell API
    echo set ASPNETCORE_ENVIRONMENT=Development
    echo dotnet run --project "%API_DIR%" --no-launch-profile --urls http://localhost:%API_PORT%
    echo pause
)
start "CountOrSell API" cmd /k "%TEMP%\cos_api.bat"

:: ── Wait for API to accept TCP connections (up to 30 s) ──────────────────────

echo [%time:~0,8%] Waiting for API to be ready...
set /a WAIT=0

:check_api
powershell -NoProfile -Command ^
    "try{$t=New-Object Net.Sockets.TcpClient;$t.Connect('localhost',%API_PORT%);$t.Close();exit 0}catch{exit 1}" >nul 2>&1
if not errorlevel 1 (
    echo [%time:~0,8%] API is ready.
    goto api_ready
)
set /a WAIT+=1
if %WAIT% GEQ 30 (
    echo [%time:~0,8%] API did not respond within 30 s -- continuing anyway.
    goto api_ready
)
timeout /t 1 /nobreak >nul
goto check_api
:api_ready

:: ── Install frontend dependencies ────────────────────────────────────────────

echo [%time:~0,8%] Installing frontend dependencies...
pushd "%WEB_DIR%"
npm install --silent
if errorlevel 1 (
    popd
    echo [ERROR] npm install failed.
    pause & exit /b 1
)
popd

:: ── Start frontend in its own window ─────────────────────────────────────────

echo [%time:~0,8%] Starting frontend dev server...

> "%TEMP%\cos_web.bat" (
    echo @echo off
    echo title CountOrSell Frontend
    echo cd /d "%WEB_DIR%"
    echo npm run dev
    echo pause
)
start "CountOrSell Frontend" cmd /k "%TEMP%\cos_web.bat"

:: ── Summary ──────────────────────────────────────────────────────────────────

echo.
echo ================================================
echo   API            http://localhost:%API_PORT%
echo   API ^(Swagger^)  http://localhost:%API_PORT%/swagger
echo   Frontend       http://localhost:5173
echo ================================================
echo   Close the API and Frontend windows to stop services.
echo.
pause

endlocal
