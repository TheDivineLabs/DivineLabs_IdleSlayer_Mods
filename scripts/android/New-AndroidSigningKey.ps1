#requires -Version 7.4
param(
 [string]$KeyStore='W:\DEV\.state\signing\DivineLabs-Android.p12',
 [string]$ProtectedPasswordFile='W:\DEV\.state\signing\DivineLabs-Android.password.dpapi',
 [string]$KeyAlias='divinelabs',
 [string]$JavaHome='W:\DEV\tools\java\21'
)
$ErrorActionPreference='Stop'
foreach($path in @($KeyStore,$ProtectedPasswordFile)){
 if(![IO.Path]::GetFullPath($path).StartsWith('W:\DEV\',[StringComparison]::OrdinalIgnoreCase)){throw 'Ключи должны храниться под W:\DEV.'}
 if(Test-Path -LiteralPath $path){throw 'Ключ или защищённый пароль уже существует. Перезапись запрещена.'}
}
if(!$IsWindows){throw 'Защита пароля этого сценария использует Windows DPAPI текущего пользователя.'}
foreach($path in @($KeyStore,$ProtectedPasswordFile)){New-Item -ItemType Directory -Force -Path (Split-Path $path -Parent) | Out-Null}
$previous=$env:LEMONLOADER_KEYSTORE_PASSWORD
try{
 $password=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
 $protected=[Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($password),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
 # Persist the protected password first: a failure must not leave a newly created key inaccessible.
 [IO.File]::WriteAllBytes($ProtectedPasswordFile,$protected)
 $env:LEMONLOADER_KEYSTORE_PASSWORD=$password
 & (Join-Path $JavaHome 'bin\keytool.exe') -genkeypair -keystore $KeyStore -storetype PKCS12 -alias $KeyAlias -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=DivineLabs' -storepass:env LEMONLOADER_KEYSTORE_PASSWORD -keypass:env LEMONLOADER_KEYSTORE_PASSWORD
 if($LASTEXITCODE -ne 0){throw 'Ключ не создан. Существующие файлы сохранены для диагностики.'}
}finally{$env:LEMONLOADER_KEYSTORE_PASSWORD=$previous;$password=$null}
[pscustomobject]@{KeyStore=$KeyStore;KeyAlias=$KeyAlias;ProtectedPasswordFile=$ProtectedPasswordFile}
