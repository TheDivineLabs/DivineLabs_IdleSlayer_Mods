param([string]$Portable='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.2\portable',
 [string]$Output='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.2\Checks')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$index=Get-Content -LiteralPath "$root\src\Runtime\Resources\package-index.json" -Raw|ConvertFrom-Json
$scratch=Join-Path 'W:\DEV\.cache\temp' ('DivineLabs-public-components-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$scratch\App",$Output|Out-Null
Copy-Item -LiteralPath "$Portable\DivineLabs.IdleSlayer.exe" -Destination $scratch
Copy-Item -LiteralPath "$Portable\App\Launcher" -Destination "$scratch\App\Launcher" -Recurse
if(Test-Path -LiteralPath "$scratch\App\Components"){throw 'Cold-download fixture contains bundled components.'}
$info=[Diagnostics.ProcessStartInfo]::new("$scratch\DivineLabs.IdleSlayer.exe");$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.ArgumentList.Add('--verify-package')
$process=[Diagnostics.Process]::Start($info)
try{
 $deadline=[DateTime]::UtcNow.AddMinutes(10)
 while(-not $process.WaitForExit(1000)){if([DateTime]::UtcNow-gt $deadline){throw 'Public component download timed out.'}}
 if($process.ExitCode-ne 0){throw "Cold download failed. See $scratch\Data\Logs\launcher.log"}
}finally{$process.Dispose()}
$download=Join-Path "$scratch\Data\Cache\Downloads" ($index.version+'-components.zip')
if((Get-FileHash -LiteralPath $download).Hash-ne $index.sha256){throw 'Downloaded archive SHA256 mismatch.'}
if(-not(Test-Path -LiteralPath "$scratch\Data\Cache\Components\$($index.version)\manifest.json")){throw 'Verified component cache not created.'}
@{Url=$index.url;Version=$index.version;Sha256=$index.sha256;AnonymousDownload=$true;BundledComponentsAbsent=$true;CacheInitiallyAbsent=$true;VerifiedByRootExe=$true;Fixture=$scratch}|ConvertTo-Json|Set-Content -LiteralPath "$Output\public-components-check.json"
Write-Output 'Public component download and package verification passed.'
