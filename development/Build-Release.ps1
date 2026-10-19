[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GameRoot,
    [string]$LoaderArchive,
    [string]$UnityLibrariesArchive,
    [string]$Revision = 'HEAD',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts/releases')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Run-Native {
    param([string]$Program, [string[]]$Arguments)
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw ($Program + ' failed: ' + $LASTEXITCODE) }
}
$repo = Split-Path -Parent $PSScriptRoot
$commit = & git -C $repo rev-parse --verify --end-of-options ($Revision + '^{commit}')
if ($LASTEXITCODE -ne 0) { throw 'Revision is not a commit.' }
$commit = $commit.Trim()
$expectedScript = & git -C $repo rev-parse ($commit + ':development/Build-Release.ps1')
if ($LASTEXITCODE -ne 0) { throw 'The selected commit does not contain development/Build-Release.ps1.' }
$actualScript = & git -C $repo hash-object --path=development/Build-Release.ps1 $PSCommandPath
if ($LASTEXITCODE -ne 0 -or $actualScript -ne $expectedScript) {
    throw 'Use the unchanged development/Build-Release.ps1 from the selected commit.'
}
$game = (Resolve-Path -LiteralPath $GameRoot).Path
$work = Join-Path $PSScriptRoot ('artifacts\release-' + [guid]::NewGuid().ToString('N'))
$snapshot = Join-Path $work 'source'
[IO.Directory]::CreateDirectory($snapshot) | Out-Null
$sourceZip = Join-Path $work 'source.zip'
Run-Native 'git' @('-C', $repo, 'archive', '--format=zip', ('--output=' + $sourceZip), $commit)
[IO.Compression.ZipFile]::ExtractToDirectory($sourceZip, $snapshot)
$settings = Get-Content -LiteralPath (Join-Path $snapshot 'development/release-settings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($entry in $settings.gameFiles) {
    $file = Join-Path $game $entry.path
    if (!(Test-Path -LiteralPath $file) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw ('Unsupported game file: ' + $entry.path)
    }
}
$core = Join-Path $game 'HeroesOldenEra_Data\StreamingAssets\Core.zip'
if (!(Test-Path -LiteralPath $core)) { throw 'Core.zip is missing.' }
$project = Join-Path $snapshot $settings.project
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Invalid project version.' }
$assembly = [string]$projectXml.Project.PropertyGroup.AssemblyName
$framework = [string]$projectXml.Project.PropertyGroup.TargetFramework
$name = $settings.name + '-' + $version
$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
$archive = Join-Path $output ($name + '.zip')
$checksum = $archive + '.sha256'
$record = Join-Path $output ($name + '.release.json')
foreach ($path in @($archive, $checksum, $record)) {
    if (Test-Path -LiteralPath $path) { throw ('Output exists; it will not be replaced: ' + $path) }
}
Write-Host ('Building committed snapshot: ' + $commit)
Write-Host 'Uncommitted files are not included.'
Run-Native 'powershell.exe' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $snapshot 'development/tests/Installer.Tests.ps1'))
Run-Native 'dotnet' @('build', $project, '-c', 'Release', ('-p:GameRoot=' + $game), '--nologo')
$dll = Join-Path (Split-Path -Parent $project) ('bin\Release\' + $framework + '\' + $assembly + '.dll')
if (!(Test-Path -LiteralPath $dll)) { throw 'The mod DLL was not produced.' }
$dllVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion.Split('+')[0]
if ($dllVersion -ne $version) { throw 'DLL version does not match the project.' }
Run-Native 'dotnet' @('run', '--project', (Join-Path $snapshot $settings.tests), '-c', 'Release', ('-p:GameRoot=' + $game), '--', $core, $dll)
$package = Join-Path $work 'package'
$payload = Join-Path $package '_files\payload'
$pluginFolder = Join-Path $payload ('BepInEx\plugins\' + $settings.mod)
[IO.Directory]::CreateDirectory($pluginFolder) | Out-Null
Copy-Item -LiteralPath $dll -Destination $pluginFolder
Copy-Item -LiteralPath (Join-Path $snapshot 'README.md') -Destination $package
Copy-Item -LiteralPath (Join-Path $snapshot 'docs') -Destination $package -Recurse
foreach ($file in @('Install.bat','Uninstall.bat')) {
    $text = [IO.File]::ReadAllText((Join-Path $snapshot ('installer/' + $file))) -replace '\r?\n', "`r`n"
    [IO.File]::WriteAllText((Join-Path $package $file), $text, [Text.Encoding]::ASCII)
}
foreach ($file in @('Install.ps1','Installer.Core.ps1','Dependencies.ps1')) {
    [IO.File]::WriteAllText((Join-Path $package ('_files/' + $file)),
        [IO.File]::ReadAllText((Join-Path $snapshot ('installer/' + $file))), [Text.UTF8Encoding]::new($true))
}
[IO.Directory]::CreateDirectory((Join-Path $payload 'BepInEx/config')) | Out-Null
Copy-Item -LiteralPath (Join-Path $snapshot 'installer/BepInEx.cfg') -Destination (Join-Path $payload 'BepInEx/config/BepInEx.cfg')
Copy-Item -LiteralPath (Join-Path $snapshot 'installer/doorstop_config.ini') -Destination $payload
# Dependency bytes are used to derive checksums, but are not redistributed in the ZIP.
. (Join-Path $snapshot 'installer/Installer.Core.ps1')
. (Join-Path $snapshot 'installer/Dependencies.ps1')
$dependencyRoot = Join-Path $work 'dependencies'
[IO.Directory]::CreateDirectory($dependencyRoot) | Out-Null
$dependencyFiles = [Collections.Generic.List[object]]::new()
foreach ($dependency in $settings.downloads) {
    $local = $UnityLibrariesArchive
    if ($dependency.kind -eq 'Archive') { $local = $LoaderArchive }
    if (!$local) {
        $local = Join-Path $dependencyRoot ($dependency.id + '.zip')
        Save-VerifiedDownload $dependency $local
    }
    if ((Get-FileHash -LiteralPath $local -Algorithm SHA256).Hash -ne $dependency.sha256) { throw 'Dependency archive hash mismatch.' }
    if ($dependency.kind -eq 'File') {
        $dependencyFiles.Add([ordered]@{path=$dependency.path;kind='Loader';sha256=$dependency.sha256})
        continue
    }
    $zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($local))
    try {
        foreach ($entry in $zip.Entries) {
            if (!$entry.Name -or $entry.FullName -in @('changelog.txt','doorstop_config.ini')) { continue }
            if ($entry.FullName -notmatch '^(BepInEx/core/|dotnet/|winhttp\.dll$|\.doorstop_version$)') { throw 'Unexpected loader entry.' }
            $target = Join-PackPath $dependencyRoot ('extracted/' + $entry.FullName)
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
            $dependencyFiles.Add([ordered]@{path=$entry.FullName;kind='Loader';sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash})
        }
    } finally { $zip.Dispose() }
}
$payloadFiles = @(Get-ChildItem -LiteralPath $payload -File -Recurse | ForEach-Object {
    $path = $_.FullName.Substring($payload.Length + 1).Replace('\','/')
    $kind = 'Config'
    if ($path.StartsWith('BepInEx/plugins/')) { $kind = 'Mod' }
    [ordered]@{path=$path;kind=$kind;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
$installerManifest = [ordered]@{
    name=$settings.name;version=$version;commit=$commit;steamBuild=$settings.steamBuild
    gameFiles=$settings.gameFiles;downloads=$settings.downloads;files=@($payloadFiles) + @($dependencyFiles.ToArray())
}
[IO.File]::WriteAllText((Join-Path $package '_files/manifest.json'), ($installerManifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $snapshot 'LICENSE') -Destination $package
$utf8 = [Text.UTF8Encoding]::new($false)
$files = @(Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = $_.FullName.Substring($package.Length + 1).Replace('\', '/')
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
})
$manifest = [ordered]@{
    name = $settings.name
    version = $version
    commit = $commit
    tag = 'v' + $version
    steamBuild = $settings.steamBuild
    loader = $settings.loader
    validation = 'Existing automated checks and Release build passed. Gameplay is not checked by this script.'
    files = $files
}
[IO.File]::WriteAllText((Join-Path $package 'release-manifest.json'), ($manifest | ConvertTo-Json -Depth 8), $utf8)
$tempArchive = Join-Path $work ($name + '.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($package, $tempArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
[IO.File]::Copy($tempArchive, $archive, $false)
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
[IO.File]::WriteAllText($checksum, ($archiveHash + '  ' + [IO.Path]::GetFileName($archive) + [Environment]::NewLine), [Text.Encoding]::ASCII)
$release = [ordered]@{
    repository = $settings.repository
    name = $settings.name
    version = $version
    commit = $commit
    tag = 'v' + $version
    archive = [IO.Path]::GetFileName($archive)
    sha256 = $archiveHash
    gameplayVerified = $false
}
[IO.File]::WriteAllText($record, ($release | ConvertTo-Json -Depth 4), $utf8)
Write-Host ('Prepared locally: ' + $archive)
Write-Host ('SHA256: ' + $archiveHash)
Write-Host 'No tag, release, upload or game installation was performed.'
