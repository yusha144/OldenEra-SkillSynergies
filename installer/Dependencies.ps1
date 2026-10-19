# Downloads are prepared outside the game before any installation writes.
function Save-VerifiedDownload($Dependency, [string]$Destination) {
    $uri = [Uri]$Dependency.url
    if ($uri.Scheme -ne 'https' -or $uri.Host -notin @('builds.bepinex.dev','unity.bepinex.dev')) {
        throw 'Неожиданный адрес зависимости.'
    }
    Write-Host ('Загрузка: ' + $uri.Host)
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $uri -OutFile $Destination -TimeoutSec 120
    if ((Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash -ne $Dependency.sha256) {
        throw 'Контрольная сумма загруженного файла не совпала. Игра не изменена.'
    }
}

function Resolve-InstallSources($Manifest, [object[]]$Plan, [string]$TemporaryRoot) {
    $missing = @($Plan | Where-Object { !$_.Remove -and !(Test-Path -LiteralPath $_.Source -PathType Leaf) })
    if ($missing.Count) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        foreach ($dependency in $Manifest.downloads) {
            $needed = @($missing | Where-Object {
                if ($dependency.kind -eq 'Archive') { $_.Path -notlike 'BepInEx/unity-libs/*' }
                else { $_.Path -eq $dependency.path }
            })
            if (!$needed.Count) { continue }
            $download = Join-PackPath $TemporaryRoot ($dependency.id + '.zip')
            Save-VerifiedDownload $dependency $download
            if ($dependency.kind -eq 'Archive') {
                $zip = [IO.Compression.ZipFile]::OpenRead($download)
                try {
                    foreach ($item in $needed) {
                        $entry = $zip.GetEntry($item.Path)
                        if (!$entry) { throw ('В загрузчике отсутствует файл: ' + $item.Path) }
                        $destination = Join-PackPath $TemporaryRoot ('payload/' + $item.Path)
                        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
                        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $false)
                        $item.Source = $destination
                    }
                } finally { $zip.Dispose() }
            } elseif ($dependency.kind -eq 'File') {
                foreach ($item in $needed) { $item.Source = $download }
            } else { throw 'Неизвестный тип зависимости.' }
        }
    }
    foreach ($item in $Plan | Where-Object { !$_.Remove }) {
        $expected = @($Manifest.files | Where-Object path -eq $item.Path)
        if ($expected.Count -ne 1 -or !(Test-Path -LiteralPath $item.Source -PathType Leaf) -or
            (Get-FileHash -LiteralPath $item.Source -Algorithm SHA256).Hash -ne $expected[0].sha256) {
            throw ('Не удалось подготовить файл: ' + $item.Path)
        }
    }
}
