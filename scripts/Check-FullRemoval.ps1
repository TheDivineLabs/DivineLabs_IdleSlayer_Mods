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
foreach($kind in @('copy','main','silent-root')){
 $folder=Join-Path $scratch $kind
 Run $Setup @('--silent','--no-launch','--no-integrations','--dir',$folder)
 foreach($file in @('Data\Config\settings.json','Data\Backups\Application\old.bin','Data\Backups\Saves\save.sav','Data\Cache\Downloads\cached.zip','Data\Logs\old.log')){
  $path=Join-Path $folder $file;New-Item -ItemType Directory -Force (Split-Path $path)|Out-Null;[IO.File]::WriteAllText($path,'test data to remove')
 }
 if($kind-eq 'copy'){[IO.File]::WriteAllText("$folder\personal.txt",'keep outside Data')}
 $report=Join-Path $Output ("full-removal-$kind.txt")
 if($kind-eq 'copy'){Run $Setup @('--remove-copy','--silent','--remove-data','--dir',$folder,'--report',$report)}
 elseif($kind-eq 'main'){Run $Setup @('--uninstall-now','--files-only','--silent','--remove-data','--dir',$folder,'--report',$report)}
 else{
  # Launch the installed EXE itself: it must detach outside the folder it removes.
  Run "$folder\DivineLabs.Uninstall.exe" @('--silent','--files-only','--remove-data','--report',$report)
  $deadline=[DateTime]::UtcNow.AddSeconds(60)
  while([DateTime]::UtcNow-lt $deadline){
   if((Test-Path -LiteralPath $report)-and (Get-Content -LiteralPath $report -Raw)-match '^100\r?\n'){break}
   Start-Sleep -Milliseconds 200
  }
  if(-not(Test-Path -LiteralPath $report)-or (Get-Content -LiteralPath $report -Raw)-notmatch '^100\r?\n'){throw 'Detached removal did not report success'}
 }
 if(Test-Path -LiteralPath "$folder\Data"){throw "Full removal retained Data: $kind"}
 if(Test-Path -LiteralPath "$folder\DivineLabs.Uninstall.exe"){throw "Full removal retained uninstaller: $kind"}
 if($kind-eq 'copy'){
  if([IO.File]::ReadAllText("$folder\personal.txt")-ne 'keep outside Data'){throw 'Unrelated file was lost'}
 }elseif(Test-Path -LiteralPath $folder){throw "Empty app folder retained: $kind"}
}
@{NativeFullRemoval=@('copy','main','silent-root');DataDeleted=$true;EmptyRootDeleted=$true;UnrelatedFilesRetained=$true;NoGameStarted=$true;Fixture=$scratch}|ConvertTo-Json|Set-Content -LiteralPath "$Output\full-removal-checks.json"
Write-Output 'Full removal passed: copy, main worker and installed detached uninstaller.'
