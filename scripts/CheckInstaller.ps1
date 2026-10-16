param(
 [string]$Setup='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.0\DivineLabs Idle Slayer Mods.exe',
 [string]$LegacySetup='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.2.0\DivineLabs-IdleSlayer-0.2.0-Setup.exe'
)
$ErrorActionPreference='Stop'
$target=Join-Path 'W:\DEV\.cache\temp' ('DivineLabs-installer-check-'+[Guid]::NewGuid().ToString('N'))
function RunApp([string]$File,[string[]]$Arguments){
 $info=[Diagnostics.ProcessStartInfo]::new($File);$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 foreach($argument in $Arguments){$info.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($info);$process.WaitForExit()
 if($process.ExitCode -ne 0){throw "Операция завершилась с кодом $($process.ExitCode); журнал: $target\Data\Logs\setup.log"}
}
function InstallTest{RunApp $Setup @('--silent','--no-integrations','--no-launch','--dir',$target)}
function CheckRoot{
 $unexpected=Get-ChildItem -LiteralPath $target|Where-Object Name -notin @('DivineLabs.IdleSlayer.exe','App','Data','Docs','user.txt')
 if($unexpected){throw "Лишние файлы в корне: $($unexpected.Name -join ', ')"}
 if(Get-ChildItem "$target\App","$target\Docs" -Recurse -File -Filter '*.pdb'){throw 'В поставке остались PDB'}
 if(-not(Test-Path "$target\App\Setup\DivineLabs.Uninstall.exe")){throw 'Неверный путь удаления'}
}
if(Test-Path $LegacySetup){RunApp $LegacySetup @('--silent','--no-integrations','--no-launch','--dir',$target)}
else {InstallTest}
New-Item -ItemType Directory -Force "$target\Data","$target\payload\MelonLoader"|Out-Null
Set-Content "$target\Data\setting.txt" 'retained'
Set-Content "$target\Data\game-path.txt" 'legacy-path'
Set-Content "$target\Data\launcher.log" 'legacy-log'
Set-Content "$target\user.txt" 'retained'
Set-Content "$target\payload\MelonLoader\legacy.txt" 'legacy'
InstallTest
CheckRoot
if(-not(Get-ChildItem "$target\Data\Backups\Application" -Recurse -File -Filter legacy.txt)){throw 'Старый payload не сохранён'}
RunApp "$target\DivineLabs.IdleSlayer.exe" @('--verify-package')
if((Get-Content "$target\Data\Config\game-path.txt") -ne 'legacy-path' -or (Get-Content "$target\Data\Logs\launcher.log") -ne 'legacy-log'){throw 'Старые данные не перенесены'}
if(Test-Path "$target\App\Launcher\Data"){throw 'Data создана внутри Launcher'}
InstallTest
CheckRoot
RunApp "$target\App\Setup\DivineLabs.Uninstall.exe" @('--uninstall','--files-only','--silent')
for($i=0;$i -lt 40 -and (Test-Path "$target\DivineLabs.IdleSlayer.exe");$i++){Start-Sleep -Milliseconds 500}
if(Test-Path "$target\DivineLabs.IdleSlayer.exe"){throw 'Удаление не завершилось'}
if(-not(Test-Path "$target\Data\Config\.divinelabs-owner")){throw 'Утрачена принадлежность папки после удаления'}
InstallTest
CheckRoot
if((Get-Content "$target\Data\setting.txt") -ne 'retained' -or (Get-Content "$target\user.txt") -ne 'retained'){throw 'Данные не сохранились'}
Write-Output "ПРОЙДЕНО: миграция 0.2.0, чистый корень, запуск EXE, обновление, удаление и повторная установка; данные сохранены. $target"