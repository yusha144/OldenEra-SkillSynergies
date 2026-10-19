param([string]$PackageRoot, [string]$GamePath, [switch]$Uninstall, [switch]$RemoveSettings, [switch]$VerifyOnly, [switch]$NonInteractive)
$ErrorActionPreference = 'Stop'
$env:PSModulePath = $PSHOME + '\Modules;' + $env:PSModulePath
Import-Module ($PSHOME + '\Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
. (Join-Path $PSScriptRoot 'Installer.Core.ps1')
. (Join-Path $PSScriptRoot 'Dependencies.ps1')
$temporaryRoot = $null
$result = 0
try {
    Write-Host 'Olden Era SkillSynergies' -ForegroundColor Cyan
    if (!$PackageRoot) { $PackageRoot = Split-Path -Parent $PSScriptRoot }
    $PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
    if (!(Test-Path -LiteralPath (Join-Path $PackageRoot '_files\manifest.json')) -and !$Uninstall) {
        throw 'Это исходники установщика. Нужен готовый архив мода, а не Source code.'
    }
    if ($RemoveSettings -and !$Uninstall) { throw '-RemoveSettings применяется только с -Uninstall.' }
    if (!$GamePath) {
        $found = @(Find-GamePaths (Get-SteamRoots))
        if ($found.Count -eq 1) { $GamePath = $found[0] }
        elseif ($NonInteractive) { throw 'Укажите папку игры через -GamePath.' }
        elseif ($found.Count -gt 1) {
            for ($i=0; $i -lt $found.Count; $i++) { Write-Host "$($i+1). $($found[$i])" }
            $choice = Read-Host 'Номер нужной игры (Enter для отмены)'
            $index=0
            if (![int]::TryParse($choice,[ref]$index) -or $index -lt 1 -or $index -gt $found.Count) { throw 'Отменено.' }
            $GamePath = $found[$index-1]
        } else { $GamePath = (Read-Host 'Путь к папке с HeroesOldenEra.exe').Trim().Trim('"') }
    }
    if ([string]::IsNullOrWhiteSpace($GamePath)) { throw 'Отменено.' }
    $GamePath = [IO.Path]::GetFullPath($GamePath).TrimEnd('\')
    Write-Host "Папка игры: $GamePath"
    if (!$VerifyOnly -and (Get-Process -Name HeroesOldenEra -ErrorAction SilentlyContinue)) { throw 'Закройте игру и повторите.' }
    if ($Uninstall) {
        if (!(Test-Path -LiteralPath (Join-PackPath $GamePath 'HeroesOldenEra.exe') -PathType Leaf)) { throw 'Это не папка Olden Era.' }
        $plan = @(Get-UninstallPlan $GamePath ([bool]$RemoveSettings))
    } else {
        $manifest = Read-PackManifest $PackageRoot
        Assert-GameBuild $GamePath $manifest
        $plan = @(Get-InstallPlan $PackageRoot $GamePath $manifest)
    }
    if ($VerifyOnly) {
        Write-Host "Проверка пройдена. Файлов для изменения: $($plan.Count). Скачивания и изменения не выполнялись."
    } elseif (!$plan.Count) {
        Write-Host 'Ничего менять не нужно.' -ForegroundColor Green
    } else {
        if (!$NonInteractive) {
            $action = 'Установить SkillSynergies'
            if ($Uninstall) { $action = 'Удалить SkillSynergies' }
            if ($RemoveSettings) { $action += ' вместе с его настройками' }
            if ((Read-Host ($action + ' в указанной папке? Д/Н')) -notmatch '^(д|да|y|yes)$') { throw 'Отменено.' }
        }
        if ($Uninstall) {
            if (Get-Process -Name HeroesOldenEra -ErrorAction SilentlyContinue) { throw 'Закройте игру и повторите.' }
            Invoke-PackUninstall $GamePath $plan
            Write-Host 'SkillSynergies удалён. Другие моды, BepInEx и общие клавиши сохранены.' -ForegroundColor Green
            if (!$RemoveSettings) { Write-Host 'Личные настройки SkillSynergies также сохранены.' }
        } else {
            $temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('SkillSynergies-install-' + [Guid]::NewGuid().ToString('N'))
            [IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
            Resolve-InstallSources $manifest $plan $temporaryRoot
            # Repeat preflight after downloads to catch changed game/loader files.
            Assert-GameBuild $GamePath $manifest
            $rechecked = @(Get-InstallPlan $PackageRoot $GamePath $manifest)
            if (($rechecked.Path -join '|') -ne ($plan.Path -join '|')) { throw 'Файлы изменились во время подготовки. Повторите установку.' }
            if (Get-Process -Name HeroesOldenEra -ErrorAction SilentlyContinue) { throw 'Закройте игру и повторите.' }
            $backup = Invoke-PackInstall $PackageRoot $GamePath $manifest $plan
            Write-Host 'SkillSynergies установлен. Запустите игру через Steam.' -ForegroundColor Green
            Write-Host "Резервная копия: $backup"
            Write-Host 'Первый запуск с BepInEx может занять больше времени.'
        }
    }
} catch {
    Write-Host ('Ошибка: ' + $_.Exception.Message) -ForegroundColor Red
    $result = 1
} finally {
    if ($temporaryRoot -and (Test-Path -LiteralPath $temporaryRoot)) {
        $resolved = [IO.Path]::GetFullPath($temporaryRoot)
        $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if ($resolved.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
            [IO.Path]::GetFileName($resolved) -match '^SkillSynergies-install-[a-f0-9]{32}$') {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
exit $result
