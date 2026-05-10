# ============================================================
# make-release.ps1 — создаёт zip-архив для распространения
# Запуск после build-windows.ps1: .\installer\make-release.ps1
# ============================================================

$VERSION  = "1.0"
$DIST_DIR = "dist"
$ZIP_NAME = "PassFlowTracker-v$VERSION-win-x64.zip"

if (-not (Test-Path $DIST_DIR)) {
    Write-Error "Папка $DIST_DIR не найдена. Сначала запустите build-windows.ps1"
    exit 1
}

# Копируем README и uninstall в dist
Copy-Item "installer\README.md"    "$DIST_DIR\README.md"    -Force
Copy-Item "installer\uninstall.ps1" "$DIST_DIR\uninstall.ps1" -Force

# Копируем appsettings.json в app
Copy-Item "PassFlow Tracker\appsettings.json" "$DIST_DIR\app\appsettings.json" -Force

# Создаём архив
if (Test-Path $ZIP_NAME) { Remove-Item $ZIP_NAME -Force }
Compress-Archive -Path "$DIST_DIR\*" -DestinationPath $ZIP_NAME

Write-Host "Архив создан: $ZIP_NAME" -ForegroundColor Green
Write-Host "Размер: $([math]::Round((Get-Item $ZIP_NAME).Length / 1MB, 1)) МБ"
