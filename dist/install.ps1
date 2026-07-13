# install.ps1 - PassFlow Tracker installer for Windows
# Run as Administrator: .\install.ps1

$ErrorActionPreference = "Stop"

$APP_NAME    = "PassFlow Tracker"
$INSTALL_DIR = "C:\Program Files\PassFlow Tracker"
$SCRIPT_DIR  = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-OK($msg)   { Write-Host "  [OK] $msg"   -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "  [!!] $msg"   -ForegroundColor Red   }

# --- 1. Check admin rights ---
Write-Step "Checking admin rights"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Fail "Run this script as Administrator (right-click -> Run as Administrator)"
    exit 1
}
Write-OK "Admin rights confirmed"

# --- 2. Check Docker Desktop ---
Write-Step "Checking Docker Desktop"

$dockerExe = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerExe) {
    Write-Fail "Docker Desktop not found!"
    Write-Host ""
    Write-Host "  PassFlow Tracker requires Docker Desktop for the PostgreSQL database."
    Write-Host "  Please install Docker Desktop and run this installer again."
    Write-Host ""
    $open = Read-Host "  Open Docker Desktop download page? (y/n)"
    if ($open -eq "y") {
        Start-Process "https://www.docker.com/products/docker-desktop/"
    }
    exit 1
}
Write-OK "Docker found: $($dockerExe.Source)"

try {
    $null = docker info 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Docker daemon not running" }
    Write-OK "Docker daemon is running"
} catch {
    Write-Fail "Docker daemon is not running. Start Docker Desktop and try again."
    exit 1
}

# --- 3. Copy files ---
Write-Step "Installing files to $INSTALL_DIR"

if (Test-Path $INSTALL_DIR) {
    Write-Host "  Previous installation found. Updating..."
    Remove-Item -Recurse -Force $INSTALL_DIR
}

New-Item -ItemType Directory -Force -Path $INSTALL_DIR | Out-Null

$appSource = Join-Path $SCRIPT_DIR "app"
if (-not (Test-Path $appSource)) {
    $appSource = Join-Path (Split-Path $SCRIPT_DIR -Parent) "publish"
}

Copy-Item -Recurse "$appSource\*" "$INSTALL_DIR\" -Force
Write-OK "Application files copied"

$settingsSrc = Join-Path $SCRIPT_DIR "appsettings.json"
if (-not (Test-Path $settingsSrc)) {
    $settingsSrc = Join-Path $appSource "appsettings.json"
}
if (Test-Path $settingsSrc) {
    Copy-Item $settingsSrc "$INSTALL_DIR\appsettings.json" -Force
    Write-OK "appsettings.json copied"
}

# --- 4. Create launch.bat ---
Write-Step "Creating launcher"

$launchContent = "@echo off`r`ntitle PassFlow Tracker`r`necho Starting PassFlow Tracker...`r`n`r`ndocker info >nul 2>&1`r`nif errorlevel 1 (`r`n    echo Error: Docker Desktop is not running.`r`n    echo Please start Docker Desktop and try again.`r`n    pause`r`n    exit /b 1`r`n)`r`n`r`ncd /d `"%~dp0`"`r`nstart `"`" `"PassFlowTracker.exe`"`r`n"

[System.IO.File]::WriteAllText("$INSTALL_DIR\launch.bat", $launchContent, [System.Text.Encoding]::ASCII)
Write-OK "launch.bat created"

# --- 5. Copy uninstall script ---
$uninstallSrc = Join-Path $SCRIPT_DIR "uninstall.ps1"
if (Test-Path $uninstallSrc) {
    Copy-Item $uninstallSrc "$INSTALL_DIR\uninstall.ps1" -Force
}

# --- 6. Create shortcuts ---
Write-Step "Creating shortcuts"

$shell = New-Object -ComObject WScript.Shell

$desktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory")
$sc1 = $shell.CreateShortcut("$desktopPath\$APP_NAME.lnk")
$sc1.TargetPath       = "$INSTALL_DIR\launch.bat"
$sc1.WorkingDirectory = $INSTALL_DIR
$sc1.Description      = "PassFlow Tracker - passenger flow analysis"
$sc1.WindowStyle      = 7
$sc1.Save()
Write-OK "Desktop shortcut created"

$startMenuPath = [Environment]::GetFolderPath("CommonPrograms")
$startMenuDir  = "$startMenuPath\PassFlow Tracker"
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null

$sc2 = $shell.CreateShortcut("$startMenuDir\$APP_NAME.lnk")
$sc2.TargetPath       = "$INSTALL_DIR\launch.bat"
$sc2.WorkingDirectory = $INSTALL_DIR
$sc2.Save()

$sc3 = $shell.CreateShortcut("$startMenuDir\Uninstall $APP_NAME.lnk")
$sc3.TargetPath  = "powershell.exe"
$sc3.Arguments   = "-ExecutionPolicy Bypass -File `"$INSTALL_DIR\uninstall.ps1`""
$sc3.Save()
Write-OK "Start menu shortcuts created"

# --- 7. Register in Add/Remove Programs ---
Write-Step "Registering in system"

$regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PassFlowTracker"
New-Item -Path $regPath -Force | Out-Null
Set-ItemProperty -Path $regPath -Name "DisplayName"     -Value $APP_NAME
Set-ItemProperty -Path $regPath -Name "DisplayVersion"  -Value "1.0"
Set-ItemProperty -Path $regPath -Name "Publisher"       -Value "PassFlow Team"
Set-ItemProperty -Path $regPath -Name "InstallLocation" -Value $INSTALL_DIR
Set-ItemProperty -Path $regPath -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$INSTALL_DIR\uninstall.ps1`""
Set-ItemProperty -Path $regPath -Name "NoModify"        -Value 1 -Type DWord
Set-ItemProperty -Path $regPath -Name "NoRepair"        -Value 1 -Type DWord
Write-OK "Registered in Add/Remove Programs"

# --- Done ---
Write-Host ""
Write-Host "  PassFlow Tracker installed successfully!" -ForegroundColor Green
Write-Host "  Location: $INSTALL_DIR"                  -ForegroundColor Green
Write-Host "  Launch:   desktop shortcut"              -ForegroundColor Green
Write-Host ""

$launch = Read-Host "Launch the application now? (y/n)"
if ($launch -eq "y") {
    Start-Process "$INSTALL_DIR\launch.bat"
}
