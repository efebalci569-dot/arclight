<#
.SYNOPSIS
    Builds Arclight's Windows release files.

.DESCRIPTION
    Produces, in .\artifacts:
      - publish\                                 self-contained win-x64 build (single Arclight.exe)
      - Arclight-<version>-win-x64-portable.zip  portable build (keeps its data next to the exe)
      - Arclight-<version>-win-x64-Setup.exe     per-user installer (needs Inno Setup 6; skipped if missing)
      - SHA256SUMS.txt                           checksums of the files above

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Version 0.1.0
#>
param(
    [string]$Version,
    [string]$Configuration = 'Release',
    [switch]$SkipInstaller,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts 'publish'

if (-not $Version) {
    $props = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
    $Version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
}
$Version = $Version.TrimStart('v')
Write-Host "Building Arclight $Version" -ForegroundColor Cyan

if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

if (-not $SkipTests) {
    dotnet test (Join-Path $root 'Arclight.sln') -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

dotnet publish (Join-Path $root 'src\Arclight.Desktop\Arclight.Desktop.csproj') `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# Portable build: the marker file makes Arclight store its data in .\Data next to the exe.
$portableDir = Join-Path $artifacts 'portable'
Copy-Item $publishDir $portableDir -Recurse
Set-Content (Join-Path $portableDir 'portable.txt') "Arclight stores its library in the Data folder next to Arclight.exe while this file exists." -Encoding utf8
$zip = Join-Path $artifacts "Arclight-$Version-win-x64-portable.zip"
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $zip
Remove-Item $portableDir -Recurse -Force

if (-not $SkipInstaller) {
    $iscc = @(
        (Get-Command iscc -ErrorAction SilentlyContinue).Source,
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

    if ($iscc) {
        & $iscc "/DAppVersion=$Version" "/DSourceDir=$publishDir" "/DOutputDir=$artifacts" (Join-Path $root 'installer\Arclight.iss')
        if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }
    }
    else {
        Write-Warning 'Inno Setup 6 was not found; the installer was skipped. Install it from https://jrsoftware.org/isinfo.php or run with -SkipInstaller.'
    }
}

$files = Get-ChildItem $artifacts -File | Where-Object { $_.Extension -in '.zip', '.exe' }
$files | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } |
    Set-Content (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii

Write-Host "`nRelease files:" -ForegroundColor Green
Get-ChildItem $artifacts -File | ForEach-Object { "  {0,-45} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB) }
