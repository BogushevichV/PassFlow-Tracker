# ============================================================
# install.ps1 — установщик PassFlow Tracker для Windows
# Запуск от имени администратора: .\install.ps1
# ============================================================

$ErrorActionPreference = "Stop"

$APP_NAME    = "PassFlow Tracker"
$INSTALL_DIR = "C:\Program Files\PassFlow Tracker"
$SCRIPT_DIR  = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── Вспомогательная функция вывода ──────────────────────────
function Write-Step($msg) {
    Write-Host "`n=== $msg ===" -ForegroundColor Cyan
}
function Write-OK($msg) {
    Write-Host "  [OK] $msg" -ForegroundColor Green
}
function Write-Fail($msg) {
    Write-Host "  [!!] $msg" -ForegroundColor Red
}

# ── 1. Проверка прав администратора ─────────────────────────
Write-Step "Проверка прав"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Fail "Запустите скрипт от имени администратора (ПКМ → Запуск от имени администратора)"
    exit 1
}
Write-OK "Права администратора подтверждены"

# ── 2. Проверка Docker Desktop ───────────────────────────────
Write-Step "Проверка Docker Desktop"

$dockerExe = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerExe) {
    Write-Fail "Docker Desktop не найден!"
    Write-Host ""
    Write-Host "  PassFlow Tracker использует Docker для запуска базы данных PostgreSQL."
    Write-Host "  Установите Docker Desktop и повторите установку."
    Write-Host ""
    $open = Read-Host "  Открыть страницу загрузки Docker Desktop? (y/n)"
    if ($open -eq "y") {
        Start-Process "https://www.docker.com/products/docker-desktop/"
    }
    exit 1
}
Write-OK "Docker найден: $($dockerExe.Source)"

# Проверяем что Docker daemon запущен
try {
    docker info 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw }
    Write-OK "Docker daemon запущен"
} catch {
    Write-Fail "Docker daemon не запущен. Запустите Docker Desktop и повторите."
    exit 1
}

# ── 3. Установка файлов ──────────────────────────────────────
Write-Step "Установка файлов в $INSTALL_DIR"

if (Test-Path $INSTALL_DIR) {
    Write-Host "  Обнаружена предыдущая установка. Обновление..."
    Remove-Item -Recurse -Force $INSTALL_DIR
}

New-Item -ItemType Directory -Force -Path $INSTALL_DIR | Out-Null

# Копируем приложение
$appSource = Join-Path $SCRIPT_DIR "app"
if (-not (Test-Path $appSource)) {
    # Если запускаем из папки installer напрямую
    $appSource = Join-Path (Split-Path $SCRIPT_DIR -Parent) "publish"
}
Copy-Item -Recurse "$appSource\*" "$INSTALL_DIR\" -Force
Write-OK "Файлы приложения скопированы"

# Копируем appsettings.json если его нет
$settingsSrc = Join-Path $SCRIPT_DIR "appsettings.json"
if (Test-Path $settingsSrc) {
    Copy-Item $settingsSrc "$INSTALL_DIR\appsettings.json" -Force
    Write-OK "appsettings.json скопирован"
}

# ── 4. Создание bat-файла запуска ────────────────────────────
Write-Step "Создание файла запуска"

$launchBat = @"
@echo off
title PassFlow Tracker
echo Запуск PassFlow Tracker...

REM Проверка Docker
docker info >nul 2>&1
if errorlevel 1 (
    echo Ошибка: Docker Desktop не запущен.
    echo Запустите Docker Desktop и повторите.
    pause
    exit /b 1
)

REM Запуск приложения
cd /d "%~dp0"
start "" "PassFlowTracker.exe"
"@

$launchBat | Out-File -FilePath "$INSTALL_DIR\launch.bat" -Encoding ASCII
Write-OK "launch.bat создан"

# ── 5. Ярлык на рабочем столе ────────────────────────────────
Write-Step "Создание ярлыков"

$shell = New-Object -ComObject WScript.Shell

# Рабочий стол
$desktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory")
$shortcut = $shell.CreateShortcut("$desktopPath\$APP_NAME.lnk")
$shortcut.TargetPath  = "$INSTALL_DIR\launch.bat"
$shortcut.WorkingDirectory = $INSTALL_DIR
$shortcut.Description = "Система анализа пассажиропотока"
$shortcut.WindowStyle = 7  # минимизированное окно консоли
$shortcut.Save()
Write-OK "Ярлык на рабочем столе создан"

# Меню Пуск
$startMenuPath = [Environment]::GetFolderPath("CommonPrograms")
$startMenuDir  = "$startMenuPath\PassFlow Tracker"
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null

$shortcut2 = $shell.CreateShortcut("$startMenuDir\$APP_NAME.lnk")
$shortcut2.TargetPath       = "$INSTALL_DIR\launch.bat"
$shortcut2.WorkingDirectory = $INSTALL_DIR
$shortcut2.Description      = "Система анализа пассажиропотока"
$shortcut2.Save()

$shortcut3 = $shell.CreateShortcut("$startMenuDir\Удалить $APP_NAME.lnk")
$shortcut3.TargetPath  = "powershell.exe"
$shortcut3.Arguments   = "-ExecutionPolicy Bypass -File `"$INSTALL_DIR\uninstall.ps1`""
$shortcut3.Description = "Удалить PassFlow Tracker"
$shortcut3.Save()
Write-OK "Ярлыки в меню Пуск созданы"

# ── 6. Запись в реестр (Установка и удаление программ) ───────
Write-Step "Регистрация в системе"

$regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PassFlowTracker"
New-Item -Path $regPath -Force | Out-Null
Set-ItemProperty -Path $regPath -Name "DisplayName"      -Value $APP_NAME
Set-ItemProperty -Path $regPath -Name "DisplayVersion"   -Value "1.0"
Set-ItemProperty -Path $regPath -Name "Publisher"        -Value "PassFlow Team"
Set-ItemProperty -Path $regPath -Name "InstallLocation"  -Value $INSTALL_DIR
Set-ItemProperty -Path $regPath -Name "UninstallString"  -Value "powershell.exe -ExecutionPolicy Bypass -File `"$INSTALL_DIR\uninstall.ps1`""
Set-ItemProperty -Path $regPath -Name "NoModify"         -Value 1 -Type DWord
Set-ItemProperty -Path $regPath -Name "NoRepair"         -Value 1 -Type DWord
Write-OK "Приложение зарегистрировано в системе"

# ── 7. Копируем скрипт удаления ──────────────────────────────
$uninstallSrc = Join-Path $SCRIPT_DIR "uninstall.ps1"
if (Test-Path $uninstallSrc) {
    Copy-Item $uninstallSrc "$INSTALL_DIR\uninstall.ps1" -Force
}

# ── Готово ───────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   PassFlow Tracker успешно установлен!       ║" -ForegroundColor Green
Write-Host "║                                              ║" -ForegroundColor Green
Write-Host "║   Запуск: ярлык на рабочем столе             ║" -ForegroundColor Green
Write-Host "║   Папка:  $INSTALL_DIR  ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

$launch = Read-Host "Запустить приложение сейчас? (y/n)"
if ($launch -eq "y") {
    Start-Process "$INSTALL_DIR\launch.bat"
}
