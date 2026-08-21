@echo off
setlocal

echo ===================================================
echo Building Affinity MCP Bridge
echo ===================================================

REM Check for CSC in PATH or Windows .NET Framework directory
where csc >nul 2>nul
if %errorlevel% equ 0 (
    echo [INFO] Found csc in PATH. Compiling...
    csc /nologo /r:System.Net.Http.dll /target:exe /out:affinity-mcp-bridge.exe AffinityMcpBridge.cs
    goto finish
)

set CSC_FRAMEWORK=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if exist "%CSC_FRAMEWORK%" (
    echo [INFO] Found .NET Framework CSC at %CSC_FRAMEWORK%. Compiling...
    "%CSC_FRAMEWORK%" /nologo /r:System.Net.Http.dll /target:exe /out:affinity-mcp-bridge.exe AffinityMcpBridge.cs
    goto finish
)

set CSC_FRAMEWORK32=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
if exist "%CSC_FRAMEWORK32%" (
    echo [INFO] Found .NET Framework CSC at %CSC_FRAMEWORK32%. Compiling...
    "%CSC_FRAMEWORK32%" /nologo /r:System.Net.Http.dll /target:exe /out:affinity-mcp-bridge.exe AffinityMcpBridge.cs
    goto finish
)

REM Check for dotnet CLI
where dotnet >nul 2>nul
if %errorlevel% equ 0 (
    echo [INFO] Compiling using dotnet...
    dotnet new console -n AffinityBridgeTemp --force >nul
    copy /Y AffinityMcpBridge.cs AffinityBridgeTemp\Program.cs >nul
    cd AffinityBridgeTemp
    dotnet publish -c Release -r win-x64 --self-contained false -o ..\ >nul
    cd ..
    rmdir /S /Q AffinityBridgeTemp
    if exist AffinityBridgeTemp.exe ren AffinityBridgeTemp.exe affinity-mcp-bridge.exe
    goto finish
)

echo [ERROR] No C# compiler (csc or dotnet) found on your system.
echo Please install .NET SDK or ensure .NET Framework is enabled.
exit /b 1

:finish
if exist "affinity-mcp-bridge.exe" (
    echo.
    echo [SUCCESS] Built affinity-mcp-bridge.exe successfully!
) else (
    echo [ERROR] Build failed.
    exit /b 1
)

endlocal
