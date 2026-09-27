#requires -RunAsAdministrator
<#
.SYNOPSIS
    Registers Nitrate's API and Worker as Windows Scheduled Tasks that start at boot,
    without anyone logging in, and restart automatically if they crash.

.DESCRIPTION
    Two Scheduled Tasks are created:
      - Nitrate-Api    runs C:\Nitrate\api\Prismedia.Api.dll
      - Nitrate-Worker runs C:\Nitrate\worker\Prismedia.Worker.dll
    Both:
      - trigger AtStartup (no user needs to sign in)
      - run as SYSTEM by default (a dedicated service account also works, see -RunAsUser)
      - restart up to 3 times, 1 minute apart, if the process exits
      - run with the working directory set to their own publish folder
      - load nitrate-env.ps1 first so ConnectionStrings, PRISMEDIA_* vars, etc. are set

    Idempotent: re-running this script unregisters and re-registers both tasks, so it is safe
    to run again after changing paths or after `git pull` + republish.

    This also sets the PostgreSQL 16 service to Automatic startup, since both processes need
    the database up before they finish starting (the API waits for it; the worker waits for
    the API's migrations).

.PARAMETER InstallRoot
    Root folder from the Windows install guide (docs/nitrate/WINDOWS_HOST.md). Must contain
    api\Prismedia.Api.dll, worker\Prismedia.Worker.dll, and nitrate-env.ps1.

.PARAMETER RunAsUser
    Account the tasks run as. Default "SYSTEM" needs no password and always has permission
    to start at boot. Pass a domain\user or .\user to run as a dedicated non-admin account
    instead; Register-ScheduledTask will prompt for that account's password.

.PARAMETER PostgresServiceName
    Windows service name for PostgreSQL 16. Default matches the installer's default name.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File install-autostart.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File install-autostart.ps1 -InstallRoot D:\Nitrate
#>
[CmdletBinding()]
param(
    [string]$InstallRoot = "C:\Nitrate",
    [string]$RunAsUser = "SYSTEM",
    [string]$PostgresServiceName = "postgresql-x64-16"
)

$ErrorActionPreference = "Stop"

$apiDll = Join-Path $InstallRoot "api\Prismedia.Api.dll"
$workerDll = Join-Path $InstallRoot "worker\Prismedia.Worker.dll"
$envScript = Join-Path $InstallRoot "nitrate-env.ps1"

foreach ($required in @($apiDll, $workerDll, $envScript)) {
    if (-not (Test-Path $required)) {
        throw "Expected file not found: $required. Follow docs/nitrate/WINDOWS_HOST.md sections 4-5 first (build/publish + nitrate-env.ps1), or pass -InstallRoot."
    }
}

# Each task loads nitrate-env.ps1 in-process (dot-sourced) before starting dotnet, so every
# PRISMEDIA_* / ConnectionStrings__* variable from section 5 of WINDOWS_HOST.md is present.
# -WindowStyle Hidden keeps no console window lingering at boot, matching a background service.
function New-NitrateAction {
    param([string]$Dll, [string]$WorkingDirectory)

    $inner = ". `"$envScript`"; & dotnet `"$Dll`""
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($inner))
    return New-ScheduledTaskAction `
        -Execute "powershell.exe" `
        -Argument "-NoProfile -WindowStyle Hidden -EncodedCommand $encoded" `
        -WorkingDirectory $WorkingDirectory
}

function Install-NitrateTask {
    param([string]$Name, [string]$Dll, [string]$WorkingDirectory)

    if (Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue) {
        Write-Host "Removing existing task '$Name'..."
        Unregister-ScheduledTask -TaskName $Name -Confirm:$false
    }

    $action = New-NitrateAction -Dll $Dll -WorkingDirectory $WorkingDirectory
    $trigger = New-ScheduledTaskTrigger -AtStartup
    # A short random delay avoids both tasks (and Postgres) racing to bind ports/disk at the
    # exact same instant on a slow boot; Prismedia.Api itself retries DB connections on startup.
    $trigger.Delay = "PT10S"

    $principal = New-ScheduledTaskPrincipal -UserId $RunAsUser -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -RestartCount 3 `
        -RestartInterval (New-TimeSpan -Minutes 1) `
        -ExecutionTimeLimit (New-TimeSpan -Days 0) # no time limit: this is a long-running server, not a scheduled job

    Register-ScheduledTask -TaskName $Name -Action $action -Trigger $trigger -Principal $principal -Settings $settings | Out-Null
    Write-Host "Registered task '$Name' (runs at boot as $RunAsUser, restarts up to 3x on failure)."
}

Install-NitrateTask -Name "Nitrate-Api" -Dll $apiDll -WorkingDirectory (Split-Path $apiDll)
Install-NitrateTask -Name "Nitrate-Worker" -Dll $workerDll -WorkingDirectory (Split-Path $workerDll)

$pgService = Get-Service -Name $PostgresServiceName -ErrorAction SilentlyContinue
if ($pgService) {
    if ($pgService.StartType -ne "Automatic") {
        Set-Service -Name $PostgresServiceName -StartupType Automatic
        Write-Host "Set '$PostgresServiceName' service to Automatic startup."
    } else {
        Write-Host "'$PostgresServiceName' service already Automatic."
    }
} else {
    Write-Warning "Service '$PostgresServiceName' not found. If your PostgreSQL service has a different name, set it to Automatic manually: Get-Service *postgres* | Set-Service -StartupType Automatic"
}

Write-Host ""
Write-Host "Done. To start Nitrate right now without rebooting:"
Write-Host "  Start-ScheduledTask -TaskName Nitrate-Api"
Write-Host "  Start-Sleep -Seconds 5"
Write-Host "  Start-ScheduledTask -TaskName Nitrate-Worker"
Write-Host ""
Write-Host "To check status and recent runs:"
Write-Host "  Get-ScheduledTaskInfo -TaskName Nitrate-Api"
Write-Host "  Get-ScheduledTaskInfo -TaskName Nitrate-Worker"
Write-Host "  (or Task Scheduler GUI -> Task Scheduler Library -> Nitrate-Api / Nitrate-Worker -> History tab)"
Write-Host ""
Write-Host "The console is hidden when launched at boot, so check status instead of a log file:"
Write-Host "  Invoke-RestMethod http://localhost:8008/api/health   # API is up"
Write-Host "  Task Manager -> Details tab -> look for dotnet.exe processes (2 expected: API + Worker)"
Write-Host "  Event Viewer -> Windows Logs -> Application -> filter source '.NET Runtime' for a crash"
