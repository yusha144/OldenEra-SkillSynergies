$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repo 'installer/Installer.Core.ps1')
. (Join-Path $repo 'installer/Dependencies.ps1')
$testRoot = Join-Path $repo ('development/artifacts/installer-tests/Проверка ! ' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$script:checks = 0
function Put([string]$Path, [string]$Text) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($true))
}
function Check([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw "FAILED: $Message" }
    $script:checks++
}
function Reject([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Check $rejected $Message
}
function Fingerprint([string]$Root) {
    return (@(Get-ChildItem -LiteralPath $Root -Recurse -File | Sort-Object FullName | ForEach-Object {
        $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }) -join '|')
}
$steam = Join-Path $testRoot 'Steam'
$library = Join-Path $testRoot 'Другая библиотека'
$game = Join-Path $library 'steamapps/common/Игра !'
Put (Join-Path $game 'HeroesOldenEra.exe') 'fixture exe'
Put (Join-Path $game 'HeroesOldenEra_Data/StreamingAssets/Core.zip') 'fixture core'
Put (Join-Path $game 'GameAssembly.dll') 'fixture game'
Put (Join-Path $library 'steamapps/appmanifest_3105440.acf') '"AppState" { "installdir" "Игра !" }'
Put (Join-Path $steam 'steamapps/libraryfolders.vdf') ('"libraryfolders" { "0" { "path" "' + $library.Replace('\','\\') + '" } }')
$found = @(Find-GamePaths @($steam))
Check ($found.Count -eq 1 -and $found[0] -eq $game) 'Steam discovery: alternate library, Cyrillic, spaces, exclamation'
$package = Join-Path $testRoot 'Архив !'
$payload = Join-Path $package '_files/payload'
$fixtureFiles = @(
    @{path='winhttp.dll';kind='Loader';text='fixture loader'},
    @{path='BepInEx/plugins/SkillSynergies/OldenEra.SkillSynergies.dll';kind='Mod';text='fixture new mod'},
    @{path='BepInEx/config/BepInEx.cfg';kind='Config';text='fixture default config'}
)
$entries = foreach ($entry in $fixtureFiles) {
    $path = Join-Path $payload $entry.path
    Put $path $entry.text
    @{path=$entry.path;kind=$entry.kind;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
}
$fixtureManifest = @{version='fixture';steamBuild='fixture';files=@($entries);gameFiles=@(@{path='GameAssembly.dll';sha256=(Get-FileHash -LiteralPath (Join-Path $game 'GameAssembly.dll') -Algorithm SHA256).Hash});downloads=@()}
Put (Join-Path $package '_files/manifest.json') ($fixtureManifest | ConvertTo-Json -Depth 5)
$manifest = Read-PackManifest $package
Assert-GameBuild $game $manifest
$plan = @(Get-InstallPlan $package $game $manifest)
Check ($plan.Count -eq 3) 'fresh install plan'
$backup = Invoke-PackInstall $package $game $manifest $plan
Check (@(Get-InstallPlan $package $game $manifest).Count -eq 0) 'repeat installation is empty'
$canonical = Join-Path $game 'BepInEx/plugins/SkillSynergies/OldenEra.SkillSynergies.dll'
$duplicate = Join-Path $game 'BepInEx/plugins/OldenEra.SkillSynergies.dll'
$preserved = @('BepInEx/plugins/MapHints/OldenEra.MapHints.dll','BepInEx/plugins/RetaliationHints/OldenEra.RetaliationHints.dll','BepInEx/plugins/OtherMod/OtherMod.dll','BepInEx/config/local.oldenera.maphints.cfg','BepInEx/config/local.oldenera.retaliationhints.cfg','BepInEx/config/OldenEra.hotkeys.cfg','BepInEx/config/yusha144.hotkeys.cfg','BepInEx/config/local.oldenera.skillsynergies.cfg','BepInEx/config/BepInEx.cfg')
foreach ($path in $preserved) { Put (Join-Path $game $path) ('untouched ' + $path) }
Put $canonical 'old mod'
Put $duplicate 'old duplicate'
$plan = @(Get-InstallPlan $package $game $manifest)
$backup = Invoke-PackInstall $package $game $manifest $plan
Check ([IO.File]::ReadAllText((Join-Path $backup 'BepInEx/plugins/SkillSynergies/OldenEra.SkillSynergies.dll')) -eq 'old mod') 'old DLL backed up'
Check (!(Test-Path -LiteralPath $duplicate)) 'duplicate retired'
Check ([IO.File]::ReadAllText((Join-Path $backup 'BepInEx/plugins/OldenEra.SkillSynergies.dll')) -eq 'old duplicate') 'duplicate backed up'
foreach ($path in $preserved) { Check ([IO.File]::ReadAllText((Join-Path $game $path)) -eq ('untouched ' + $path)) ('preserved during update: ' + $path) }
Reject { Join-PackPath $game '../outside.dll' } 'parent traversal'
Reject { Join-PackPath $game 'C:/outside.dll' } 'rooted path'
Put (Join-Path $game 'GameAssembly.dll') 'changed game'
Reject { Assert-GameBuild $game $manifest } 'unsupported game'
Put (Join-Path $game 'GameAssembly.dll') 'fixture game'
Put (Join-Path $game 'winhttp.dll') 'foreign loader'
Reject { Get-InstallPlan $package $game $manifest } 'foreign loader'
Check ([IO.File]::ReadAllText((Join-Path $game 'winhttp.dll')) -eq 'foreign loader') 'foreign loader unchanged'
Put (Join-Path $game 'winhttp.dll') 'fixture loader'
Put (Join-Path $game 'doorstop_config.ini') "[General]`nenabled = false"
Reject { Get-InstallPlan $package $game $manifest } 'disabled loader'
Remove-Item -LiteralPath (Join-Path $game 'doorstop_config.ini')
Put (Join-Path $payload 'winhttp.dll') 'damaged'
Reject { Read-PackManifest $package } 'corrupted payload'
Put (Join-Path $payload 'winhttp.dll') 'fixture loader'
# A failure after a replacement must restore both old and absent files.
Put $canonical 'before failure'
$failurePlan = @(
    [pscustomobject]@{Path='BepInEx/core/fresh.dll';Source=(Join-Path $payload 'winhttp.dll');Remove=$false},
    [pscustomobject]@{Path='BepInEx/plugins/SkillSynergies/OldenEra.SkillSynergies.dll';Source=(Join-Path $payload 'winhttp.dll');Remove=$false}
)
$originalWriter = (Get-Item Function:Write-PackFile).ScriptBlock
$script:writeCount = 0
function Write-PackFile([string]$Source, [string]$Destination) {
    & $originalWriter $Source $Destination
    $script:writeCount++
    if ($script:writeCount -eq 2) { throw 'injected copy failure' }
}
Reject { Invoke-PackInstall $package $game $manifest $failurePlan } 'copy failure propagated'
Set-Item Function:Write-PackFile $originalWriter
Check ([IO.File]::ReadAllText($canonical) -eq 'before failure') 'rollback restores old file'
Check (!(Test-Path -LiteralPath (Join-Path $game 'BepInEx/core/fresh.dll'))) 'rollback removes new file'
# Verification is read only, including when a real game process is running.
$before = Fingerprint $game
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'installer/Install.ps1') -PackageRoot $package -GamePath $game -VerifyOnly -NonInteractive
Check ($LASTEXITCODE -eq 0) 'VerifyOnly entry point'
Check ((Fingerprint $game) -eq $before) 'VerifyOnly leaves game unchanged'
Put $duplicate 'duplicate for removal'
Put (Join-Path $game 'GameAssembly.dll') 'updated game'
$uninstall = @(Get-UninstallPlan $game)
Check ($uninstall.Count -eq 2) 'uninstall selects only SkillSynergies copies even after game update'
Invoke-PackUninstall $game $uninstall
Check (@(Get-UninstallPlan $game).Count -eq 0) 'repeat uninstall is empty'
foreach ($path in $preserved) { Check ([IO.File]::ReadAllText((Join-Path $game $path)) -eq ('untouched ' + $path)) ('preserved during uninstall: ' + $path) }
$purge = @(Get-UninstallPlan $game $true)
Check ($purge.Count -eq 1 -and $purge[0].Path -eq 'BepInEx/config/local.oldenera.skillsynergies.cfg') 'optional settings removal is SkillSynergies only'
Invoke-PackUninstall $game $purge
Check (Test-Path -LiteralPath (Join-Path $game 'winhttp.dll')) 'shared loader retained'
# Exercise download extraction and checksum failure with a local network substitute.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$downloadFiles = Join-Path $testRoot 'download-source'
Put (Join-Path $downloadFiles 'winhttp.dll') 'downloaded loader'
$downloadZip = Join-Path $testRoot 'download.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($downloadFiles, $downloadZip)
$dep = [pscustomobject]@{id='loader';kind='Archive';url='https://builds.bepinex.dev/fixture.zip';sha256=(Get-FileHash $downloadZip).Hash}
function Invoke-WebRequest { param($Uri,$OutFile,$TimeoutSec,[switch]$UseBasicParsing) Copy-Item -LiteralPath $downloadZip -Destination $OutFile }
$entry = [pscustomobject]@{path='winhttp.dll';kind='Loader';sha256=(Get-FileHash (Join-Path $downloadFiles 'winhttp.dll')).Hash}
$downloadManifest = [pscustomobject]@{downloads=@($dep);files=@($entry)}
$downloadPlan = @([pscustomobject]@{Path='winhttp.dll';Source=(Join-Path $testRoot 'missing.dll');Remove=$false})
$staging = Join-Path $testRoot 'staging'
[IO.Directory]::CreateDirectory($staging) | Out-Null
Resolve-InstallSources $downloadManifest $downloadPlan $staging
Check ([IO.File]::ReadAllText($downloadPlan[0].Source) -eq 'downloaded loader') 'verified dependency extraction'
$dep.sha256 = '0' * 64
Reject { Save-VerifiedDownload $dep (Join-Path $staging 'bad.zip') } 'download checksum mismatch'
$dep.url = 'http://example.com/wrong.zip'
Reject { Save-VerifiedDownload $dep (Join-Path $staging 'unsafe.zip') } 'unexpected dependency origin'
Remove-Item Function:Invoke-WebRequest
# Запускаем настоящие BAT в искусственных папках, без обращения к установленной игре.
function Run-Launcher([string]$Path) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $env:ComSpec
    $start.Arguments = '/d /v:off /s /c ""' + $Path + '""'
    $start.WorkingDirectory = $testRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $consoleEncoding = [Text.Encoding]::GetEncoding([Globalization.CultureInfo]::CurrentCulture.TextInfo.OEMCodePage)
    $start.StandardOutputEncoding = $consoleEncoding
    $start.StandardErrorEncoding = $consoleEncoding
    $process = [Diagnostics.Process]::Start($start)
    try {
        $outputTask = $process.StandardOutput.ReadToEndAsync()
        $errorTask = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.WriteLine()
        $process.StandardInput.Close()
        if (!$process.WaitForExit(15000)) {
            $process.Kill()
            throw 'BAT не завершился за 15 секунд.'
        }
        return [pscustomobject]@{ExitCode=$process.ExitCode; Output=$outputTask.Result; Error=$errorTask.Result}
    } finally {
        $process.Dispose()
    }
}
$sourceLaunchers = Join-Path $testRoot 'Яшины моды ! & (исходники)/OldenEra-SkillSynergies-main/installer'
[IO.Directory]::CreateDirectory($sourceLaunchers) | Out-Null
# Справка должна работать даже без остальных сценариев и файлов установщика.
Copy-Item -LiteralPath (Join-Path $repo 'installer/Install.ps1') -Destination $sourceLaunchers
foreach ($launcher in @('Install.bat', 'Uninstall.bat')) {
    Copy-Item -LiteralPath (Join-Path $repo ('installer/' + $launcher)) -Destination $sourceLaunchers
    $sourceResult = Run-Launcher (Join-Path $sourceLaunchers $launcher)
    Check ($sourceResult.ExitCode -eq 1) ($launcher + ': исходники не запускают установку или удаление')
    Check ($sourceResult.Output.Contains('Это архив исходников SkillSynergies.')) ($launcher + ': понятное сообщение на русском')
    Check ($sourceResult.Output.Contains('https://github.com/yusha144/OldenEra-SkillSynergies/releases/latest')) ($launcher + ': ссылка на готовый выпуск')
    Check ($sourceResult.Output.Contains('запустите ' + $launcher + ' из корня')) ($launcher + ': справка для выбранного действия')
    Check ([string]::IsNullOrWhiteSpace($sourceResult.Error)) ($launcher + ': нет ошибки отсутствующего сценария')
}
# В готовом архиве выбирается сценарий из _files, сохраняются путь, действие и код завершения.
$launcherPackage = Join-Path $testRoot 'Яшины моды ! & (установщик)'
Put (Join-Path $launcherPackage '_files/Install.ps1') @'
param([string]$PackageRoot, [switch]$Uninstall)
Write-Host ('PackageRoot=' + [IO.Path]::GetFullPath($PackageRoot))
Write-Host ('Uninstall=' + $Uninstall.IsPresent)
exit 17
'@
Copy-Item -LiteralPath (Join-Path $repo 'installer/Install.ps1') -Destination $launcherPackage
foreach ($launcher in @('Install.bat', 'Uninstall.bat')) {
    Copy-Item -LiteralPath (Join-Path $repo ('installer/' + $launcher)) -Destination $launcherPackage
    $packageResult = Run-Launcher (Join-Path $launcherPackage $launcher)
    Check ($packageResult.ExitCode -eq 17) ($launcher + ': сохраняется код завершения установщика')
    Check ($packageResult.Output.Contains('PackageRoot=' + $launcherPackage)) ($launcher + ': путь с кириллицей, пробелами и специальными символами')
    Check ($packageResult.Output.Contains('Uninstall=' + ($launcher -eq 'Uninstall.bat'))) ($launcher + ': сохраняется действие')
}
Write-Host "Installer checks passed: $script:checks"
