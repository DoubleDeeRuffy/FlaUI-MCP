@echo off
setlocal

echo === FlaUI-MCP Phase 3 Verify ===

REM --- Build ---
dotnet build src\FlaUI.Mcp\FlaUI.Mcp.csproj -c Release
if errorlevel 1 (
    echo FAIL: dotnet build
    exit /b 1
)

REM --- Smoke check 1: WinExe OutputType (TSK-01) — must match ---
findstr /C:"<OutputType>WinExe</OutputType>" src\FlaUI.Mcp\FlaUI.Mcp.csproj >nul
if errorlevel 1 (
    echo FAIL: OutputType WinExe not found in FlaUI.Mcp.csproj
    exit /b 1
)

REM --- Smoke check 2: WindowsServices package gone (TSK-07) — must NOT match ---
findstr /C:"Microsoft.Extensions.Hosting.WindowsServices" src\FlaUI.Mcp\FlaUI.Mcp.csproj >nul
if not errorlevel 1 (
    echo FAIL: Microsoft.Extensions.Hosting.WindowsServices still referenced in FlaUI.Mcp.csproj
    exit /b 1
)

REM --- Smoke check 3: WinTaskSchedulerManager used (TSK-02) — must match ---
findstr /C:"WinTaskSchedulerManager" src\FlaUI.Mcp\Program.cs >nul
if errorlevel 1 (
    echo FAIL: WinTaskSchedulerManager not found in Program.cs
    exit /b 1
)

REM --- Smoke check 4: AttachConsole present (TSK-04) — must match ---
findstr /C:"AttachConsole" src\FlaUI.Mcp\Program.cs >nul
if errorlevel 1 (
    echo FAIL: AttachConsole not found in Program.cs
    exit /b 1
)

REM --- Smoke check 5: raw schtasks shell-out removed (TSK-02) — must NOT match ---
findstr /C:"schtasks" src\FlaUI.Mcp\Program.cs >nul
if not errorlevel 1 (
    echo FAIL: raw schtasks shell-out still present in Program.cs
    exit /b 1
)

echo === ALL GREEN ===
exit /b 0
