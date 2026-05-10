# ============================================================
# build-windows.ps1 — сборка дистрибутива PassFlow Tracker
# Запуск: .\build-windows.ps1
# ============================================================

$ErrorActionPreference = "Stop"

$VERSION      = "1.0"
$APP_NAME     = "PassFlow Tracker"
$PROJECT_PATH = "PassFlow Tracker\PassFlow Tracker.csproj"
$PUBLISH_DIR  = "publish"
$DIST_DIR     = "dist"

Write-Host "=== 1. Очистка ===" -ForegroundColor Cyan
if (Test-Path $PUBLISH_DIR) { Remove-Item -Recurse -Force $PUBLISH_DIR }
if (Test-Path $DIST_DIR)    { Remove-Item -Recurse -Force $DIST_DIR    }

Write-Host "=== 2. Публикация приложения ===" -ForegroundColor Cyan
dotnet publish $PROJECT_PATH `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $PUBLISH_DIR

if ($LASTEXITCODE -ne 0) {
    Write-Error "Ошибка публикации!"
    exit 1
}

Write-Host "=== 3. Создание структуры дистрибутива ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path "$DIST_DIR\app"     | Out-Null
New-Item -ItemType Directory -Force -Path "$DIST_DIR\scripts" | Out-Null

# Копируем опубликованные файлы
Copy-Item -Recurse "$PUBLISH_DIR\*" "$DIST_DIR\app\"

# Копируем вспомогательные файлы
Copy-Item "installer\install.ps1"   "$DIST_DIR\install.ps1"
Copy-Item "installer\uninstall.ps1" "$DIST_DIR\uninstall.ps1"
Copy-Item "installer\launch.bat"    "$DIST_DIR\app\launch.bat"

Write-Host "=== 4. Готово ===" -ForegroundColor Green
Write-Host "Дистрибутив: $DIST_DIR\"
Write-Host "Для установки запустите: $DIST_DIR\install.ps1"
