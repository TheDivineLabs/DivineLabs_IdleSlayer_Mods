#requires -Version 7.4
param(
 [Parameter(Mandatory)][string]$GameApk,
 [Parameter(Mandatory)][string]$CompatibilityProfile,
 [string]$AndroidBuildTools='W:\DEV\tools\android-sdk\build-tools\37.0.0',
 [string]$KeyStore='W:\DEV\.state\signing\DivineLabs-Android.p12',
 [string]$KeyAlias='divinelabs',
 [string]$ProtectedPasswordFile='W:\DEV\.state\signing\DivineLabs-Android.password.dpapi',
 [Parameter(Mandatory)][ValidateRange(26,99)][int]$DeviceApi,
 [string]$Output=('W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\android-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
 [string]$Dotnet='W:\DEV\tools\dotnet\dotnet.exe',
 [string]$Python='python',
 [string]$JavaHome='W:\DEV\tools\java\21'
)
$ErrorActionPreference='Stop'
$GameApk=[IO.Path]::GetFullPath($GameApk);$CompatibilityProfile=[IO.Path]::GetFullPath($CompatibilityProfile);$AndroidBuildTools=[IO.Path]::GetFullPath($AndroidBuildTools);$KeyStore=[IO.Path]::GetFullPath($KeyStore);$Output=[IO.Path]::GetFullPath($Output)
$env:JAVA_HOME=$JavaHome
if(!(Test-Path -LiteralPath (Join-Path $JavaHome 'bin\java.exe'))){throw 'Укажите установленный JDK 17+ через JavaHome.'}
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$env:DOTNET_ROOT=Split-Path $Dotnet -Parent;$env:PATH=$env:DOTNET_ROOT+';'+(Join-Path $JavaHome 'bin')+';'+$env:PATH;$env:DOTNET_CLI_HOME='W:\DEV\.cache\dotnet-home';$env:NUGET_PACKAGES='W:\DEV\.cache\nuget';$env:TEMP='W:\DEV\.cache\temp';$env:TMP=$env:TEMP;$env:PYTHONDONTWRITEBYTECODE='1'
if(-not [IO.Path]::GetFullPath($Output).StartsWith('W:\DEV\',[StringComparison]::OrdinalIgnoreCase)){throw 'Выходные файлы должны находиться под W:\DEV.'}
if(Test-Path -LiteralPath $Output){throw 'Выберите новую папку Output. Прежние сборки не перезаписываются.'}
if($DeviceApi -lt 26){throw 'Закреплённый Android-загрузчик требует Android 8 / API 26 или новее.'}
if(!(Test-Path -LiteralPath $GameApk -PathType Leaf) -or [IO.Path]::GetExtension($GameApk) -ne '.apk'){throw 'Укажите исходный полный APK игры.'}
if(!(Test-Path -LiteralPath $CompatibilityProfile -PathType Leaf)){throw 'Нет проверенного профиля Android-игры. Сначала inspect_apk.py и анализ игровых методов.'}
$gameProfile=Get-Content -LiteralPath $CompatibilityProfile -Raw | ConvertFrom-Json
if($gameProfile.SchemaVersion -ne 1 -or $gameProfile.Approved -ne $true){throw 'Профиль совместимости не утверждён. Неизвестная игра не включается автоматически.'}
foreach($tool in @('zipalign.exe','apksigner.bat')){if(!(Test-Path -LiteralPath (Join-Path $AndroidBuildTools $tool))){throw "Нет Android Build Tools: $tool"}}
if([string]::IsNullOrWhiteSpace($env:LEMONLOADER_KEYSTORE_PASSWORD) -and (Test-Path -LiteralPath $ProtectedPasswordFile -PathType Leaf)){
 try{
  $secret=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($ProtectedPasswordFile),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
  $env:LEMONLOADER_KEYSTORE_PASSWORD=[Text.Encoding]::UTF8.GetString($secret)
  [Array]::Clear($secret,0,$secret.Length)
 }catch{throw 'Не удалось открыть пароль подписи DPAPI текущего пользователя. Ключ не изменён.'}
}
if(!(Test-Path -LiteralPath $KeyStore -PathType Leaf) -or [string]::IsNullOrWhiteSpace($env:LEMONLOADER_KEYSTORE_PASSWORD)){throw 'Укажите постоянный keystore и LEMONLOADER_KEYSTORE_PASSWORD. Ключи в Git не добавляются.'}
New-Item -ItemType Directory -Path $Output | Out-Null
$candidate=Join-Path $Output 'input-profile.json'
& $Python -B (Join-Path $PSScriptRoot 'inspect_apk.py') $GameApk --output $candidate
if($LASTEXITCODE -ne 0){throw 'APK не прошёл структурную проверку.'}
$inputProfile=Get-Content -LiteralPath $candidate -Raw | ConvertFrom-Json
foreach($field in @('Package','VersionName','NativeSha256','MetadataSha256')){if([string]$gameProfile.$field -cne [string]$inputProfile.$field){throw "APK не совпадает с проверенным профилем: $field"}}
if($gameProfile.OriginalApkSha256 -and $gameProfile.OriginalApkSha256 -ne $inputProfile.OriginalApkSha256){throw 'Исходный APK не совпадает с проверенной копией.'}
if([int]$inputProfile.MinSdk -gt $DeviceApi){throw 'Версия Android телефона ниже minSdk исходной игры.'}
$preparedInput=$GameApk
if([int]$inputProfile.MinSdk -lt 26 -or @($inputProfile.Abis | Where-Object {$_ -ne 'arm64-v8a'}).Count -gt 0){
 $preparedInput=Join-Path $Output 'input-api26.apk'
 & $Python -B (Join-Path $PSScriptRoot 'prepare_apk.py') $GameApk --output $preparedInput
 if($LASTEXITCODE -ne 0){throw 'Не удалось согласовать minSdk с Android-загрузчиком.'}
}
$tools=& "$PSScriptRoot\Get-AndroidTools.ps1"
function RunPatcher([string[]]$Arguments){& $tools.Patcher @Arguments;if($LASTEXITCODE -ne 0){throw 'Операция Android Patcher не завершена. Исходный APK сохранён.'}}
$interop=Join-Path $Output 'Interop'
RunPatcher @('generate-interop',$preparedInput,'--output',$interop)
& $Dotnet run --project (Join-Path $PSScriptRoot 'ApiReview') -c Release "-p:AndroidLoaderPath=$($tools.LoaderManaged)" -- $interop (Join-Path $Output 'api-review.json')
if($LASTEXITCODE -ne 0){throw 'Android API не соответствует контрактам игровых патчей.'}
$managed=Join-Path $Output 'Managed'
& $Dotnet build (Join-Path $repo 'src\AndroidMod\AndroidMod.csproj') -c Release "-p:AndroidLoaderPath=$($tools.LoaderManaged)" "-p:AndroidInteropPath=$interop" "-p:AndroidGameProfile=$CompatibilityProfile" -p:DebugType=None -p:DebugSymbols=false -o $managed
if($LASTEXITCODE -ne 0){throw 'Мод не собрался с Android Interop. Windows DLL вместо него не подставляются.'}
$deployment=Join-Path $Output 'Deployment'
New-Item -ItemType Directory -Force -Path "$deployment\Mods","$deployment\UserLibs" | Out-Null
Copy-Item -LiteralPath "$managed\DivineLabs.IdleSlayer.Mod.dll" -Destination "$deployment\Mods"
Copy-Item -LiteralPath "$managed\DivineLabs.IdleSlayer.Contracts.dll" -Destination "$deployment\UserLibs"
$apk=Join-Path $Output 'DivineLabs.IdleSlayer.Android.apk'
RunPatcher @('inject',$preparedInput,'--output',$apk,'--interop',$interop,'--release',$tools.LoaderZip,'--runtime','android','--deployment',$deployment,'--profile','production','--align','--zipalign',(Join-Path $AndroidBuildTools 'zipalign.exe'),'--keystore',$KeyStore,'--key-alias',$KeyAlias,'--apksigner',(Join-Path $AndroidBuildTools 'apksigner.bat'))
# Success requires both signing verification and 16 KiB native library alignment.
& (Join-Path $AndroidBuildTools 'apksigner.bat') verify --verbose $apk
if($LASTEXITCODE -ne 0){throw 'Подпись итогового APK не прошла проверку. APK не готов к установке.'}
& (Join-Path $AndroidBuildTools 'zipalign.exe') -c -P 16 4 $apk
if($LASTEXITCODE -ne 0){throw 'Итоговый APK не прошёл проверку выравнивания 16 KiB.'}
& $Python -B (Join-Path $PSScriptRoot 'audit_runtime.py') $apk --output (Join-Path $Output 'runtime-audit.json')
if($LASTEXITCODE -ne 0){throw 'Не прошла статическая проверка Android runtime payload/ELF.'}
# Verify our two modules and the unchanged native/metadata entries, without extracting a whole game APK on NTFS.
$archive=[IO.Compression.ZipFile]::OpenRead($apk)
try{
 foreach($item in @(@{Path='lib/arm64-v8a/libil2cpp.so';Hash=$gameProfile.NativeSha256},@{Path='assets/bin/Data/Managed/Metadata/global-metadata.dat';Hash=$gameProfile.MetadataSha256},@{Path='assets/LemonLoader/deployment/Mods/DivineLabs.IdleSlayer.Mod.dll';Hash=(Get-FileHash "$deployment\Mods\DivineLabs.IdleSlayer.Mod.dll").Hash},@{Path='assets/LemonLoader/deployment/UserLibs/DivineLabs.IdleSlayer.Contracts.dll';Hash=(Get-FileHash "$deployment\UserLibs\DivineLabs.IdleSlayer.Contracts.dll").Hash})){
  $entry=$archive.GetEntry($item.Path);if(!$entry){throw "Нет обязательного компонента: $($item.Path)"};$stream=$entry.Open();try{$actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()};if($actual -ne $item.Hash){throw 'Компонент APK не совпадает с исходным или собственной сборкой.'}
 }
}finally{$archive.Dispose()}
$docs=Join-Path $Output 'Licenses';New-Item -ItemType Directory -Path $docs | Out-Null
Copy-Item -LiteralPath $tools.LoaderLicense -Destination "$docs\LemonLoader-Apache-2.0.txt"
Copy-Item -LiteralPath $tools.LoaderNotices -Destination "$docs\LemonLoader-NOTICE.txt"
Copy-Item -LiteralPath (Join-Path $tools.LoaderRoot 'licenses') -Destination (Join-Path $docs 'Dependencies') -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination "$docs\DivineLabs-Apache-2.0.txt"
"$((Get-FileHash -LiteralPath $apk).Hash)  $([IO.Path]::GetFileName($apk))" | Set-Content -LiteralPath (Join-Path $Output 'SHA256SUMS.txt')
Write-Output "Создан локальный APK: $apk. Установка на телефон и проверка игрового запуска ещё не выполнялись."
