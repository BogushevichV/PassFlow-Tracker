# build-windows.ps1 - build PassFlow Tracker distributiive
# Run: .\build-windows.ps1

$ErrorActionPreference = "Stop"

$VERSION      = "1.0"
$PROJECT_PATH = "PassFlow Tracker\PassFlow Tracker.csproj"
$PUBLISH_DIR  = "publish"
$DIST_DIR     = "dist"

Write-Host "=== 1. Cleanup ===" -ForegroundColor Cyan
if (Test-Path $PUBLISH_DIR) { Remove-Item -Recurse -Force $PUBLISH_DIR }
if (Test-Path $DIST_DIR)    { Remove-Item -Recurse -Force $DIST_DIR    }

Write-Host "=== 2. Publish ===" -ForegroundColor Cyan
dotnet publish $PROJECT_PATH `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $PUBLISH_DIR

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed!"
    exit 1
}

Write-Host "=== 3. Create dist structure ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path "$DIST_DIR\app"     | Out-Null
New-Item -ItemType Directory -Force -Path "$DIST_DIR\scripts" | Out-Null

Copy-Item -Recurse "$PUBLISH_DIR\*" "$DIST_DIR\app\"

Copy-Item "installer\install.ps1"   "$DIST_DIR\install.ps1"
Copy-Item "installer\uninstall.ps1" "$DIST_DIR\uninstall.ps1"

# Copy appsettings
Copy-Item "PassFlow Tracker\appsettings.json" "$DIST_DIR\app\appsettings.json" -Force

Write-Host "=== 4. Done ===" -ForegroundColor Green
Write-Host "Distribution: $DIST_DIR\"
Write-Host "To install run: $DIST_DIR\install.ps1"
