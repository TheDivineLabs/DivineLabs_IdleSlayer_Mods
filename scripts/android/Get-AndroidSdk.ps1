#requires -Version 7.4
param(
 [string]$SdkRoot='W:\DEV\tools\android-sdk',
 [string]$Cache='W:\DEV\.cache\downloads\divinelabs-idleslayer-android'
)
$ErrorActionPreference='Stop'
$SdkRoot=[IO.Path]::GetFullPath($SdkRoot)
$Cache=[IO.Path]::GetFullPath($Cache)
foreach($path in @($SdkRoot,$Cache)){
 if(!$path.StartsWith('W:\DEV\',[StringComparison]::OrdinalIgnoreCase)){throw 'Инструменты и кэш должны находиться под W:\DEV.'}
}
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$lock=Get-Content -LiteralPath (Join-Path $repo 'android\dependencies.lock.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $SdkRoot,$Cache | Out-Null
foreach($name in @('platformTools','buildTools')){
 $entry=$lock.sdk.$name
 $zip=Join-Path $Cache ([Uri]$entry.url).Segments[-1]
 if(!(Test-Path -LiteralPath $zip)){
  $part=$zip+'.'+[Guid]::NewGuid().ToString('N')+'.part'
  Invoke-WebRequest -Uri $entry.url -OutFile $part
  if((Get-Item -LiteralPath $part).Length -ne $entry.size -or (Get-FileHash -LiteralPath $part).Hash -ne $entry.sha256){throw "Не совпала контрольная сумма SDK: $name"}
  Move-Item -LiteralPath $part -Destination $zip
 }
 if((Get-Item -LiteralPath $zip).Length -ne $entry.size -or (Get-FileHash -LiteralPath $zip).Hash -ne $entry.sha256){throw "Повреждён архив SDK: $name"}
 $destination=Join-Path $SdkRoot $entry.directory
 if(!(Test-Path -LiteralPath $destination)){
  $stage=Join-Path $SdkRoot ('.divinelabs-stage-'+[Guid]::NewGuid().ToString('N'))
  Expand-Archive -LiteralPath $zip -DestinationPath $stage
  $parent=Split-Path $destination -Parent
  New-Item -ItemType Directory -Force -Path $parent | Out-Null
  Move-Item -LiteralPath (Join-Path $stage $entry.archiveRoot) -Destination $destination
  # The staging directory is now empty. Never recursively delete an SDK directory.
  Remove-Item -LiteralPath $stage
 }
 # Reusing a directory must not silently execute altered tools or their dependencies.
 $archive=[IO.Compression.ZipFile]::OpenRead($zip)
 try{
  $prefix=$entry.archiveRoot+'/'
  foreach($file in $archive.Entries){
   if($file.FullName.EndsWith('/')){continue}
   if(!$file.FullName.StartsWith($prefix,[StringComparison]::Ordinal)){throw 'Неожиданная структура архива SDK.'}
   $relative=$file.FullName.Substring($prefix.Length)
   $target=[IO.Path]::GetFullPath((Join-Path $destination $relative))
   if(!$target.StartsWith($destination.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Путь SDK выходит за каталог.'}
   if(!(Test-Path -LiteralPath $target -PathType Leaf) -or (Get-Item -LiteralPath $target).Length -ne $file.Length){throw "Неполная установка SDK: $name. Существующие файлы не перезаписаны."}
   $inputStream=$file.Open();$diskStream=[IO.File]::OpenRead($target)
   try{
    $expected=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inputStream))
    $actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($diskStream))
    if($actual -ne $expected){throw "Изменён компонент SDK: $relative. Существующие файлы не перезаписаны."}
   }finally{$inputStream.Dispose();$diskStream.Dispose()}
  }
 }finally{$archive.Dispose()}
}
$docs=Join-Path $SdkRoot 'DivineLabs-Notices'
New-Item -ItemType Directory -Force -Path $docs | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'android\licenses\Android-SDK-License.txt') -Destination $docs
[pscustomobject]@{
 SdkRoot=$SdkRoot
 Adb=Join-Path $SdkRoot 'platform-tools\adb.exe'
 BuildTools=Join-Path $SdkRoot $lock.sdk.buildTools.directory
 License=Join-Path $docs 'Android-SDK-License.txt'
}
