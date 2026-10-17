# Сборка и публикация Skill Synergies

## 1. Исходники и версия

Основная ветка: `main`. Репозиторий: [OldenEra-SkillSynergies](https://github.com/Devourer22/OldenEra-SkillSynergies). Существующая история и старые теги сохраняются. Теги `stable/*` обозначают сохранённые локальные состояния, а не новые публичные выпуски.

Сначала закончите изменения в отдельном моде и зафиксируйте только относящиеся к нему файлы. Номер новой версии задаётся в `source/SkillSynergies.csproj` и `source/Plugin.cs`; обновите описание и затронутую документацию. Не используйте уже опубликованный номер для другого содержимого.

Готовые DLL, архивы, журналы, игровые библиотеки, личные настройки и рабочие отчёты восстановления в новые коммиты не добавляются. Лицензию авторского кода выбирает владелец проекта.

## 2. Локальная сборка

Нужны Git, .NET SDK 8 или новее, среда .NET 8 для проверок и игра с подготовленными библиотеками `BepInEx/core` и `BepInEx/interop`. Путь к игре указывается явно. Пример для PowerShell из папки репозитория:

~~~powershell
.\Build-Release.ps1 -GameRoot 'D:\Games\Heroes of Might and Magic Olden Era' -Revision HEAD
~~~

Команда берёт точный коммит, создаёт отдельную копию исходников, проверяет игровые файлы, запускает существующие проверки и сборку Release. Незакоммиченные изменения не входят в архив. Если выбран более старый коммит, используйте неизменённый `Build-Release.ps1` именно из него.

Результат в `releases/`:

- `OldenEra-SkillSynergies-<версия>.zip`, отдельный мод и документация.
- `OldenEra-SkillSynergies-<версия>.zip.sha256`, контрольная сумма.
- `OldenEra-SkillSynergies-<версия>.release.json`, номер, коммит, тег и SHA-256 архива.

В архиве есть `release-manifest.json` с контрольными суммами включённых файлов. Повторная запись существующих файлов запрещена. Промежуточные файлы остаются в `work/`.

## 3. Проверка перед выпуском

Распакуйте подготовленный архив и проверьте установку и сценарий из [инструкции игрока](ops-admin.md) на совместимой игре. Зафиксируйте, что проверено и какие ограничения остались. Скрипт сам игру не запускает и всегда записывает `gameplayVerified: false`, потому что не может подтвердить ручную проверку.

Проверьте отсутствие игровых библиотек, загрузчика, конфигураций и журналов в ZIP. Уточните лицензию и сохраните необходимые сведения об авторстве. При любом изменении исходников создайте новый проверяемый коммит и пересоберите архив.

## 4. Публикация через GitHub CLI

Выполняется отдельно, после проверки архива. Команды ниже являются примером для выбранного файла `.release.json`; путь к нему и к подготовленному тексту выпуска нужно указать явно.

~~~powershell
$releaseFile = '<путь к файлу .release.json>'
$notesFile = '<путь к тексту выпуска>'
$r = Get-Content -LiteralPath $releaseFile -Raw | ConvertFrom-Json
$folder = Split-Path -Parent $releaseFile
$zip = Join-Path $folder $r.archive
$sum = $zip + '.sha256'
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $r.sha256) { throw 'Archive checksum mismatch' }
gh release list --repo $r.repository
git ls-remote --tags origin
~~~

Перед продолжением убедитесь, что такая версия ещё не опубликована. Если тег уже существует, сравните его коммит с `$r.commit`. Совпадающий тег можно использовать; несовпадающий тег нельзя передвигать. При существующем выпуске сначала проверьте его файлы и добавляйте только отсутствующие, без перезаписи.

Для новой версии без существующего тега:

~~~powershell
git tag -a $r.tag $r.commit -m $r.tag
git push origin main
git push origin $r.tag
gh release create $r.tag $zip $sum $releaseFile --repo $r.repository --verify-tag --draft --title ($r.name + ' ' + $r.version) --notes-file $notesFile
~~~

Каждую команду выполняйте только после успешного завершения предыдущей. Черновик позволяет проверить описание и файлы перед открытием скачивания. Для предварительного выпуска дополнительно укажите `--prerelease`.

В тексте укажите возможности, изменения, совместимую сборку игры, требования к загрузчику, установку, ограничения и фактические проверки. Не обозначайте неподтверждённый выпуск стабильным. Проверив черновик, опубликуйте его:

~~~powershell
gh release edit $r.tag --repo $r.repository --draft=false
gh release view $r.tag --repo $r.repository
~~~

[Справка создания выпуска](https://cli.github.com/manual/gh_release_create). Сценарий не требует автоматической сборки на GitHub.

## 5. Проверка скачивания

Скачайте ZIP из опубликованного выпуска в новую папку, затем сравните SHA-256 с `$r.sha256`. Дополнительно проверьте прямую ссылку без авторизации, поскольку репозиторий открытый.

~~~powershell
$download = Join-Path $folder ('download-' + [guid]::NewGuid().ToString('N'))
gh release download $r.tag --repo $r.repository --pattern $r.archive --dir $download
if ($LASTEXITCODE -ne 0) { throw 'Download failed' }
$downloaded = Join-Path $download $r.archive
if ((Get-FileHash -LiteralPath $downloaded -Algorithm SHA256).Hash -ne $r.sha256) { throw 'Downloaded checksum mismatch' }
~~~

[Справка скачивания](https://cli.github.com/manual/gh_release_download). Выпуск считается завершённым только после проверки опубликованных файлов. Не заменяйте старый архив другим содержимым и не передвигайте выпущенный тег.

[Главная](../README.md)
