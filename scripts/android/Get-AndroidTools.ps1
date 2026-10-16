param([string]$Cache='W:\DEV\.cache\downloads\divinelabs-idleslayer-android')
$ErrorActionPreference='Stop'
if(-not [IO.Path]::GetFullPath($Cache).StartsWith('W:\DEV\',[StringComparison]::OrdinalIgnoreCase)){throw 'Кэш Android должен находиться под W:\DEV.'}
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$lock=Get-Content -LiteralPath (Join-Path $repo 'android\dependencies.lock.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $Cache | Out-Null
foreach($name in @('loader','patcher')){
 $entry=$lock.$name;$zip=Join-Path $Cache "$name-$($entry.version).zip"
 if(!(Test-Path -LiteralPath $zip)){
  $part="$zip.part";Invoke-WebRequest -Uri $entry.url -OutFile $part
  if((Get-FileHash -LiteralPath $part).Hash -ne $entry.sha256){throw "Неверная контрольная сумма: $name"}
  Move-Item -LiteralPath $part -Destination $zip
 }
 if((Get-FileHash -LiteralPath $zip).Hash -ne $entry.sha256){throw "Повреждён компонент: $name"}
 # Use a fresh verified extraction instead of trusting a previously edited tool directory.
 $directory=Join-Path $Cache ($name+'-'+$entry.version+'-'+[Guid]::NewGuid().ToString('N'))
 Expand-Archive -LiteralPath $zip -DestinationPath $directory
 if($name -eq 'loader'){$loader=$directory;$loaderZip=$zip}else{$patcher=Join-Path $directory 'LemonLoader.Patcher.CLI.exe'}
}
[pscustomobject]@{LoaderRoot=$loader;LoaderZip=$loaderZip;LoaderManaged=Join-Path $loader 'assets\LemonLoader\runtime\loader\net6';Patcher=$patcher;LoaderLicense=Join-Path $loader 'LICENSE.md';LoaderNotices=Join-Path $loader 'NOTICE.txt'}
