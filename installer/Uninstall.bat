@echo off
setlocal DisableDelayedExpansion
title Uninstall Olden Era SkillSynergies
if not exist "%~dp0_files\Install.ps1" if exist "%~dp0Install.ps1" goto source
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0_files\Install.ps1" -PackageRoot "%~dp0." -Uninstall
goto completed
:source
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" -SourceArchive -Uninstall
:completed
set "result=%errorlevel%"
echo.
pause
exit /b %result%
