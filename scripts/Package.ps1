param([string]$Portable='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.4.2\portable',[string]$Destination='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.4.2\DivineLabs-IdleSlayer-0.4.2-portable.zip')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Portable)
if(Test-Path -LiteralPath $Destination){throw 'Архив уже существует: выберите новое имя.'}
$files=Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $relative=[IO.Path]::GetRelativePath($root,$_.FullName);$relative -notmatch '^Data[\\/]' }
foreach($file in $files){if($file.Extension -eq '.pdb'){throw 'Отладочные символы не входят в пользовательский архив'};if($file.Name -match 'Assembly-CSharp|IdleSlayerMods.Common|AcceptanceProbe|ResourcesProbe|\.sav$' -or $file.FullName -match '[\\/]Il2CppAssemblies[\\/]'){throw "Запрещённый файл дистрибутива: $($file.Name)"}}
$archive=[IO.Compression.ZipFile]::Open($Destination,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($file in $files){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,[IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose()}
Get-FileHash -LiteralPath $Destination -Algorithm SHA256
