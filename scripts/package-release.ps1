param(
    [string]$Version,
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot "AltHMinimize.csproj"
$ArtifactsRoot = Join-Path $RepoRoot "artifacts\release"
$ProjectXml = [xml](Get-Content -LiteralPath $ProjectPath)

if (-not $Version) {
    $Version = $ProjectXml.Project.PropertyGroup.Version
}

$Tag = if ($Version.StartsWith("v")) { $Version } else { "v$Version" }
$VersionNumber = $Tag.TrimStart("v")

$CsprojVersion = $ProjectXml.Project.PropertyGroup.Version
if ($CsprojVersion -and $VersionNumber -ne $CsprojVersion) {
    throw "Version mismatch: packaging $VersionNumber but AltHMinimize.csproj has <Version>$CsprojVersion</Version>. Update the csproj (and CHANGELOG.md) first."
}
$OutDir = Join-Path $ArtifactsRoot $Tag
$PublishDir = Join-Path $OutDir "publish"
$ExeAssetName = "AltHMinimize-$Tag-$Runtime.exe"
$MsiAssetName = "AltHMinimize-$Tag-$Runtime.msi"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE."
    }
}

Get-Process -Name "AltHMinimize" -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $OutDir) {
    Remove-Item -LiteralPath $OutDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

Invoke-Checked { dotnet restore $ProjectPath }
Invoke-Checked {
    dotnet publish $ProjectPath `
        -c Release `
        -r $Runtime `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:PublishTrimmed=false `
        /p:Version=$VersionNumber `
        /p:PublishDir="$PublishDir\"
}

$PublishedExe = Join-Path $PublishDir "AltHMinimize.exe"
$ExeAssetPath = Join-Path $OutDir $ExeAssetName
Copy-Item -LiteralPath $PublishedExe -Destination $ExeAssetPath -Force

Invoke-Checked { dotnet tool restore }
Invoke-Checked { dotnet wix eula accept wix7 }
Invoke-Checked {
    dotnet wix build (Join-Path $RepoRoot "Installer\AltHMinimize.wxs") `
        -arch x64 `
        -d ProductVersion=$VersionNumber `
        -d PublishDir=$PublishDir `
        -out (Join-Path $OutDir $MsiAssetName) `
        -pdbtype none
}

Copy-Item -LiteralPath (Join-Path $RepoRoot "install.ps1") -Destination (Join-Path $OutDir "install.ps1") -Force

$checksumLines = Get-ChildItem -LiteralPath $OutDir -File |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object Name |
    ForEach-Object {
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
        "$($hash.Hash.ToLowerInvariant())  $($_.Name)"
    }

$checksumLines | Set-Content -LiteralPath (Join-Path $OutDir "SHA256SUMS.txt") -Encoding ascii

Write-Host "Release assets written to $OutDir"
Get-ChildItem -LiteralPath $OutDir -File | Select-Object Name, Length
