#requires -RunAsAdministrator
<#
.SYNOPSIS
    Removes the Nitrate-Api and Nitrate-Worker Scheduled Tasks created by
    install-autostart.ps1. Nitrate itself, its data, and PostgreSQL are untouched.

.DESCRIPTION
    Does not change the PostgreSQL service's startup type, since the owner may use that
    PostgreSQL instance for other things. Set it back manually if needed:
        Set-Service -Name postgresql-x64-16 -StartupType Manual

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File uninstall-autostart.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

foreach ($name in @("Nitrate-Api", "Nitrate-Worker")) {
    if (Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $name -Confirm:$false
        Write-Host "Removed scheduled task '$name'."
    } else {
        Write-Host "Task '$name' was not registered; nothing to remove."
    }
}

Write-Host ""
Write-Host "Autostart removed. Nitrate no longer starts at boot; use start-nitrate.ps1 to run it manually."
