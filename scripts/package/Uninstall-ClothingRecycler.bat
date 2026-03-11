@echo off
setlocal
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0Uninstall-ClothingRecycler.ps1"
exit /b %errorlevel%
