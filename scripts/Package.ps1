param([string]$Portable='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\portable',[string]$Destination='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\DivineLabs-IdleSlayer-0.1.0-win-x64.zip')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Portable)
if(Test-Path -LiteralPath $Destination){throw 'Архив уже существует: выберите новое имя.'}
$files=Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $relative=[IO.Path]::GetRelativePath($root,$_.FullName);$relative -notmatch '^Data[\\/]' }
foreach($file in $files){if($file.Name -match 'Assembly-CSharp|IdleSlayerMods.Common|AcceptanceProbe|\.sav$' -or $file.FullName -match '[\\/]Il2CppAssemblies[\\/]'){throw "Запрещённый файл дистрибутива: $($file.Name)"}}
$archive=[IO.Compression.ZipFile]::Open($Destination,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($file in $files){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,[IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose()}
Get-FileHash -LiteralPath $Destination -Algorithm SHA256
