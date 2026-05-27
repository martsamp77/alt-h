param(
    [string]$Version = "latest",
    [switch]$NoStartup,
    [switch]$NoRun,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

$Repo = "martsamp77/alt-h"
$AppName = "AltHMinimize"
$DisplayName = "Alt-H Minimize"
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\AltHMinimize"
$ExePath = Join-Path $InstallDir "AltHMinimize.exe"
$RunKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$RunValueName = "AltHMinimize"

function Stop-AltHMinimize {
    Get-Process -Name $AppName -ErrorAction SilentlyContinue | Stop-Process -Force
}

function Set-StartupEntry {
    param([bool]$Enabled)

    if ($Enabled) {
        New-Item -Path $RunKeyPath -Force | Out-Null
        Set-ItemProperty -Path $RunKeyPath -Name $RunValueName -Value "`"$ExePath`""
        return
    }

    Remove-ItemProperty -Path $RunKeyPath -Name $RunValueName -ErrorAction SilentlyContinue
}

function Get-Release {
    param([string]$RequestedVersion)

    $headers = @{ "User-Agent" = "$AppName-installer" }
    if ($RequestedVersion -eq "latest") {
        return Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/$Repo/releases/latest"
    }

    $tag = if ($RequestedVersion.StartsWith("v")) { $RequestedVersion } else { "v$RequestedVersion" }
    return Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/$Repo/releases/tags/$tag"
}

if ($Uninstall) {
    Stop-AltHMinimize
    Set-StartupEntry -Enabled $false
    if (Test-Path $InstallDir) {
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
    }
    Write-Host "$DisplayName uninstalled."
    return
}

$release = Get-Release -RequestedVersion $Version
$asset = $release.assets | Where-Object { $_.name -like "AltHMinimize-*-win-x64.exe" } | Select-Object -First 1
if (-not $asset) {
    throw "Could not find a win-x64 executable asset on release $($release.tag_name)."
}

$tempPath = Join-Path ([System.IO.Path]::GetTempPath()) $asset.name
Write-Host "Downloading $($asset.name) from $($release.tag_name)..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tempPath

Stop-AltHMinimize
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -LiteralPath $tempPath -Destination $ExePath -Force
Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue

Set-StartupEntry -Enabled (-not $NoStartup)

if (-not $NoRun) {
    Start-Process -FilePath $ExePath
}

Write-Host "$DisplayName installed to $ExePath."
