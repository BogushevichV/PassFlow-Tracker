# ============================================================
# uninstall.ps1 — удаление PassFlow Tracker
# ============================================================

$ErrorActionPreference = "Stop"

$APP_NAME    = "PassFlow Tracker"
$INSTALL_DIR = "C:\Program Files\PassFlow Tracker"

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-OK($msg)   { Write-Host "  [OK] $msg"   -ForegroundColor Green }

# Проверка прав
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Запустите от имени администратора" -ForegroundColor Red
    exit 1
}

$confirm = Read-Host "Удалить PassFlow Tracker? (y/n)"
if ($confirm -ne "y") { exit 0 }

# ── 1. Остановка Docker-контейнера PostgreSQL ────────────────
Write-Step "Остановка базы данных"
try {
    $containerName = "my_postgres"
    $running = docker ps --filter "name=$containerName" --format "{{.Names}}" 2>$null
    if ($running -eq $containerName) {
        docker stop $containerName | Out-Null
        Write-OK "Контейнер $containerName остановлен"
    } else {
        Write-OK "Контейнер не запущен"
    }
} catch {
    Write-Host "  Docker недоступен, пропускаем остановку контейнера" -ForegroundColor Yellow
}

# ── 2. Удаление ярлыков ──────────────────────────────────────
Write-Step "Удаление ярлыков"

$desktopPath  = [Environment]::GetFolderPath("CommonDesktopDirectory")
$startMenuDir = "$([Environment]::GetFolderPath("CommonPrograms"))\PassFlow Tracker"

$shortcuts = @(
    "$desktopPath\$APP_NAME.lnk",
    "$startMenuDir\$APP_NAME.lnk",
    "$startMenuDir\Удалить $APP_NAME.lnk"
)
foreach ($s in $shortcuts) {
    if (Test-Path $s) { Remove-Item $s -Force; Write-OK "Удалён: $s" }
}
if (Test-Path $startMenuDir) {
    Remove-Item -Recurse -Force $startMenuDir
    Write-OK "Папка меню Пуск удалена"
}

# ── 3. Удаление из реестра ───────────────────────────────────
Write-Step "Удаление из реестра"
$regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PassFlowTracker"
if (Test-Path $regPath) {
    Remove-Item -Path $regPath -Recurse -Force
    Write-OK "Запись реестра удалена"
}

# ── 4. Удаление файлов ───────────────────────────────────────
Write-Step "Удаление файлов"

# Сохраняем appsettings.json если пользователь хочет
$keepSettings = Read-Host "Сохранить настройки (appsettings.json)? (y/n)"

if (Test-Path $INSTALL_DIR) {
    if ($keepSettings -eq "y") {
        $settingsBackup = "$env:USERPROFILE\passflow-appsettings-backup.json"
        $settingsSrc    = "$INSTALL_DIR\appsettings.json"
        if (Test-Path $settingsSrc) {
            Copy-Item $settingsSrc $settingsBackup -Force
            Write-OK "Настройки сохранены: $settingsBackup"
        }
    }
    Remove-Item -Recurse -Force $INSTALL_DIR
    Write-OK "Папка $INSTALL_DIR удалена"
}

Write-Host ""
Write-Host "PassFlow Tracker удалён." -ForegroundColor Green
Write-Host "Docker-контейнер и данные PostgreSQL сохранены." -ForegroundColor Yellow
Write-Host "Для удаления данных БД выполните: docker volume rm pgdata" -ForegroundColor Yellow
