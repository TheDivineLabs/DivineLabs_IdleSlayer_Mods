param(
 [string]$GamePath='W:\steam\steamapps\common\Idle Slayer',
 [string]$Output='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.2.0',
 [string]$Dotnet='dotnet', [switch]$SkipLoader, [switch]$SkipInstaller,
 [string]$NativeBin='', [string]$NativeLib=''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fork=Join-Path $root 'DivineLabs_Loader'
$env:DOTNET_CLI_HOME='W:\DEV\.cache\dotnet-home';$env:NUGET_PACKAGES='W:\DEV\.cache\nuget\'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:TEMP='W:\DEV\.cache\temp';$env:TMP=$env:TEMP
function RunDotnet([string[]]$Arguments){ & $Dotnet @Arguments;if($LASTEXITCODE -ne 0){throw "Ошибка сборки: $($Arguments -join ' ')"} }
if($NativeBin){$env:PATH=$NativeBin+';'+$env:PATH;$env:LIB=$NativeLib}
New-Item -ItemType Directory -Force $Output | Out-Null
if(-not $SkipLoader){
 RunDotnet @('build',"$fork\MelonLoader\MelonLoader.csproj",'-c','Release','-p:TargetFrameworks=net6.0','-p:DotNetTarget=net6.0','-p:Platform=x64',"-p:SolutionDir=$fork\")
 $native=@('publish',"$fork\MelonLoader.Bootstrap\MelonLoader.Bootstrap.csproj",'-c','Release','-r','win-x64','-p:BootstrapTarget=net9.0','-p:Platform=x64','-p:SkipPublishAfterBuild=true',"-p:SolutionDir=$fork\",'-o',"$fork\Output\bootstrap")
 if($NativeBin){$native+='-p:IlcUseEnvironmentalTools=true'};RunDotnet $native
}
RunDotnet @('build',"$root\src\Mod\Mod.csproj",'-c','Release',"-p:GamePath=$GamePath")
$lock=Get-Content -LiteralPath "$root\dependencies.lock.json" -Raw|ConvertFrom-Json
$cache='W:\DEV\.cache\downloads\divinelabs-idleslayer';New-Item -ItemType Directory -Force $cache|Out-Null
foreach($name in @('loader','runtime')){
 $entry=$lock.$name;$zip=Join-Path $cache "$name.zip"
 if(-not(Test-Path -LiteralPath $zip)){Invoke-WebRequest $entry.url -OutFile $zip}
 if((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $entry.sha256){throw "Неверная контрольная сумма: $name"}
}
$stage=Join-Path 'W:\DEV\.cache\temp' ('divinelabs-build-'+[Guid]::NewGuid().ToString('N'))
$portable=Join-Path $stage 'portable';$payload=Join-Path $portable 'payload';New-Item -ItemType Directory -Force "$payload\Mods","$payload\UserLibs","$payload\Plugins","$payload\UserData"|Out-Null
Expand-Archive -LiteralPath "$cache\loader.zip" -DestinationPath $payload
Move-Item -LiteralPath "$payload\MelonLoader" -Destination "$payload\DivineLabs_Loader"
Expand-Archive -LiteralPath "$cache\runtime.zip" -DestinationPath "$payload\DivineLabs_Loader\Dependencies\dotnet"
Copy-Item -LiteralPath "$fork\Output\Release\win-x64\MelonLoader\net6.0\MelonLoader.dll" -Destination "$payload\DivineLabs_Loader\net6\MelonLoader.dll" -Force
Copy-Item -LiteralPath "$fork\Output\bootstrap\MelonLoader.Bootstrap.dll" -Destination "$payload\version.dll"
Copy-Item -LiteralPath "$root\src\Mod\bin\Release\net6.0\DivineLabs.IdleSlayer.Mod.dll" -Destination "$payload\Mods"
Copy-Item -LiteralPath "$root\src\Contracts\bin\Release\net6.0\DivineLabs.IdleSlayer.Contracts.dll" -Destination "$payload\UserLibs"
Copy-Item -LiteralPath "$fork\LICENSE.md","$fork\NOTICE.txt" -Destination "$payload\DivineLabs_Loader\Documentation" -Force
@('[loader]','disable_start_screen = true','[console]','hide_console = true','[unityengine]','force_il2cpp_dumper_version = "2022.1.0-pre-release.21"')|Set-Content -LiteralPath "$payload\UserData\Loader.cfg"
$files=[ordered]@{};Get-ChildItem -LiteralPath $payload -Recurse -File|ForEach-Object{$relative=[IO.Path]::GetRelativePath($payload,$_.FullName).Replace('\','/');$files[$relative]=(Get-FileHash $_.FullName).Hash}
@{SchemaVersion=1;Version='0.2.0';Files=$files}|ConvertTo-Json -Depth 5|Set-Content "$payload\manifest.json"
$components=Join-Path $Output 'DivineLabs-IdleSlayer-0.2.0-components.zip'
if(Test-Path $components){throw 'Комплект этой версии уже собран; выберите новый каталог Output.'}
[IO.Compression.ZipFile]::CreateFromDirectory($payload,$components,[IO.Compression.CompressionLevel]::Optimal,$false)
$index=@{version='0.2.0';url='https://github.com/TheDivineLabs/DivineLabs_IdleSlayer_Mods/releases/download/v0.2.0/DivineLabs-IdleSlayer-0.2.0-components.zip';sha256=(Get-FileHash $components).Hash}
$index|ConvertTo-Json|Set-Content "$root\src\Runtime\Resources\package-index.json"
RunDotnet @('publish',"$root\src\Launcher\Launcher.csproj",'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=false','-o',$portable)
Copy-Item -LiteralPath "$root\LICENSE","$root\THIRD-PARTY-NOTICES.txt","$root\README.md" -Destination $portable
Copy-Item -LiteralPath "$root\licenses" -Destination $portable -Recurse
$dotnetDir=Split-Path (Get-Command $Dotnet).Source -Parent
foreach($notice in @('LICENSE.txt','ThirdPartyNotices.txt')){if(Test-Path "$dotnetDir\$notice"){Copy-Item "$dotnetDir\$notice" (Join-Path $portable "DOTNET-$notice")}}
$portableOutput=Join-Path $Output 'portable';Move-Item -LiteralPath $portable -Destination $portableOutput
$archive=Join-Path $Output 'DivineLabs-IdleSlayer-0.2.0-portable.zip'
& "$PSScriptRoot\Package.ps1" -Portable $portableOutput -Destination $archive
if(-not $SkipInstaller){
 $installer=@('publish',"$root\src\Installer\Installer.csproj",'-c','Release','-r','win-x64',"-p:PackageArchive=$archive",'-o',(Join-Path $stage 'installer'))
 if($NativeBin){$installer+='-p:IlcUseEnvironmentalTools=true'};RunDotnet $installer
 Copy-Item -LiteralPath "$stage\installer\DivineLabs.IdleSlayer.Setup.exe" -Destination (Join-Path $Output 'DivineLabs-IdleSlayer-0.2.0-Setup.exe')
}
Get-ChildItem -LiteralPath $Output -File|Where-Object Extension -in '.zip','.exe'|ForEach-Object{"$((Get-FileHash $_.FullName).Hash)  $($_.Name)"}|Set-Content (Join-Path $Output 'SHA256SUMS.txt')
Write-Output "Готово: $Output"
