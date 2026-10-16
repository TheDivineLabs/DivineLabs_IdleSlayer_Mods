#requires -Version 7.4
param(
 [Parameter(Mandatory)][string]$GamePackage,
 [string]$Output=('W:\DEV\environments\idle-slayer\android\prepared-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
 [string]$Python='python',
 [string]$AndroidBuildTools='W:\DEV\tools\android-sdk\build-tools\37.0.0',
 [string]$JavaHome='W:\DEV\tools\java\21'
)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$Output=[IO.Path]::GetFullPath($Output)
if(!$Output.StartsWith('W:\DEV\',[StringComparison]::OrdinalIgnoreCase)){throw 'Исходники игры и подготовка должны находиться под W:\DEV.'}
if(Test-Path -LiteralPath $Output){throw 'Выберите новую папку Output. Исходники не перезаписываются.'}
$lock=Get-Content -LiteralPath (Join-Path $repo 'android\dependencies.lock.json') -Raw | ConvertFrom-Json
$cache='W:\DEV\.cache\downloads\divinelabs-idleslayer-android'
$jar=Join-Path $cache ('APKEditor-'+$lock.apkEditor.version+'.jar')
New-Item -ItemType Directory -Force -Path $cache | Out-Null
if(!(Test-Path -LiteralPath $jar)){
 $partial=$jar+'.'+[Guid]::NewGuid().ToString('N')+'.partial'
 Invoke-WebRequest -Uri $lock.apkEditor.url -OutFile $partial
 if((Get-FileHash -LiteralPath $partial).Hash -ne $lock.apkEditor.sha256){throw 'Контрольная сумма APKEditor не совпала.'}
 Move-Item -LiteralPath $partial -Destination $jar
}
if((Get-FileHash -LiteralPath $jar).Hash -ne $lock.apkEditor.sha256){throw 'Повреждён APKEditor.'}
$env:JAVA_HOME=$JavaHome;$env:TEMP='W:\DEV\.cache\temp';$env:TMP=$env:TEMP
$parts=Join-Path $Output 'Parts'
& $Python -B (Join-Path $PSScriptRoot 'stage_splits.py') $GamePackage --output $parts
if($LASTEXITCODE -ne 0){throw 'Подготовка частей APK остановлена.'}
$inventory=Get-Content -LiteralPath (Join-Path $parts 'parts.json') -Raw | ConvertFrom-Json
$signers=$null
foreach($file in $inventory.Files){
 $text=& (Join-Path $AndroidBuildTools 'apksigner.bat') verify --print-certs (Join-Path $parts $file.File)
 if($LASTEXITCODE -ne 0){throw 'Подпись исходной части APK не прошла проверку.'}
 $digests=@($text | ForEach-Object {if($_ -match '^(?:V[\d.]+ )?Signer(?: #\d+)?: certificate SHA-256 digest: ([a-fA-F0-9]{64})$'){$Matches[1].ToUpperInvariant()}} | Sort-Object -Unique)
 if($digests.Count -eq 0){throw 'Сертификат подписанта APK не прочитан.'}
 $identity=$digests -join ','
 if($null -eq $signers){$signers=$identity}elseif($signers -cne $identity){throw 'Исходные части APK подписаны разными ключами.'}
}
$merged=Join-Path $Output 'game-arm64.apk'
& (Join-Path $JavaHome 'bin\java.exe') -Xmx2g '-Djava.io.tmpdir=W:\DEV\.cache\temp' -jar $jar m -i $parts -o $merged -validate-modules
if($LASTEXITCODE -ne 0){throw 'Не удалось объединить split APK.'}
$candidate=Join-Path $Output 'candidate.json'
& $Python -B (Join-Path $PSScriptRoot 'inspect_apk.py') $merged --output $candidate
if($LASTEXITCODE -ne 0){throw 'Объединённый APK не прошёл проверку.'}
$profile=Get-Content -LiteralPath $candidate -Raw | ConvertFrom-Json
foreach($field in @('Package','VersionCode','NativeSha256','MetadataSha256')){if([string]$profile.$field -cne [string]$inventory.$field){throw "При слиянии изменился игровой компонент: $field"}}
$inventory | Add-Member -NotePropertyName SignerSha256 -NotePropertyValue $signers
$inventory | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $Output 'provenance.json') -Encoding utf8
[pscustomobject]@{GameApk=$merged;Candidate=$candidate;SignerSha256=$signers;Approved=$false}
