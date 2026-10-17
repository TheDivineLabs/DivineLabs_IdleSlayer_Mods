param(
 [string]$Output='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\DivineLabs Idle Slayer Design',
 [string]$Dotnet='W:\DEV\tools\dotnet\dotnet.exe',
 [switch]$UseExistingRenders
)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$full=[IO.Path]::GetFullPath($Output)
if(-not $full.StartsWith('W:\DEV\artifacts\',[StringComparison]::OrdinalIgnoreCase)){throw 'Design deliverables must be under W:\DEV\artifacts.'}
$raw=Join-Path $full 'Renders'
if(-not $UseExistingRenders){
 $env:DOTNET_CLI_HOME='W:\DEV\.cache\dotnet-home';$env:NUGET_PACKAGES='W:\DEV\.cache\nuget';$env:TEMP='W:\DEV\.cache\temp';$env:TMP=$env:TEMP
 & $Dotnet run --project "$root\tests\Layout\Layout.csproj" -c Release -- $raw
 if($LASTEXITCODE-ne 0){throw 'Rendering failed.'}
}
$surfaces=@(
 @{Folder='01 Установщик';Prefix='Installer';Pages=@(0,1,2,3,4);Labels=@('01 Начало','02 Лицензии','03 Параметры','04 Установка','05 Готово')},
 @{Folder='03 Удалятор';Prefix='Uninstaller';Pages=@(0,2,3,4);Labels=@('01 Начало','02 Параметры','03 Удаление','04 Готово')}
)
foreach($surface in $surfaces){
 $surfaceRoot=Join-Path $full $surface.Folder
 foreach($folder in @('Screens\RU','Screens\EN','Editable\RU','Editable\EN')){New-Item -ItemType Directory -Force (Join-Path $surfaceRoot $folder)|Out-Null}
 for($i=0;$i-lt $surface.Pages.Count;$i++){
  $page=$surface.Pages[$i];$label=$surface.Labels[$i]
  foreach($scale in @('1','1.5','2')){Copy-Item -LiteralPath "$raw\$($surface.Prefix)-$page-$scale.png" -Destination "$surfaceRoot\Screens\RU\$label-$scale.png" -Force}
  Copy-Item -LiteralPath "$raw\$($surface.Prefix)-$page-en.png" -Destination "$surfaceRoot\Screens\EN\$label.png" -Force
  Copy-Item -LiteralPath "$raw\$($surface.Prefix)-$page-1.svg" -Destination "$surfaceRoot\Editable\RU\$label.svg" -Force
  Copy-Item -LiteralPath "$raw\$($surface.Prefix)-$page-en.svg" -Destination "$surfaceRoot\Editable\EN\$label.svg" -Force
 }
}
$launcher=Join-Path $full '02 Программа'
New-Item -ItemType Directory -Force "$launcher\Screens","$launcher\Editable"|Out-Null
$launcherNames=[ordered]@{HomePage='01 Главная';FeaturesPage='02 Функции';ResourcesPage='03 Ресурсы';SettingsPage='04 Настройки'}
foreach($entry in $launcherNames.GetEnumerator()){
 foreach($scale in @('1','1.5','2')){Copy-Item -LiteralPath "$raw\$($entry.Key)-$scale.png" -Destination "$launcher\Screens\$($entry.Value)-$scale.png" -Force}
 Copy-Item -LiteralPath "$raw\$($entry.Key)-1.svg" -Destination "$launcher\Editable\$($entry.Value).svg" -Force
}
Copy-Item -LiteralPath "$raw\Installer-cancel.png","$raw\Installer-options-small.png" -Destination "$full\01 Установщик\Screens\RU" -Force
$shared=Join-Path $full 'Shared'
New-Item -ItemType Directory -Force "$shared\Sources","$shared\Assets"|Out-Null
foreach($relative in @('src\DesktopUI\SetupTheme.xaml','src\DesktopUI\ApplicationTheme.xaml','src\DesktopUI\Motion.cs','src\Launcher\MaintenanceWindow.xaml','src\Launcher\MaintenanceWindow.xaml.cs','src\Launcher\MainWindow.xaml','src\Launcher\MainWindow.xaml.cs','src\Launcher\ResourceView.xaml','src\Launcher\LanguageMenu.xaml','src\Launcher\LanguageMenu.xaml.cs','src\Launcher\SocialLinks.xaml','src\Launcher\SocialLinks.xaml.cs','src\Launcher\SetupText.cs')){Copy-Item -LiteralPath (Join-Path $root $relative) -Destination "$shared\Sources" -Force}
foreach($name in @('DivineLabs.svg','DivineLabs.png','DivineLabs.ico','Installer.cover.png')){Copy-Item -LiteralPath "$root\assets\$name" -Destination "$shared\Assets" -Force}
Copy-Item -LiteralPath "$root\docs\design\catalog.json" -Destination "$full\catalog.json" -Force
Write-Output "Design library: $full"
