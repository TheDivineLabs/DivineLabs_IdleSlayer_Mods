param(
 [string]$Output='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\design-review-installer-0.5.0',
 [string]$Dotnet='W:\DEV\tools\dotnet\dotnet.exe',
 [switch]$UseExistingRenders
)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$full=[IO.Path]::GetFullPath($Output)
if(-not $full.StartsWith('W:\DEV\artifacts\',[StringComparison]::OrdinalIgnoreCase)){throw 'Комплект обзора должен находиться в W:\DEV\artifacts.'}
$zip=Join-Path (Split-Path $full -Parent) ([IO.Path]::GetFileName($full)+'.zip')
if(Test-Path -LiteralPath $zip){throw 'Архив уже существует. Выберите новую папку Output для следующего обзора.'}
New-Item -ItemType Directory -Force $full|Out-Null
$env:DOTNET_CLI_HOME='W:\DEV\.cache\dotnet-home';$env:NUGET_PACKAGES='W:\DEV\.cache\nuget';$env:TEMP='W:\DEV\.cache\temp';$env:TMP=$env:TEMP
$raw=Join-Path $full 'raw'
if(-not $UseExistingRenders){
 & $Dotnet run --project (Join-Path $root 'tests\Layout\Layout.csproj') -c Release -- $raw --maintenance-only
 if($LASTEXITCODE-ne 0){throw 'Не удалось снять экраны установщика.'}
}
$versionMatch=[regex]::Match([IO.File]::ReadAllText((Join-Path $root 'src\Runtime\Product.cs')),'Version = "([^"]+)"')
$version=$versionMatch.Groups[1].Value
$commit=(& git -c ('safe.directory='+$root.Replace('\','/')) -C $root rev-parse HEAD).Trim()
if($LASTEXITCODE-ne 0){throw 'Не удалось определить ревизию.'}
$screens=Join-Path $full 'Screens';$sources=Join-Path $full 'Sources';$assets=Join-Path $full 'Assets'
New-Item -ItemType Directory -Force $screens,$sources,$assets|Out-Null
$names=@('01-start','02-license','03-options','04-install','05-complete')
$files=[ordered]@{}
for($i=0;$i-lt 5;$i++){
 foreach($scale in @('1','2')){
  $suffix=if($scale-eq '2'){'@2x'}else{''}
  $target=Join-Path $screens ($names[$i]+$suffix+'.png')
  Copy-Item -LiteralPath (Join-Path $raw ('Installer-'+$i+'-'+$scale+'.png')) -Destination $target -Force
  $files['Screens/'+[IO.Path]::GetFileName($target)]=(Get-FileHash -LiteralPath $target).Hash
 }
}
foreach($relative in @('src\Launcher\MaintenanceWindow.xaml','src\Launcher\MaintenanceWindow.xaml.cs','src\DesktopUI\SetupTheme.xaml','src\DesktopUI\Motion.cs','src\DesktopUI\Theme.xaml','src\DesktopUI\SetupProgressRing.cs')){
 $file=Join-Path $root $relative
 if(Test-Path -LiteralPath $file){Copy-Item -LiteralPath $file -Destination $sources -Force;$files[$relative.Replace('\','/')]=(Get-FileHash -LiteralPath $file).Hash}
}
foreach($name in @('DivineLabs.png','DivineLabs.svg','DivineLabs.ico','Installer.cover.png','IdleSlayer.wordmark.png')){
 Copy-Item -LiteralPath (Join-Path $root ('assets\'+$name)) -Destination $assets -Force
}
$template=[IO.File]::ReadAllText((Join-Path $root 'docs\design\installer-review.html'))
[IO.File]::WriteAllText((Join-Path $full 'index.html'),$template.Replace('{{VERSION}}',$version).Replace('{{COMMIT}}',$commit),[Text.UTF8Encoding]::new($false))
@{SchemaVersion=1;Version=$version;SourceCommit=$commit;Capture='WPF RenderTargetBitmap';InstallationPerformed=$false;ProgressIsFixture=$true;Files=$files}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $full 'manifest.json')
$readme=@'
DivineLabs — обзор установщика
Откройте index.html в браузере. Стрелки влево/вправо переключают этапы.
«Отметить правку» — нажмите на место на снимке и введите комментарий.
Комментарии сохраняются только в этом браузере. Скачайте JSON для передачи.
Импорт JSON заменяет текущий список заметок — сначала экспортируйте нужные.
PNG доступны в 100% и 200%; Sources содержит редактируемые исходники XAML.
Это снимки интерфейса из исходников; прогресс 62% и завершение демонстрационные.
PNG сами по себе не являются редактируемыми слоями дизайна.
Никакой установки, загрузки игровых данных, отправки комментариев или платных сервисов.
Для обновления снимков: scripts/design/Export-InstallerReview.ps1 в репозитории.
'@
[IO.File]::WriteAllText((Join-Path $full 'README.ru.txt'),$readme,[Text.UTF8Encoding]::new($false))
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
 Get-ChildItem -LiteralPath $full -Recurse -File|Where-Object{[IO.Path]::GetRelativePath($full,$_.FullName)-notmatch '^raw[\\/]'}|ForEach-Object{
  [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,[IO.Path]::GetRelativePath($full,$_.FullName).Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal)|Out-Null
 }
}finally{$archive.Dispose()}
Write-Output ('Галерея: '+(Join-Path $full 'index.html'))
Write-Output ('Архив: '+$zip)
