#!/usr/bin/env pwsh
# Launches the WebClient dev server (in watch mode) bound to all interfaces and prints the
# LAN URL other devices on the network (e.g. a phone) can use.
#
# Usage:
#   .\run.ps1             run with --watch
#   .\run.ps1 <args...>   any extra args are forwarded to `dotnet run`

$ErrorActionPreference = "Stop"
$Port = 5290

# Find the IPv4 address of the up adapter that has a default gateway (the LAN-facing one).
$lanIp = $null
try {
    $lanIp = (Get-NetIPConfiguration |
        Where-Object { $null -ne $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } |
        Select-Object -First 1).IPv4Address.IPAddress
} catch { }

Write-Host ""
Write-Host "  WebClient dev server"
Write-Host "    Local:   http://localhost:$Port"
if ($lanIp) {
    Write-Host "    Network: http://${lanIp}:$Port"
} else {
    Write-Host "    Network: (no LAN adapter with a default gateway found)"
}
Write-Host ""

$projectPath = Join-Path $PSScriptRoot "WebClient.csproj"
dotnet run --project $projectPath -p:MetaplayWebAssembly=true --watch @args
