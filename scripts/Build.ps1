param(
 [string]$GamePath='W:\steam\steamapps\common\Idle Slayer',
 [string]$Output='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\portable',
 [string]$Dotnet='dotnet',
 [switch]$SkipLoader,
 [string]$NativeBin='', [string]$NativeLib=''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME='W:\DEV\.cache\dotnet-home'
$env:NUGET_PACKAGES='W:\DEV\.cache\nuget\'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:TEMP='W:\DEV\.cache\temp';$env:TMP=$env:TEMP
function RunDotnet([string[]]$Arguments){ & $Dotnet @Arguments; if($LASTEXITCODE -ne 0){throw "Сборка завершилась с ошибкой: $($Arguments -join ' ')"} }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
if(-not $SkipLoader){
 RunDotnet @('build',"$root\loader\MelonLoader\MelonLoader.csproj",'-c','Release','-p:TargetFrameworks=net6.0','-p:DotNetTarget=net6.0','-p:Platform=x64',"-p:SolutionDir=$root\loader\")
 $nativeArgs=@('publish',"$root\loader\MelonLoader.Bootstrap\MelonLoader.Bootstrap.csproj",'-c','Release','-r','win-x64','-p:BootstrapTarget=net9.0','-p:Platform=x64','-p:SkipPublishAfterBuild=true',"-p:SolutionDir=$root\loader\",'-o',"$root\loader\Output\bootstrap")
 if($NativeBin){$env:PATH=$NativeBin+';'+$env:PATH;$env:LIB=$NativeLib;$nativeArgs+='-p:IlcUseEnvironmentalTools=true'}
 RunDotnet $nativeArgs
}
RunDotnet @('build',"$root\src\Mod\Mod.csproj",'-c','Release',"-p:GamePath=$GamePath")
RunDotnet @('publish',"$root\src\Launcher\Launcher.csproj",'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=false','-o',$Output)
$lock=Get-Content -LiteralPath "$root\dependencies.lock.json" -Raw|ConvertFrom-Json
$cache='W:\DEV\.cache\downloads\divinelabs-idleslayer';New-Item -ItemType Directory -Force -Path $cache|Out-Null
foreach($name in @('loader','runtime')){
 $entry=$lock.$name;$zip=Join-Path $cache "$name.zip"
 if(-not(Test-Path -LiteralPath $zip)){Invoke-WebRequest $entry.url -OutFile $zip}
 if((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $entry.sha256){throw "Неверная контрольная сумма: $name"}
}
$payload=Join-Path $Output 'payload';New-Item -ItemType Directory -Force -Path "$payload\Mods","$payload\UserLibs"|Out-Null
Expand-Archive -LiteralPath "$cache\loader.zip" -DestinationPath $payload -Force
Expand-Archive -LiteralPath "$cache\runtime.zip" -DestinationPath "$payload\MelonLoader\Dependencies\dotnet" -Force
Copy-Item -LiteralPath "$root\loader\Output\Release\win-x64\MelonLoader\net6.0\MelonLoader.dll" -Destination "$payload\MelonLoader\net6\MelonLoader.dll" -Force
$bootstrap="$root\loader\Output\bootstrap\MelonLoader.Bootstrap.dll"
if(-not(Test-Path -LiteralPath $bootstrap)){throw 'Сначала соберите нативный загрузчик.'}
Copy-Item -LiteralPath $bootstrap -Destination "$payload\version.dll" -Force
Copy-Item -LiteralPath "$root\src\Mod\bin\Release\net6.0\DivineLabs.IdleSlayer.Mod.dll" -Destination "$payload\Mods" -Force
Copy-Item -LiteralPath "$root\src\Contracts\bin\Release\net6.0\DivineLabs.IdleSlayer.Contracts.dll" -Destination "$payload\UserLibs" -Force
Copy-Item -LiteralPath "$root\LICENSE","$root\THIRD-PARTY-NOTICES.txt","$root\README.md" -Destination $Output -Force
Copy-Item -LiteralPath "$root\loader\LICENSE.md","$root\loader\NOTICE.txt" -Destination "$payload\MelonLoader\Documentation" -Force
$dotnetDir=Split-Path (Get-Command $Dotnet).Source -Parent
foreach($notice in @('LICENSE.txt','ThirdPartyNotices.txt')){if(Test-Path "$dotnetDir\$notice"){Copy-Item "$dotnetDir\$notice" (Join-Path $Output "DOTNET-$notice") -Force}}
Copy-Item -LiteralPath "$root\licenses" -Destination $Output -Recurse -Force
$manifest=[ordered]@{};Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object Name -ne 'manifest.json' | ForEach-Object { $relative=[IO.Path]::GetRelativePath($payload,$_.FullName);$manifest[$relative]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
$manifest|ConvertTo-Json -Depth 3|Set-Content -LiteralPath "$payload\manifest.json"
Write-Output "Готово: $Output"
