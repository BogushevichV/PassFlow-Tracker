# uninstall.ps1 - PassFlow Tracker uninstaller
# Run as Administrator

$ErrorActionPreference = "Stop"

$APP_NAME    = "PassFlow Tracker"
$INSTALL_DIR = "C:\Program Files\PassFlow Tracker"

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-OK($msg)   { Write-Host "  [OK] $msg"   -ForegroundColor Green }

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Run as Administrator" -ForegroundColor Red
    exit 1
}

$confirm = Read-Host "Uninstall PassFlow Tracker? (y/n)"
if ($confirm -ne "y") { exit 0 }

# --- 1. Stop Docker container ---
Write-Step "Stopping database"
try {
    $running = docker ps --filter "name=my_postgres" --format "{{.Names}}" 2>$null
    if ($running -eq "my_postgres") {
        docker stop my_postgres | Out-Null
        Write-OK "Container my_postgres stopped"
    } else {
        Write-OK "Container not running"
    }
} catch {
    Write-Host "  Docker not available, skipping container stop" -ForegroundColor Yellow
}

# --- 2. Remove shortcuts ---
Write-Step "Removing shortcuts"

$desktopPath  = [Environment]::GetFolderPath("CommonDesktopDirectory")
$startMenuDir = "$([Environment]::GetFolderPath("CommonPrograms"))\PassFlow Tracker"

$shortcuts = @(
    "$desktopPath\$APP_NAME.lnk",
    "$startMenuDir\$APP_NAME.lnk",
    "$startMenuDir\Uninstall $APP_NAME.lnk"
)
foreach ($s in $shortcuts) {
    if (Test-Path $s) { Remove-Item $s -Force; Write-OK "Removed: $s" }
}
if (Test-Path $startMenuDir) {
    Remove-Item -Recurse -Force $startMenuDir
    Write-OK "Start menu folder removed"
}

# --- 3. Remove registry entry ---
Write-Step "Removing registry entry"
$regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PassFlowTracker"
if (Test-Path $regPath) {
    Remove-Item -Path $regPath -Recurse -Force
    Write-OK "Registry entry removed"
}

# --- 4. Remove files ---
Write-Step "Removing files"

$keepSettings = Read-Host "Keep settings (appsettings.json)? (y/n)"
if ($keepSettings -eq "y") {
    $backup = "$env:USERPROFILE\passflow-appsettings-backup.json"
    $src    = "$INSTALL_DIR\appsettings.json"
    if (Test-Path $src) {
        Copy-Item $src $backup -Force
        Write-OK "Settings saved to: $backup"
    }
}

if (Test-Path $INSTALL_DIR) {
    Remove-Item -Recurse -Force $INSTALL_DIR
    Write-OK "Folder $INSTALL_DIR removed"
}

Write-Host ""
Write-Host "  PassFlow Tracker uninstalled." -ForegroundColor Green
Write-Host "  Docker container and PostgreSQL data preserved." -ForegroundColor Yellow
Write-Host "  To remove database data run: docker volume rm pgdata" -ForegroundColor Yellow
