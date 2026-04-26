@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish_steam_workshop.ps1" %*
exit /b %ERRORLEVEL%
