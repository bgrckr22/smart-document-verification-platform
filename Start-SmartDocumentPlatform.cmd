@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-Application.ps1"
if errorlevel 1 (
    echo.
    echo Application startup failed. Review the message above and any open service windows.
    pause
    exit /b 1
)
