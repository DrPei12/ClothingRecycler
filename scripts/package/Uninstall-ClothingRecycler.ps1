$ErrorActionPreference = "Stop"

$appName = "衣物回收管理"
$installRoot = $PSScriptRoot
$startMenuFolder = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\ClothingRecycler"
$desktopShortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "$appName.lnk"
$startMenuShortcutPath = Join-Path $startMenuFolder "$appName.lnk"
$uninstallShortcutPath = Join-Path $startMenuFolder "卸载$appName.lnk"
$uninstallRegistryKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ClothingRecycler"

Get-Process "ClothingRecycler.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-Item -Path $desktopShortcutPath -Force -ErrorAction SilentlyContinue
Remove-Item -Path $startMenuShortcutPath -Force -ErrorAction SilentlyContinue
Remove-Item -Path $uninstallShortcutPath -Force -ErrorAction SilentlyContinue
Remove-Item -Path $startMenuFolder -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $uninstallRegistryKey -Recurse -Force -ErrorAction SilentlyContinue

$cleanupScriptPath = Join-Path $env:TEMP ("clothingrecycler-cleanup-" + [Guid]::NewGuid().ToString("N") + ".cmd")
$cleanupScriptContent = @"
@echo off
ping 127.0.0.1 -n 3 > nul
rd /s /q "$installRoot"
del /f /q "%~f0"
"@
Set-Content -Path $cleanupScriptPath -Value $cleanupScriptContent -Encoding ascii
Start-Process -FilePath "cmd.exe" -ArgumentList "/c `"$cleanupScriptPath`"" -WindowStyle Hidden

Write-Output "Uninstall scheduled for: $installRoot"
