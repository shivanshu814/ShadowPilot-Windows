#Requires -Version 5.1
<#
.SYNOPSIS
    ShadowPilot Windows Installer
.DESCRIPTION
    Installs .NET 8 if missing, builds ShadowPilot, and creates desktop + startup shortcuts.
    Run once on your Windows machine: Right-click install.ps1 → "Run with PowerShell"
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$AppName    = "ShadowPilot"
$InstallDir = "$env:LOCALAPPDATA\ShadowPilot"
$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Definition

# ── Helpers ───────────────────────────────────────────────────────────────────
function Write-Step  { param($msg) Write-Host "`n▶  $msg" -ForegroundColor Cyan }
function Write-Ok    { param($msg) Write-Host "   ✓  $msg" -ForegroundColor Green }
function Write-Warn  { param($msg) Write-Host "   ⚠  $msg" -ForegroundColor Yellow }
function Write-Fatal { param($msg) Write-Host "`n✕  $msg" -ForegroundColor Red; Read-Host "Press Enter to exit"; exit 1 }

function Ensure-ElevatedOrSkip {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        Write-Warn "Not running as Administrator — machine-wide installs may be skipped."
    }
}

# ── Step 1: Check .NET 8 SDK ──────────────────────────────────────────────────
function Ensure-DotNet8 {
    Write-Step "Checking .NET 8 SDK..."

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        $sdks = & dotnet --list-sdks 2>$null
        if ($sdks -match "^8\.") {
            Write-Ok ".NET 8 SDK found"
            return
        }
    }

    Write-Warn ".NET 8 SDK not found — downloading installer..."

    $installerUrl = "https://dot.net/v1/dotnet-install.ps1"
    $installerPath = "$env:TEMP\dotnet-install.ps1"

    try {
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
        & $installerPath -Channel 8.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
        # Add to PATH for this session
        $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
        Write-Ok ".NET 8 SDK installed to $env:LOCALAPPDATA\Microsoft\dotnet"
    } catch {
        Write-Fatal ".NET 8 SDK install failed. Please install manually from https://dot.net/download then re-run this script."
    }
}

# ── Step 2: Create .env if missing ───────────────────────────────────────────
function Ensure-EnvFile {
    Write-Step "Checking API key config..."

    $envPath = "$env:USERPROFILE\.shadowpilot.env"
    if (Test-Path $envPath) {
        $content = Get-Content $envPath -Raw
        if ($content -match "OPENAI_API_KEY=sk-" -or $content -match "OPENROUTER_API_KEY=sk-") {
            Write-Ok "API key found in $envPath"
            return
        }
    }

    Write-Warn "No API key found at $envPath"
    Write-Host ""
    Write-Host "   Enter your OpenAI API key (starts with sk-...):" -ForegroundColor White
    $key = Read-Host "   OPENAI_API_KEY"

    if ($key -and $key.StartsWith("sk-")) {
        "OPENAI_API_KEY=$key" | Set-Content -Path $envPath -Encoding UTF8
        Write-Ok "Saved to $envPath"
    } else {
        Write-Warn "Skipping — you can add it later to $envPath"
        "# OPENAI_API_KEY=sk-your-key-here`n# OPENROUTER_API_KEY=sk-or-your-key-here" |
            Set-Content -Path $envPath -Encoding UTF8
    }
}

# ── Step 3: Build ─────────────────────────────────────────────────────────────
function Build-App {
    Write-Step "Building ShadowPilot (Release)..."

    $projectPath = Join-Path $ScriptDir "ShadowPilot\ShadowPilot.csproj"
    if (-not (Test-Path $projectPath)) {
        Write-Fatal "Project file not found at $projectPath — make sure install.ps1 is in the ShadowPilot-Windows folder."
    }

    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

    $buildArgs = @(
        "publish",
        $projectPath,
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:PublishTrimmed=false",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-o", $InstallDir
    )

    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Fatal "Build failed. Check errors above."
    }

    Write-Ok "Built to $InstallDir"
}

# ── Step 4: Copy .env into install dir ───────────────────────────────────────
function Copy-Env {
    $envPath = "$env:USERPROFILE\.shadowpilot.env"
    if (Test-Path $envPath) {
        Copy-Item $envPath "$InstallDir\.env" -Force
    }
}

# ── Step 5: Create shortcuts ──────────────────────────────────────────────────
function Create-Shortcuts {
    Write-Step "Creating shortcuts..."

    $exePath = "$InstallDir\ShadowPilot.exe"
    $wsh     = New-Object -ComObject WScript.Shell

    # Desktop shortcut
    $desktop = [Environment]::GetFolderPath("Desktop")
    $lnk     = $wsh.CreateShortcut("$desktop\ShadowPilot.lnk")
    $lnk.TargetPath       = $exePath
    $lnk.WorkingDirectory = $InstallDir
    $lnk.Description      = "ShadowPilot — Interview Co-pilot"
    $lnk.Save()
    Write-Ok "Desktop shortcut created"

    # Start Menu shortcut
    $startMenu = [Environment]::GetFolderPath("Programs")
    $smDir     = "$startMenu\ShadowPilot"
    New-Item -ItemType Directory -Path $smDir -Force | Out-Null
    $lnk2 = $wsh.CreateShortcut("$smDir\ShadowPilot.lnk")
    $lnk2.TargetPath       = $exePath
    $lnk2.WorkingDirectory = $InstallDir
    $lnk2.Description      = "ShadowPilot — Interview Co-pilot"
    $lnk2.Save()
    Write-Ok "Start Menu shortcut created"
}

# ── Step 6: Register uninstaller ─────────────────────────────────────────────
function Register-Uninstaller {
    $uninstallScript = @"
Remove-Item "$InstallDir" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "`$([Environment]::GetFolderPath('Desktop'))\ShadowPilot.lnk" -ErrorAction SilentlyContinue
Remove-Item "`$([Environment]::GetFolderPath('Programs'))\ShadowPilot" -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ShadowPilot" -Name * -ErrorAction SilentlyContinue
Write-Host "ShadowPilot uninstalled." -ForegroundColor Green
Read-Host "Press Enter to close"
"@
    $uninstallPath = "$InstallDir\uninstall.ps1"
    $uninstallScript | Set-Content $uninstallPath -Encoding UTF8

    # Add to Add/Remove Programs (HKCU — no admin needed)
    $regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ShadowPilot"
    New-Item -Path $regPath -Force | Out-Null
    Set-ItemProperty $regPath "DisplayName"          "ShadowPilot"
    Set-ItemProperty $regPath "DisplayVersion"       "1.0.0"
    Set-ItemProperty $regPath "Publisher"            "ShadowPilot"
    Set-ItemProperty $regPath "InstallLocation"      $InstallDir
    Set-ItemProperty $regPath "UninstallString"      "powershell -ExecutionPolicy Bypass -File `"$uninstallPath`""
    Set-ItemProperty $regPath "NoModify"             1 -Type DWord
    Set-ItemProperty $regPath "NoRepair"             1 -Type DWord
}

# ── Main ──────────────────────────────────────────────────────────────────────
Clear-Host
Write-Host @"

  ███████╗██╗  ██╗ █████╗ ██████╗  ██████╗ ██╗    ██╗
  ██╔════╝██║  ██║██╔══██╗██╔══██╗██╔═══██╗██║    ██║
  ███████╗███████║███████║██║  ██║██║   ██║██║ █╗ ██║
  ╚════██║██╔══██║██╔══██║██║  ██║██║   ██║██║███╗██║
  ███████║██║  ██║██║  ██║██████╔╝╚██████╔╝╚███╔███╔╝
  ╚══════╝╚═╝  ╚═╝╚═╝  ╚═╝╚═════╝  ╚═════╝  ╚══╝╚══╝

  P I L O T   —   Windows Installer
"@ -ForegroundColor Yellow

Write-Host "  Installing to: $InstallDir" -ForegroundColor DarkGray
Write-Host ""

Ensure-ElevatedOrSkip
Ensure-DotNet8
Ensure-EnvFile
Build-App
Copy-Env
Create-Shortcuts
Register-Uninstaller

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  ✓  ShadowPilot installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "  Hotkeys (work system-wide):" -ForegroundColor White
Write-Host "    Ctrl+Shift+L  →  Mic toggle (listen)" -ForegroundColor DarkGray
Write-Host "    Ctrl+Shift+A  →  Get AI answer" -ForegroundColor DarkGray
Write-Host "    Ctrl+Shift+D  →  Screenshot + analyze" -ForegroundColor DarkGray
Write-Host "    Ctrl+Shift+X  →  Clear" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Launch: Double-click ShadowPilot on your Desktop" -ForegroundColor White
Write-Host "  Uninstall: Settings → Apps → ShadowPilot" -ForegroundColor DarkGray
Write-Host ""

$launch = Read-Host "  Launch ShadowPilot now? (Y/n)"
if ($launch -ne "n" -and $launch -ne "N") {
    Start-Process "$InstallDir\ShadowPilot.exe"
}
