@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1"
if errorlevel 1 (pause & exit /b 1)
start "" "%~dp0build\Dw3.Launcher.exe"
