<#
.SYNOPSIS
    Unregisters the OrkThumbnailHandler shell extension.

.DESCRIPTION
    Unregisters the .ork thumbnail handler COM server. Must be run as Administrator.

.PARAMETER DllPath
    Full path to OrkThumbnailHandler.dll.

.EXAMPLE
    .\unregister.ps1 -DllPath "C:\Program Files\OrkThumbnailHandler\OrkThumbnailHandler.dll"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$DllPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# --- Check elevation ---
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)
if (-not $isAdmin) {
    Write-Error "This script must be run as Administrator."
    exit 1
}

# --- Validate DLL exists ---
if (-not (Test-Path $DllPath)) {
    Write-Error "DLL not found at: $DllPath"
    exit 1
}

$DllPath = (Resolve-Path $DllPath).Path

# --- Find 64-bit regasm ---
$regasmPath = Join-Path $env:windir "Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
if (-not (Test-Path $regasmPath)) {
    Write-Error ".NET Framework 4.x 64-bit regasm not found at: $regasmPath"
    exit 1
}

# --- Unregister ---
Write-Host "Unregistering $DllPath ..." -ForegroundColor Cyan
& $regasmPath /unregister $DllPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "regasm /unregister failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Unregistration successful." -ForegroundColor Green
Write-Host "Run .\restart-explorer.ps1 or log off/on to clear cached thumbnails."
