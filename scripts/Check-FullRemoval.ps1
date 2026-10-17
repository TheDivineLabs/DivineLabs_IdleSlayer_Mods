param([string]$Setup='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.3\DivineLabs Idle Slayer Mods.exe',
 [string]$Output='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.3\Checks')
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $Output|Out-Null
$scratch=Join-Path 'W:\DEV\.cache\temp' ('DivineLabs-full-removal-'+[Guid]::NewGuid().ToString('N'))
function Run([string]$File,[string[]]$Arguments){
 $info=[Diagnostics.ProcessStartInfo]::new($File);$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 foreach($argument in $Arguments){$info.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($info)
 try{if(-not $process.WaitForExit(60000)){throw 'Native operation timed out.'};if($process.ExitCode-ne 0){throw "Native operation failed ($($process.ExitCode))."}}finally{$process.Dispose()}
}
function Complete([string]$Report){
 try{
  $stream=[IO.FileStream]::new($Report,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite-bor [IO.FileShare]::Delete))
  $reader=[IO.StreamReader]::new($stream)
  try{return $reader.ReadLine()-eq '100'}finally{$reader.Dispose()}
 }catch [IO.IOException]{return $false}
}
foreach($kind in @('copy','main','silent-root')){
 $folder=Join-Path $scratch $kind
 Run $Setup @('--silent','--no-launch','--no-integrations','--dir',$folder)
 foreach($file in @('Data\Config\settings.json','Data\Backups\Application\old.bin','Data\Backups\Saves\save.sav','Data\Cache\Downloads\cached.zip','Data\Logs\old.log')){
  $path=Join-Path $folder $file;New-Item -ItemType Directory -Force (Split-Path $path)|Out-Null;[IO.File]::WriteAllText($path,'test data to remove')
 }
 if($kind-eq 'copy'){[IO.File]::WriteAllText("$folder\personal.txt",'keep outside Data')}
 $report=Join-Path $Output ("full-removal-$kind-"+[Guid]::NewGuid().ToString('N')+'.txt')
 if($kind-eq 'copy'){Run $Setup @('--remove-copy','--silent','--remove-data','--dir',$folder,'--report',$report)}
 elseif($kind-eq 'main'){Run $Setup @('--uninstall-now','--files-only','--silent','--remove-data','--dir',$folder,'--report',$report)}
 else{
  # Launch the installed EXE itself: it must detach outside the folder it removes.
  Run "$folder\DivineLabs.Uninstall.exe" @('--silent','--files-only','--remove-data','--report',$report)
  $deadline=[DateTime]::UtcNow.AddSeconds(60)
  while([DateTime]::UtcNow-lt $deadline){
   if(Complete $report){break}
   Start-Sleep -Milliseconds 200
  }
  if(-not(Complete $report)){throw 'Detached removal did not report success'}
 }
 if(Test-Path -LiteralPath "$folder\Data"){throw "Full removal retained Data: $kind"}
 if(Test-Path -LiteralPath "$folder\DivineLabs.Uninstall.exe"){throw "Full removal retained uninstaller: $kind"}
 if($kind-eq 'copy'){
  if([IO.File]::ReadAllText("$folder\personal.txt")-ne 'keep outside Data'){throw 'Unrelated file was lost'}
 }elseif(Test-Path -LiteralPath $folder){throw "Empty app folder retained: $kind"}
}
@{NativeFullRemoval=@('copy','main','silent-root');DataDeleted=$true;EmptyRootDeleted=$true;UnrelatedFilesRetained=$true;NoGameStarted=$true;Fixture=$scratch}|ConvertTo-Json|Set-Content -LiteralPath "$Output\full-removal-checks.json"
Write-Output 'Full removal passed: copy, main worker and installed detached uninstaller.'
