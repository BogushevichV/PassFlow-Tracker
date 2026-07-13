# make-release.ps1 - create zip archive for distribution
# Run after build-windows.ps1: .\installer\make-release.ps1

$VERSION  = "1.0"
$DIST_DIR = "dist"
$ZIP_NAME = "PassFlowTracker-v$VERSION-win-x64.zip"

if (-not (Test-Path $DIST_DIR)) {
    Write-Error "Folder $DIST_DIR not found. Run build-windows.ps1 first."
    exit 1
}

Copy-Item "installer\README.md"     "$DIST_DIR\README.md"     -Force
Copy-Item "installer\uninstall.ps1" "$DIST_DIR\uninstall.ps1" -Force

if (Test-Path $ZIP_NAME) { Remove-Item $ZIP_NAME -Force }
Compress-Archive -Path "$DIST_DIR\*" -DestinationPath $ZIP_NAME

$sizeMB = [math]::Round((Get-Item $ZIP_NAME).Length / 1MB, 1)
Write-Host "Archive created: $ZIP_NAME ($sizeMB MB)" -ForegroundColor Green
