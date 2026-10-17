param(
 [string]$BasePortable='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.0\DivineLabs-IdleSlayer-0.5.0-portable.zip',
 [string]$Output='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.3',
 [string]$Dotnet='W:\DEV\tools\dotnet\dotnet.exe',
 [string]$NativeBin='', [string]$NativeLib=''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$full=[IO.Path]::GetFullPath($Output)
if(-not $full.StartsWith('W:\DEV\artifacts\',[StringComparison]::OrdinalIgnoreCase)){throw 'Release output must be under W:\DEV\artifacts.'}
if((Test-Path -LiteralPath $full)-and (Get-ChildItem -LiteralPath $full -Force)){throw 'Choose an empty output directory.'}
$baseHash=(Get-FileHash -LiteralPath $BasePortable).Hash
$checksum=Get-Content -LiteralPath (Join-Path (Split-Path $BasePortable) 'SHA256SUMS.txt')|Where-Object {$_ -match ('  '+[regex]::Escape((Split-Path $BasePortable -Leaf))+'$')}
if(-not $checksum -or ($checksum -split ' ')[0] -ne $baseHash){throw 'The base package does not match its release checksum.'}
$env:DOTNET_CLI_HOME='W:\DEV\.cache\dotnet-home';$env:NUGET_PACKAGES='W:\DEV\.cache\nuget';$env:TEMP='W:\DEV\.cache\temp';$env:TMP=$env:TEMP
if($NativeBin){$env:PATH=$NativeBin+';'+$env:PATH;$env:LIB=$NativeLib}
function RunDotnet([string[]]$Arguments){& $Dotnet @Arguments;if($LASTEXITCODE-ne 0){throw 'Desktop release build failed.'}}
$version=[regex]::Match([IO.File]::ReadAllText("$root\src\Runtime\Product.cs"),'const string Version = "([^"]+)"').Groups[1].Value
$componentVersion=[regex]::Match([IO.File]::ReadAllText("$root\src\Runtime\Product.cs"),'const string ComponentVersion = "([^"]+)"').Groups[1].Value
New-Item -ItemType Directory -Force $full|Out-Null
$portable=Join-Path $full 'portable'
Expand-Archive -LiteralPath $BasePortable -DestinationPath $portable
$payload=Join-Path $portable 'App\Components'
$manifest=Get-Content -Raw -LiteralPath "$payload\manifest.json"|ConvertFrom-Json
if($manifest.Version-ne $componentVersion){throw 'Unexpected game component version.'}
foreach($file in $manifest.Files.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $payload $file.Name)).Hash -ne $file.Value){throw "Component checksum mismatch: $($file.Name)"}}
$index=Get-Content -Raw -LiteralPath "$root\src\Runtime\Resources\package-index.json"|ConvertFrom-Json
$componentArchive=Join-Path (Split-Path $BasePortable) "DivineLabs-IdleSlayer-$componentVersion-components.zip"
if($index.version-ne $componentVersion -or (Get-FileHash -LiteralPath $componentArchive).Hash-ne $index.sha256){throw 'The component archive does not match the pinned download index.'}
Copy-Item -LiteralPath $componentArchive -Destination $full
foreach($document in @('LICENSE','THIRD-PARTY-NOTICES.txt')){Copy-Item -LiteralPath (Join-Path $root $document) -Destination (Join-Path $portable "Docs\$document")}
# Repository README links do not belong in an offline package.
if(Test-Path -LiteralPath "$portable\Docs\README.md"){Remove-Item -LiteralPath "$portable\Docs\README.md"}
Copy-Item -LiteralPath "$root\LICENSE" -Destination "$portable\Docs\LICENSE.txt"
Copy-Item -LiteralPath "$root\docs\README.ru.txt" -Destination "$portable\Docs\README.ru.txt"
Copy-Item -LiteralPath "$root\docs\README.en.txt" -Destination "$portable\Docs\README.en.txt"
RunDotnet @('publish',"$root\src\Launcher\Launcher.csproj",'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=false','-p:DebugType=None','-p:DebugSymbols=false','-o',"$portable\App\Launcher")
$bootstrap=@('publish',"$root\src\Bootstrap\Bootstrap.csproj",'-c','Release','-r','win-x64','-p:DebugType=None','-p:DebugSymbols=false','-o',"$full\Bootstrap")
if($NativeBin){$bootstrap+='-p:IlcUseEnvironmentalTools=true'}
RunDotnet $bootstrap
Copy-Item -LiteralPath "$full\Bootstrap\DivineLabs.IdleSlayer.exe" -Destination $portable -Force
$archive=Join-Path $full "DivineLabs-IdleSlayer-$version-portable.zip"
& "$root\scripts\Package.ps1" -Portable $portable -Destination $archive
$setup=@('publish',"$root\src\Installer\Installer.csproj",'-c','Release','-r','win-x64',"-p:PackageArchive=$archive",'-p:DebugType=None','-p:DebugSymbols=false','-o',"$full\Setup")
if($NativeBin){$setup+='-p:IlcUseEnvironmentalTools=true'}
RunDotnet $setup
Copy-Item -LiteralPath "$full\Setup\DivineLabs.IdleSlayer.Setup.exe" -Destination "$full\DivineLabs Idle Slayer Mods.exe"
Get-ChildItem -LiteralPath $full -File|Where-Object Extension -in '.zip','.exe'|ForEach-Object{"$((Get-FileHash -LiteralPath $_.FullName).Hash)  $($_.Name)"}|Set-Content -LiteralPath "$full\SHA256SUMS.txt"
@{ApplicationVersion=$version;ComponentVersion=$componentVersion;BasePackageSha256=$baseHash;GameComponentsChanged=$false;SourceCommit=(& git -c ('safe.directory='+$root.Replace('\','/')) -C $root rev-parse HEAD).Trim()}|ConvertTo-Json|Set-Content -LiteralPath "$full\build.json"
Write-Output "Desktop release: $full"
