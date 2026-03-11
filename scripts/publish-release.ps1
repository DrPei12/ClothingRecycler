param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "ClothingRecycler.Desktop.csproj"
$installerProject = Join-Path $projectRoot "Installer\ClothingRecycler.Installer.csproj"
$installerPayloadPath = Join-Path $projectRoot "Installer\payload.zip"
$artifactsRoot = Join-Path $projectRoot "artifacts"
$publishRoot = Join-Path (Join-Path $artifactsRoot "publish") $Runtime
$installerPublishRoot = Join-Path (Join-Path $artifactsRoot "installer-publish") $Runtime
$releaseRoot = Join-Path $artifactsRoot "release"
$templateRoot = Join-Path $PSScriptRoot "package"
$packageName = "ClothingRecycler_PC_v$Version" + "_$Runtime"
$packageDir = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot "$packageName.zip"
$installerExePath = Join-Path $releaseRoot ("ClothingRecycler_PC_Install_v{0}.exe" -f $Version)
$launcherPath = Join-Path $packageDir "Run-ClothingRecycler.bat"
$installPs1Path = Join-Path $packageDir "Install-ClothingRecycler.ps1"
$installBatPath = Join-Path $packageDir "Install-ClothingRecycler.bat"
$uninstallPs1Path = Join-Path $packageDir "Uninstall-ClothingRecycler.ps1"
$uninstallBatPath = Join-Path $packageDir "Uninstall-ClothingRecycler.bat"
$readmePath = Join-Path $packageDir "README.txt"

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $installerPublishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null

if (Test-Path $publishRoot) {
    Remove-Item -Path (Join-Path $publishRoot "*") -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path $installerPublishRoot) {
    Remove-Item -Path (Join-Path $installerPublishRoot "*") -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path $packageDir) {
    Remove-Item -Path $packageDir -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

if (Test-Path $installerExePath) {
    Remove-Item -Path $installerExePath -Force
}

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

$platform = switch ($Runtime) {
    "win-x86" { "x86" }
    "win-arm64" { "ARM64" }
    default { "x64" }
}

dotnet publish $projectFile `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Platform=$platform `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=true `
    -o $publishRoot

Copy-Item -Path (Join-Path $publishRoot "*") -Destination $packageDir -Recurse

$launcherContent = @"
@echo off
setlocal
start "" "%~dp0ClothingRecycler.Desktop.exe"
"@
Set-Content -Path $launcherPath -Value $launcherContent -Encoding ascii

$installPs1Content = (Get-Content -Encoding utf8 -Path (Join-Path $templateRoot "Install-ClothingRecycler.ps1") -Raw).Replace("__VERSION__", $Version)
Set-Content -Path $installPs1Path -Value $installPs1Content -Encoding utf8
Copy-Item -Path (Join-Path $templateRoot "Install-ClothingRecycler.bat") -Destination $installBatPath -Force
Copy-Item -Path (Join-Path $templateRoot "Uninstall-ClothingRecycler.ps1") -Destination $uninstallPs1Path -Force
Copy-Item -Path (Join-Path $templateRoot "Uninstall-ClothingRecycler.bat") -Destination $uninstallBatPath -Force

$readmeContent = @"
ClothingRecycler PC Release

Version: $Version
Runtime: $Runtime

Install:
1. Double-click Install-ClothingRecycler.bat
2. The installer lets you choose the install directory
3. The recommended default is %LOCALAPPDATA%\Programs\ClothingRecycler
4. It creates desktop and Start Menu shortcuts, and also registers uninstall

Run:
1. Portable mode: open this folder and double-click Run-ClothingRecycler.bat
2. Installed mode: use the desktop or Start Menu shortcut

Data:
- The application stores its local database, backups, exports, and logs under:
  %LOCALAPPDATA%\ClothingRecycler

Notes:
- This is an unpackaged local desktop release.
- Uninstall is available from the Start Menu shortcut or installed folder.
- The first launch will create local folders automatically.
"@
Set-Content -Path $readmePath -Value $readmeContent -Encoding utf8

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force

Copy-Item -Path $zipPath -Destination $installerPayloadPath -Force

dotnet publish $installerProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $installerPublishRoot

$builtInstallerExe = Join-Path $installerPublishRoot "ClothingRecycler.Installer.exe"
Copy-Item -Path $builtInstallerExe -Destination $installerExePath -Force

Remove-Item -Path $installerPayloadPath -Force -ErrorAction SilentlyContinue

Write-Output "Publish folder: $publishRoot"
Write-Output "Release folder: $packageDir"
Write-Output "Zip package: $zipPath"
Write-Output "Installer exe: $installerExePath"
