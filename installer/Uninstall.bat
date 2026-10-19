@echo off
setlocal DisableDelayedExpansion
title Uninstall Olden Era SkillSynergies
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0_files\Install.ps1" -PackageRoot "%~dp0." -Uninstall
set "result=%errorlevel%"
echo.
pause
exit /b %result%
