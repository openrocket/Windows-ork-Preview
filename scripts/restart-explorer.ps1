<#
.SYNOPSIS
    Restarts Windows Explorer to pick up shell extension changes.

.DESCRIPTION
    Kills and restarts explorer.exe so that newly registered (or unregistered)
    shell extensions take effect immediately. Also clears the icon/thumbnail cache.
#>

Set-StrictMode -Version Latest

Write-Host "Clearing thumbnail cache..." -ForegroundColor Cyan

# Clear the thumbnail cache database files
$thumbCachePath = "$env:LOCALAPPDATA\Microsoft\Windows\Explorer"
if (Test-Path $thumbCachePath) {
    # Stop Explorer first so it releases the cache files
    Write-Host "Stopping explorer.exe..." -ForegroundColor Cyan
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    Get-ChildItem "$thumbCachePath\thumbcache_*.db" -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            Remove-Item $_.FullName -Force
            Write-Host "  Deleted $($_.Name)" -ForegroundColor DarkGray
        }
        catch {
            Write-Host "  Could not delete $($_.Name) (in use)" -ForegroundColor Yellow
        }
    }
}
else {
    Write-Host "Stopping explorer.exe..." -ForegroundColor Cyan
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Restart Explorer
Write-Host "Starting explorer.exe..." -ForegroundColor Cyan
Start-Process explorer.exe

Write-Host ""
Write-Host "Done. Explorer has been restarted." -ForegroundColor Green
