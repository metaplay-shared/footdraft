@echo off
setlocal enabledelayedexpansion

rem Launches the WebClient dev server (in watch mode) bound to all interfaces and prints the
rem LAN URL other devices on the network can use.
rem
rem Usage:
rem   run.bat             run with --watch
rem   run.bat <args...>   any extra args are forwarded to `dotnet run`

set "PORT=5290"

rem Find the IPv4 address of the up adapter that has a default gateway (the LAN-facing one).
set "LAN_IP="
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "(Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway -ne $null -and $_.NetAdapter.Status -eq 'Up' } | Select-Object -First 1).IPv4Address.IPAddress"`) do set "LAN_IP=%%i"

echo.
echo   WebClient dev server
echo     Local:   http://localhost:%PORT%
if defined LAN_IP (
    echo     Network: http://!LAN_IP!:%PORT%
) else (
    echo     Network: ^(no LAN adapter with a default gateway found^)
)
echo.

dotnet run --project "%~dp0WebClient.csproj" -p:MetaplayWebAssembly=true --watch %*
