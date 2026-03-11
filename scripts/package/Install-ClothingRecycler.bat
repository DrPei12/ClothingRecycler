@echo off
setlocal
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0Install-ClothingRecycler.ps1"
exit /b %errorlevel%
