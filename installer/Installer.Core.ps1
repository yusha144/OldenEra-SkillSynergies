Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Join-PackPath([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or
        $Relative.Contains(':') -or @($Relative -split '[/\\]' | Where-Object { $_ -in @('..','.') }).Count) {
        throw "Недопустимый путь в сборке: $Relative"
    }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $path = [IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (!$path.StartsWith($base + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Путь выходит за пределы папки.' }
    $cursor = $path
    while ($cursor.Length -ge $base.Length) {
        if (Test-Path -LiteralPath $cursor) {
            if (((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Символическая ссылка в пути: $cursor. Укажите настоящую папку игры."
            }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
        if (!$cursor) { break }
    }
    return $path
}

function Get-VdfValue([string]$Text, [string]$Key) {
    $match = [regex]::Match($Text, '"' + [regex]::Escape($Key) + '"\s*"((?:\\.|[^"\\])*)"')
    if ($match.Success) { return $match.Groups[1].Value.Replace('\\','\').Replace('\"','"') }
    return $null
}

function Get-SteamRoots {
    $roots = [Collections.Generic.List[string]]::new()
    foreach ($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam','HKLM:\SOFTWARE\Valve\Steam')) {
        if (Test-Path -LiteralPath $key) {
            $data = Get-ItemProperty -LiteralPath $key
            foreach ($name in @('SteamPath','InstallPath')) {
                $property = $data.PSObject.Properties[$name]
                if ($property -and $property.Value) { $roots.Add([string]$property.Value) }
            }
        }
    }
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($base) { $roots.Add((Join-Path $base 'Steam')) }
    }
    return @($roots | Select-Object -Unique)
}

function Find-GamePaths([string[]]$SteamRoots) {
    $libraries = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($root in $SteamRoots) {
        if (!(Test-Path -LiteralPath $root -PathType Container)) { continue }
        [void]$libraries.Add([IO.Path]::GetFullPath($root))
        foreach ($relative in @('steamapps\libraryfolders.vdf','config\libraryfolders.vdf')) {
            $vdf = Join-Path $root $relative
            if (!(Test-Path -LiteralPath $vdf -PathType Leaf)) { continue }
            $text = [IO.File]::ReadAllText($vdf)
            foreach ($match in [regex]::Matches($text, '"(?:path|\d+)"\s*"((?:\\.|[^"\\])*)"')) {
                $candidate = $match.Groups[1].Value.Replace('\\','\')
                if ([IO.Path]::IsPathRooted($candidate) -and (Test-Path -LiteralPath $candidate -PathType Container)) {
                    [void]$libraries.Add([IO.Path]::GetFullPath($candidate))
                }
            }
        }
    }
    $games = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($library in $libraries) {
        $common = Join-Path $library 'steamapps\common'
        $names = [Collections.Generic.List[string]]::new()
        $names.Add('Heroes of Might and Magic Olden Era')
        $acf = Join-Path $library 'steamapps\appmanifest_3105440.acf'
        if (Test-Path -LiteralPath $acf) {
            $name = Get-VdfValue ([IO.File]::ReadAllText($acf)) 'installdir'
            if ($name -and [IO.Path]::GetFileName($name) -eq $name) { $names.Add($name) }
        }
        foreach ($name in $names) {
            $path = Join-PackPath $common $name
            if (Test-Path -LiteralPath (Join-Path $path 'HeroesOldenEra.exe') -PathType Leaf) { [void]$games.Add($path) }
        }
    }
    return @($games | Sort-Object)
}

function Read-PackManifest([string]$PackageRoot) {
    $manifest = Get-Content -Raw -LiteralPath (Join-PackPath $PackageRoot '_files\manifest.json') | ConvertFrom-Json
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $manifest.files) {
        if (!$seen.Add($entry.path)) { throw 'Повторяющийся файл в сборке.' }
        $destinationAllowed = ($entry.kind -eq 'Loader' -and ($entry.path -match '^(BepInEx/(core|unity-libs)/|dotnet/)' -or $entry.path -in @('winhttp.dll','.doorstop_version'))) -or
            ($entry.kind -eq 'Config' -and $entry.path -in @('doorstop_config.ini','BepInEx/config/BepInEx.cfg')) -or
            ($entry.kind -eq 'Mod' -and $entry.path -eq 'BepInEx/plugins/SkillSynergies/OldenEra.SkillSynergies.dll')
        if (!$destinationAllowed -or $entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw "Неожиданный файл сборки: $($entry.path)" }
        $source = Join-PackPath (Join-Path $PackageRoot '_files\payload') $entry.path
        if ($entry.kind -eq 'Loader' -and !(Test-Path -LiteralPath $source)) { continue }
        if (!(Test-Path -LiteralPath $source -PathType Leaf) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.sha256) {
            throw "Архив повреждён или распакован не полностью: $($entry.path)"
        }
    }
    return $manifest
}

function Assert-GameBuild([string]$GamePath, $Manifest) {
    foreach ($relative in @('HeroesOldenEra.exe','HeroesOldenEra_Data\StreamingAssets\Core.zip')) {
        if (!(Test-Path -LiteralPath (Join-PackPath $GamePath $relative) -PathType Leaf)) { throw 'Это не папка установленной Olden Era.' }
    }
    foreach ($entry in $Manifest.gameFiles) {
        $path = Join-PackPath $GamePath $entry.path
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) {
            throw "Версия игры не подходит. SkillSynergies рассчитан на Steam build $($Manifest.steamBuild). Файлы игры не изменены."
        }
    }
}

function Get-InstallPlan([string]$PackageRoot, [string]$GamePath, $Manifest) {
    $items = [Collections.Generic.List[object]]::new()
    foreach ($entry in $Manifest.files) {
        $destination = Join-PackPath $GamePath $entry.path
        $exists = Test-Path -LiteralPath $destination -PathType Leaf
        if ($entry.kind -eq 'Config' -and $exists) { continue }
        if ($exists) {
            $current = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
            if ($current -eq $entry.sha256) { continue }
            if ($entry.kind -eq 'Loader') {
                throw "Обнаружен другой или изменённый загрузчик: $($entry.path). Он не будет заменён автоматически."
            }
        }
        $source = Join-PackPath (Join-Path $PackageRoot '_files\payload') $entry.path
        $items.Add([pscustomobject]@{ Path=$entry.path; Source=$source; Destination=$destination; Remove=$false })
    }
    # Retire duplicate copies of our exact DLL names outside their canonical folders.
    # Every removed duplicate is backed up outside the plugins tree first.
    $plugins = Join-PackPath $GamePath 'BepInEx\plugins'
    if (Test-Path -LiteralPath $plugins) {
        $children = @(Get-ChildItem -LiteralPath $plugins -Recurse -Force)
        if (@($children | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count) { throw 'Ссылки внутри plugins: установка остановлена без изменений.' }
        foreach ($entry in $Manifest.files | Where-Object { $_.kind -eq 'Mod' }) {
            $canonical = Join-PackPath $GamePath $entry.path
            foreach ($file in $children | Where-Object { !$_.PSIsContainer -and $_.Name -eq [IO.Path]::GetFileName($canonical) -and $_.FullName -ne $canonical }) {
                $relative = $file.FullName.Substring($GamePath.TrimEnd('\').Length + 1)
                $items.Add([pscustomobject]@{Path=$relative;Source=$null;Destination=(Join-PackPath $GamePath $relative);Remove=$true})
            }
        }
    }
    # A foreign/disabled Doorstop configuration must not lead to a false success.
    $doorstop = Join-PackPath $GamePath 'doorstop_config.ini'
    if (Test-Path -LiteralPath $doorstop) {
        $text = Get-Content -Raw -LiteralPath $doorstop
        if ($text -notmatch '(?im)^\s*enabled\s*=\s*true\s*$' -or $text -notmatch '(?im)^\s*target_assembly\s*=\s*BepInEx[\\/]core[\\/]BepInEx\.Unity\.IL2CPP\.dll\s*$') {
            throw 'Существующий doorstop_config.ini настроен иначе. Сначала согласуйте загрузчик; установка ничего не заменила.'
        }
    }
    return $items.ToArray()
}

function Write-PackFile([string]$Source, [string]$Destination) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Destination)) | Out-Null
    $temporary = $Destination + '.skillsynergies-' + [Guid]::NewGuid().ToString('N')
    try {
        [IO.File]::Copy($Source, $temporary, $false)
        if ([IO.File]::Exists($Destination)) { [IO.File]::Replace($temporary, $Destination, [NullString]::Value) }
        else { [IO.File]::Move($temporary, $Destination) }
    } finally {
        if ([IO.File]::Exists($temporary)) { Remove-Item -LiteralPath $temporary -Force }
    }
}

function Get-UninstallPlan([string]$GamePath, [bool]$RemoveSettings = $false) {
    $plugins = Join-PackPath $GamePath 'BepInEx\plugins'
    $names = @('OldenEra.SkillSynergies.dll')
    if (Test-Path -LiteralPath $plugins -PathType Container) {
        $children = @(Get-ChildItem -LiteralPath $plugins -Recurse -Force)
        if (@($children | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count) { throw 'Ссылки внутри plugins: удаление остановлено без изменений.' }
        foreach ($file in $children | Where-Object { !$_.PSIsContainer -and $_.Name -in $names }) {
            $relative = $file.FullName.Substring($GamePath.TrimEnd('\').Length + 1)
            [pscustomobject]@{Path=$relative;Destination=(Join-PackPath $GamePath $relative)}
        }
    }
    foreach ($name in @('local.oldenera.skillsynergies.cfg') | Where-Object { $RemoveSettings }) {
        $relative = 'BepInEx/config/' + $name
        $path = Join-PackPath $GamePath $relative
        if (Test-Path -LiteralPath $path -PathType Leaf) { [pscustomobject]@{Path=$relative;Destination=$path} }
    }
}

function Invoke-PackUninstall([string]$GamePath, [object[]]$Plan) {
    $failures = [Collections.Generic.List[string]]::new()
    foreach ($item in $Plan) {
        try { Remove-Item -LiteralPath (Join-PackPath $GamePath $item.Path) -Force -ErrorAction Stop }
        catch { $failures.Add($item.Path + ': ' + $_.Exception.Message) }
    }
    if ($failures.Count) { throw ('Не все файлы удалены. Закройте игру, проверьте доступ и повторите удаление. Остались: ' + ($failures -join '; ')) }
    # Delete only our empty leaf folders; never recurse or touch another plugin.
    foreach ($name in @('SkillSynergies')) {
        $folder = Join-PackPath $GamePath ('BepInEx/plugins/' + $name)
        if ((Test-Path -LiteralPath $folder -PathType Container) -and @(Get-ChildItem -LiteralPath $folder -Force).Count -eq 0) {
            [IO.Directory]::Delete($folder, $false)
        }
    }
}

function Invoke-PackInstall([string]$PackageRoot, [string]$GamePath, $Manifest, [object[]]$Plan) {
    $backupRelative = 'BepInEx\SkillSynergies-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
    $backup = Join-PackPath $GamePath $backupRelative
    [IO.Directory]::CreateDirectory($backup) | Out-Null
    $records = [Collections.Generic.List[object]]::new()
    $backupPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($item in $Plan) { [void]$backupPaths.Add($item.Path.Replace('\','/')) }
    foreach ($path in @('BepInEx/config/local.oldenera.skillsynergies.cfg',
        'BepInEx/config/OldenEra.hotkeys.cfg','BepInEx/config/yusha144.hotkeys.cfg')) { [void]$backupPaths.Add($path) }
    foreach ($path in $backupPaths) {
        $source = Join-PackPath $GamePath $path
        $existed = Test-Path -LiteralPath $source -PathType Leaf
        $hash = $null
        if ($existed) {
            $saved = Join-PackPath $backup $path
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
            Copy-Item -LiteralPath $source -Destination $saved
            $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
            if ((Get-FileHash -LiteralPath $saved -Algorithm SHA256).Hash -ne $hash) { throw 'Ошибка проверки резервной копии. Установка остановлена.' }
        }
        $records.Add([pscustomobject]@{path=$path;existed=$existed;sha256=$hash})
    }
    [ordered]@{version=$Manifest.version;gamePath=$GamePath;files=$records.ToArray()} | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $backup 'backup.json') -Encoding UTF8
    $applied = [Collections.Generic.List[object]]::new()
    try {
        [IO.Directory]::CreateDirectory((Join-PackPath $GamePath 'BepInEx/patchers')) | Out-Null
        foreach ($item in $Plan) {
            $destination = Join-PackPath $GamePath $item.Path
            $applied.Add($item)
            if ($item.Remove) { Remove-Item -LiteralPath $destination -Force }
            else {
                Write-PackFile $item.Source $destination
                if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $item.Source -Algorithm SHA256).Hash) {
                    throw "Проверка скопированного файла не прошла: $($item.Path)"
                }
            }
        }
    } catch {
        $failure = $_.Exception.Message
        $rollbackErrors = [Collections.Generic.List[string]]::new()
        for ($i=$applied.Count-1; $i -ge 0; $i--) {
            $item = $applied[$i]
            try {
                $destination = Join-PackPath $GamePath $item.Path
                $saved = Join-PackPath $backup $item.Path
                if (Test-Path -LiteralPath $saved -PathType Leaf) { Copy-Item -LiteralPath $saved -Destination $destination -Force }
                elseif (Test-Path -LiteralPath $destination -PathType Leaf) { Remove-Item -LiteralPath $destination -Force }
            } catch { $rollbackErrors.Add($item.Path) }
        }
        if ($rollbackErrors.Count) { throw "Ошибка: $failure. Нужен ручной откат файлов: $($rollbackErrors -join ', '). Копия: $backup" }
        throw "Ошибка: $failure. Заменённые файлы восстановлены. Копия: $backup"
    }
    return $backup
}
