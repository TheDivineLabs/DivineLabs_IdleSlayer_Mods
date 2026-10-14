param([string]$Archive='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.3.0\DivineLabs-IdleSlayer-0.3.0-portable.zip')
$ErrorActionPreference='Stop'
$workspace=Join-Path 'W:\DEV\.cache\temp' ('DivineLabs-portable-check-'+[Guid]::NewGuid().ToString('N'))
$target=Join-Path $workspace 'Portable до перемещения'
Expand-Archive -LiteralPath $Archive -DestinationPath $target
$root=Get-ChildItem -LiteralPath $target
if(($root.Name|Sort-Object) -join ',' -ne 'App,DivineLabs.IdleSlayer.exe,Docs'){throw 'Неверный состав корня portable'}
if(Get-ChildItem -LiteralPath $target -Recurse -File -Filter '*.pdb'){throw 'В архиве остались PDB'}
function Verify{
 $info=[Diagnostics.ProcessStartInfo]::new((Join-Path $target 'DivineLabs.IdleSlayer.exe'));$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WorkingDirectory=$workspace;$info.ArgumentList.Add('--verify-package')
 $process=[Diagnostics.Process]::Start($info);$process.WaitForExit()
 if($process.ExitCode -ne 0){throw "Не работает корневой EXE: $($process.ExitCode)"}
 if(Test-Path "$target\App\Launcher\Data"){throw 'Data создана внутри Launcher'}
 foreach($name in @('Config','Cache','Logs','Backups')){if(-not(Test-Path "$target\Data\$name")){throw "Нет Data/$name"}}
}
Verify
$moved=Join-Path $workspace 'Portable после перемещения'
$resolved=[IO.Path]::GetFullPath($target)
if(-not $resolved.StartsWith([IO.Path]::GetFullPath($workspace)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Неверная папка проверки'}
Move-Item -LiteralPath $resolved -Destination $moved
$target=$moved
Verify
$info=[Diagnostics.ProcessStartInfo]::new((Join-Path $target 'DivineLabs.IdleSlayer.exe'));$info.UseShellExecute=$false;$info.CreateNoWindow=$true
foreach($argument in @('--prepare','--game',(Join-Path $workspace 'missing game'))){$info.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($info);$process.WaitForExit()
if($process.ExitCode -ne 2 -or -not(Test-Path "$target\Data\Logs\launcher.log")){throw 'Код ошибки или журнал не возвращаются через корневой EXE'}
Write-Output "ПРОЙДЕНО: состав ZIP, запуск, перенос portable с пробелами и кириллицей, корень Data и возврат ошибок. $workspace"