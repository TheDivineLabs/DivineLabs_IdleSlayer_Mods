param(
 [string]$Dotnet='W:\DEV\tools\dotnet\dotnet.exe',
 [string]$DesktopInterop='W:\steam\steamapps\common\Idle Slayer\DivineLabs_Loader\Il2CppAssemblies'
)
# Compile-only diagnostic. Its DLLs are not Android deliverables and never enter Build-Android.ps1.
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$tools=& "$PSScriptRoot\Get-AndroidTools.ps1"
$env:DOTNET_ROOT=Split-Path $Dotnet -Parent;$env:DOTNET_CLI_HOME='W:\DEV\.cache\dotnet-home';$env:NUGET_PACKAGES='W:\DEV\.cache\nuget';$env:TEMP='W:\DEV\.cache\temp';$env:TMP=$env:TEMP
$output=Join-Path 'W:\DEV\.cache\temp' ('DivineLabs-Android-typecheck-'+[Guid]::NewGuid().ToString('N'))
& $Dotnet build (Join-Path $repo 'src\AndroidMod\AndroidMod.csproj') -c Release "-p:AndroidLoaderPath=$($tools.LoaderManaged)" "-p:AndroidInteropPath=$DesktopInterop" "-p:AndroidGameProfile=$repo\android\profiles\unapproved.example.json" -o $output
if($LASTEXITCODE -ne 0){throw 'Проверка компиляции не прошла.'}
Write-Output "Только проверка компиляции с настольными Unity-прокси. Не подтверждает Android-совместимость. Выход: $output"
