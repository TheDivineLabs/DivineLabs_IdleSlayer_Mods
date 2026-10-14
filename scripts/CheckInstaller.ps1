param([string]$Setup='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.2.0\DivineLabs-IdleSlayer-0.2.0-Setup.exe')
$ErrorActionPreference='Stop'
$target=Join-Path 'W:\DEV\.cache\temp' ('DivineLabs-installer-check-'+[Guid]::NewGuid().ToString('N'))
function RunApp([string]$File,[string[]]$Arguments){$info=[Diagnostics.ProcessStartInfo]::new($File);$info.UseShellExecute=$false;$info.CreateNoWindow=$true;foreach($argument in $Arguments){$info.ArgumentList.Add($argument)};$process=[Diagnostics.Process]::Start($info);$process.WaitForExit();if($process.ExitCode -ne 0){throw "Операция завершилась с кодом $($process.ExitCode); журнал: $target\Data\setup.log"}}
function InstallTest{RunApp $Setup @('--silent','--no-integrations','--no-launch','--dir',$target)}
InstallTest
New-Item -ItemType Directory -Force "$target\Data","$target\payload\MelonLoader"|Out-Null
Set-Content "$target\Data\setting.txt" 'retained';Set-Content "$target\user.txt" 'retained';Set-Content "$target\payload\MelonLoader\legacy.txt" 'legacy'
InstallTest
if(Test-Path "$target\payload\MelonLoader"){throw 'Старый payload остался активным'}
if(-not(Get-ChildItem "$target\.application-history" -Recurse -File -Filter legacy.txt)){throw 'Старый payload не сохранён'}
RunApp "$target\DivineLabs.Uninstall.exe" @('--uninstall','--files-only','--silent')
for($i=0;$i -lt 40 -and (Test-Path "$target\DivineLabs.IdleSlayer.exe");$i++){Start-Sleep -Milliseconds 500}
if(Test-Path "$target\DivineLabs.IdleSlayer.exe"){throw 'Удаление не завершилось'}
if(-not(Test-Path "$target\.divinelabs-owner")){throw 'Утрачена принадлежность папки после удаления'}
InstallTest
if((Get-Content "$target\Data\setting.txt") -ne 'retained' -or (Get-Content "$target\user.txt") -ne 'retained'){throw 'Данные не сохранились'}
Write-Output "ПРОЙДЕНО: установка, обновление, архивирование MelonLoader, удаление и повторная установка; данные сохранены. $target"
