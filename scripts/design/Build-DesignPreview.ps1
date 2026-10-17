param(
 [string]$BasePortable='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.0\DivineLabs-IdleSlayer-0.5.0-portable.zip',
 [string]$Output='W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\design-preview-2026-10-17',
 [string]$Dotnet='W:\DEV\tools\dotnet\dotnet.exe',
 [string]$NativeBin='', [string]$NativeLib=''
)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$full=[IO.Path]::GetFullPath($Output)
if(-not $full.StartsWith('W:\DEV\artifacts\',[StringComparison]::OrdinalIgnoreCase)){throw 'Preview output must be under W:\DEV\artifacts.'}
if((Test-Path -LiteralPath $full)-and (Get-ChildItem -LiteralPath $full -Force)){throw 'Choose an empty preview output directory.'}
if(-not(Test-Path -LiteralPath $BasePortable)){throw 'A previously verified portable package is required.'}
$env:DOTNET_CLI_HOME='W:\DEV\.cache\dotnet-home';$env:NUGET_PACKAGES='W:\DEV\.cache\nuget';$env:TEMP='W:\DEV\.cache\temp';$env:TMP=$env:TEMP
if($NativeBin){$env:PATH=$NativeBin+';'+$env:PATH;$env:LIB=$NativeLib}
function RunDotnet([string[]]$Arguments){& $Dotnet @Arguments;if($LASTEXITCODE-ne 0){throw 'Design preview build failed.'}}
New-Item -ItemType Directory -Force $full|Out-Null
$portable=Join-Path $full 'portable'
Expand-Archive -LiteralPath $BasePortable -DestinationPath $portable
foreach($document in @('LICENSE','THIRD-PARTY-NOTICES.txt','README.md')){Copy-Item -LiteralPath (Join-Path $root $document) -Destination (Join-Path $portable "Docs\$document")}
RunDotnet @('publish',"$root\src\Launcher\Launcher.csproj",'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=false','-p:DebugType=None','-p:DebugSymbols=false','-o',"$portable\App\Launcher")
$archive=Join-Path $full 'DivineLabs-IdleSlayer-design-preview-portable.zip'
& "$root\scripts\Package.ps1" -Portable $portable -Destination $archive
if($LASTEXITCODE-ne 0){throw 'Packaging failed.'}
$setupArguments=@('publish',"$root\src\Installer\Installer.csproj",'-c','Release','-r','win-x64',"-p:PackageArchive=$archive",'-p:DebugType=None','-p:DebugSymbols=false','-o',"$full\Setup")
if($NativeBin){$setupArguments+='-p:IlcUseEnvironmentalTools=true'}
RunDotnet $setupArguments
Copy-Item -LiteralPath "$full\Setup\DivineLabs.IdleSlayer.Setup.exe" -Destination "$full\DivineLabs Idle Slayer Mods.exe"
Get-ChildItem -LiteralPath $full -File|Where-Object Extension -in '.zip','.exe'|ForEach-Object{"$((Get-FileHash -LiteralPath $_.FullName).Hash)  $($_.Name)"}|Set-Content -LiteralPath "$full\SHA256SUMS.txt"
@{Kind='Design preview; not a new stable release';BasePackageSha256=(Get-FileHash -LiteralPath $BasePortable).Hash;GameComponentsChanged=$false;SourceCommit=(& git -c ('safe.directory='+$root.Replace('\','/')) -C $root rev-parse HEAD).Trim()}|ConvertTo-Json|Set-Content -LiteralPath "$full\preview.json"
Write-Output "Design preview: $full"
