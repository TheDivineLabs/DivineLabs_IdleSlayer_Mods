param([string]$Setup='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.0\DivineLabs Idle Slayer Mods.exe')
$ErrorActionPreference='Stop'
$work=Join-Path 'W:\DEV\.cache\temp' ('DivineLabs-progress-check-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work|Out-Null
$target=Join-Path $work 'installation'
$report=Join-Path $work 'operation.txt'
function StartWorker([string]$File,[string[]]$Arguments){
 $start=[Diagnostics.ProcessStartInfo]::new($File);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
 return [Diagnostics.Process]::Start($start)
}
$oldStage=Join-Path $target ('Data\Cache\Work\setup-'+[Guid]::NewGuid().ToString('N')+'\App\Components')
New-Item -ItemType Directory -Path $oldStage,(Join-Path $target 'Data\Logs') -Force|Out-Null
Set-Content -LiteralPath (Join-Path $oldStage 'manifest.json') -Value '{"Files":{"Mods/DivineLabs.IdleSlayer.Mod.dll":"fixture"}}'
Set-Content -LiteralPath (Join-Path $target 'Data\Logs\setup.log') -Value 'at DivineLabs.IdleSlayer.Setup.SetupOperations.Install'
$arguments=@('--silent','--no-integrations','--no-launch','--dir',$target,'--report',$report)
[IO.File]::WriteAllText($report,('0'+[Environment]::NewLine+'Исходный статус'))
$held=[IO.FileStream]::new($report,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try{
 $process=StartWorker $Setup $arguments
 $process.WaitForExit()
 if($process.ExitCode-ne 0){throw "Установка с занятым статусом завершилась с кодом $($process.ExitCode). $target\Data\Logs\setup.log"}
}finally{$held.Dispose()}
if(-not(Test-Path -LiteralPath "$target\App\Setup\installation.json")){throw 'Нет результата установки'}
Write-Output 'ПРОЙДЕНО: реальный установщик восстановил старую прерванную установку при заблокированном файле прогресса'
$preserved=Join-Path $target 'Data\Config\acceptance.txt';Set-Content -LiteralPath $preserved -Value 'retained'
$process=StartWorker $Setup $arguments
$samples=0
while(-not $process.HasExited){
 try{
  $stream=[IO.FileStream]::new($report,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite-bor[IO.FileShare]::Delete))
  $reader=[IO.StreamReader]::new($stream)
  try{$number=$reader.ReadLine();$message=$reader.ReadLine();if($null-ne $message){$samples++}}finally{$reader.Dispose()}
 }catch [IO.IOException]{}
 Start-Sleep -Milliseconds 10
}
$process.WaitForExit()
if($process.ExitCode-ne 0-or $samples-eq 0){throw 'Обновление с опросом прогресса не прошло'}
if((Get-Content -LiteralPath $preserved)-ne 'retained'){throw 'Настройки потеряны при обновлении'}
Write-Output "ПРОЙДЕНО: реальное обновление с частым чтением прогресса, получено $samples статусов"
$worker=Join-Path $work 'uninstall-worker.exe'
Copy-Item -LiteralPath "$target\App\Setup\DivineLabs.Uninstall.exe" -Destination $worker
$process=StartWorker $worker @('--silent','--uninstall-now','--files-only','--dir',$target,'--report',$report)
$process.WaitForExit()
if($process.ExitCode-ne 0-or (Test-Path -LiteralPath "$target\DivineLabs.IdleSlayer.exe")){throw 'Удаление вернуло неверный результат'}
if((Get-Content -LiteralPath $preserved)-ne 'retained'){throw 'Настройки потеряны при удалении'}
Write-Output "ПРОЙДЕНО: удаление завершено до кода успеха, пользовательские данные сохранены. $work"
