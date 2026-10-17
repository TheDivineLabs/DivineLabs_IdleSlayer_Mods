param([string]$Setup='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.3\DivineLabs Idle Slayer Mods.exe',
 [string]$Output='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.3\Checks')
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $Output|Out-Null
$scratch=Join-Path 'W:\DEV\.cache\temp' ('DivineLabs-native-copy-check-'+[Guid]::NewGuid().ToString('N'))
$first=Join-Path $scratch 'kept';$second=Join-Path $scratch 'removed'
function Run([string]$File,[string[]]$Arguments){
 $info=[Diagnostics.ProcessStartInfo]::new($File);$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 foreach($argument in $Arguments){$info.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($info)
 try{if(-not $process.WaitForExit(60000)){throw 'Native operation timed out.'};if($process.ExitCode-ne 0){throw "Native operation failed ($($process.ExitCode))."}}finally{$process.Dispose()}
}
foreach($folder in @($first,$second)){
 Run $Setup @('--silent','--no-launch','--no-integrations','--dir',$folder,'--report',(Join-Path $Output ((Split-Path $folder -Leaf)+'-install.txt')))
 foreach($file in @('DivineLabs.IdleSlayer.exe','DivineLabs.Uninstall.exe','App\Setup\installation.json')){if(-not(Test-Path -LiteralPath (Join-Path $folder $file))){throw "Installation missing: $file"}}
 $receipt=Get-Content -LiteralPath "$folder\App\Setup\installation.json" -Raw|ConvertFrom-Json
 if($receipt.Version-ne '0.5.3'){throw 'Wrong installation version.'}
}
$canaries=@('Data\Config\settings.json','Data\Saves\progress.sav','Data\Backups\Application\old.bin','Data\Backups\Saves\save.sav','notes.txt','Docs\my-notes.txt')
foreach($relative in $canaries){$path=Join-Path $second $relative;New-Item -ItemType Directory -Force (Split-Path $path)|Out-Null;[IO.File]::WriteAllText($path,'retain')}
$keptBefore=(Get-FileHash -LiteralPath "$first\App\Launcher\DivineLabs.IdleSlayer.Launcher.exe").Hash
Run $Setup @('--remove-copy','--silent','--dir',$second,'--report',"$Output\native-copy-removal.txt")
if(Test-Path -LiteralPath "$second\DivineLabs.IdleSlayer.exe"){throw 'Selected root app remains.'}
if(Test-Path -LiteralPath "$second\DivineLabs.Uninstall.exe"){throw 'Selected uninstaller remains.'}
if(Test-Path -LiteralPath "$second\App\Setup\installation.json"){throw 'Receipt remains after removal.'}
foreach($relative in $canaries){if([IO.File]::ReadAllText((Join-Path $second $relative))-ne 'retain'){throw "Retained data changed: $relative"}}
if((Get-FileHash -LiteralPath "$first\App\Launcher\DivineLabs.IdleSlayer.Launcher.exe").Hash-ne $keptBefore){throw 'Kept copy changed.'}
# Self-contained launcher and native root EXE must verify the unchanged payload without external .NET.
$previousDotnetRoot=$env:DOTNET_ROOT;$previousMultilevel=$env:DOTNET_MULTILEVEL_LOOKUP
try{
 $env:DOTNET_ROOT=Join-Path $scratch 'no-external-dotnet';$env:DOTNET_MULTILEVEL_LOOKUP='0'
 Run "$first\DivineLabs.IdleSlayer.exe" @('--verify-package')
}finally{$env:DOTNET_ROOT=$previousDotnetRoot;$env:DOTNET_MULTILEVEL_LOOKUP=$previousMultilevel}
@{NativeInstallations=2;Removed=$second;Kept=$first;Retained=$canaries;KeptLauncherHashUnchanged=$true;SelfContainedPackageVerification=$true;NoGameStarted=$true}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath "$Output\native-copy-removal-checks.json"
Write-Output "Native copy removal checks passed: $Output"
