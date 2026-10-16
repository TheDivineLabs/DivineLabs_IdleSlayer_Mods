"""Manual CI-only Android startup probe. Game binaries and keys never become artifacts."""
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import sys
import tarfile
import time
import urllib.request
import zipfile
from stage_splits import stage
from prepare_apk import prepare
from inspect_apk import inspect, sha_entry, NATIVE, METADATA
from audit_runtime import audit

REPO=Path(__file__).resolve().parents[2]
ROOT=Path(os.environ.get('RUNNER_TEMP','/nonexistent'))/'divinelabs-android'
REPORTS=ROOT/'reports'
PROFILE=REPO/'android/profiles/idle-slayer-7.3.4-arm64-preview.json'
SOURCE='https://d.apkpure.net/b/XAPK/com.pabloleban.IdleSlayer?versionCode=866&nc=arm64-v8a&sv=25'
PACKAGE='com.pabloleban.IdleSlayer'


def run(arguments,label,environment=None,timeout=600):
    print(label,flush=True)
    result=subprocess.run([str(item) for item in arguments],capture_output=True,text=True,
                          encoding='utf-8',errors='replace',env=environment,timeout=timeout)
    (ROOT/'private').mkdir(exist_ok=True)
    (ROOT/'private'/f'{label}.log').write_text(result.stdout+'\n'+result.stderr,encoding='utf-8')
    if result.returncode:
        raise RuntimeError(f'{label}: exit {result.returncode}; private tool log retained only in temporary runner storage')
    return result.stdout


def download(url,digest,target):
    print('Download '+target.name,flush=True)
    sha=hashlib.sha256()
    with urllib.request.urlopen(url,timeout=120) as source,target.open('xb') as output:
        while chunk:=source.read(1024*1024):
            sha.update(chunk);output.write(chunk)
    if sha.hexdigest().lower()!=digest.lower():
        raise ValueError('Download checksum mismatch: '+target.name)
    return target


def write_report(name,value):
    REPORTS.mkdir(parents=True,exist_ok=True)
    (REPORTS/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


def build():
    ROOT.mkdir(exist_ok=False)
    lock=json.loads((REPO/'android/dependencies.lock.json').read_text(encoding='utf-8-sig'))
    profile=json.loads(PROFILE.read_text(encoding='utf-8-sig'))
    sdk=Path(os.environ['ANDROID_HOME']);tools=sdk/'build-tools/37.0.0'
    java=Path(os.environ['JAVA_HOME'])/'bin/java'
    original=download(SOURCE,profile['SourceXapkSha256'],ROOT/'original.xapk')
    parts=ROOT/'parts';inventory=stage(original,parts)
    for item in inventory['Files']:
        text=run([tools/'apksigner','verify','--print-certs',parts/item['File']], 'verify-source-'+item['File'])
        signers=set(re.findall(r'^(?:V[\d.]+ )?Signer(?: #\d+)?: certificate SHA-256 digest: ([a-fA-F0-9]{64})$',text,re.M))
        if {s.upper() for s in signers}!={profile['SourceSignerSha256'].upper()}:
            raise ValueError('Source signer differs from reviewed profile')
    editor=download(lock['apkEditor']['url'],lock['apkEditor']['sha256'],ROOT/'APKEditor.jar')
    merged=ROOT/'merged.apk'
    run([java,'-Xmx2g','-jar',editor,'m','-i',parts,'-o',merged,'-validate-modules'],'merge')
    actual=inspect(merged)
    for key in ('Package','VersionName','VersionCode','NativeSha256','MetadataSha256'):
        if actual[key]!=profile[key]:raise ValueError('Different game profile: '+key)
    source=ROOT/'api26.apk';prepare(merged,source)
    loader_zip=download(lock['loader']['url'],lock['loader']['sha256'],ROOT/'loader.zip')
    loader_root=ROOT/'loader'
    with zipfile.ZipFile(loader_zip) as archive:archive.extractall(loader_root)
    linux=lock['patcherLinux']
    patcher_tar=download(linux['url'],linux['sha256'],ROOT/'patcher.tar.gz')
    patcher_root=ROOT/'patcher';patcher_root.mkdir()
    with tarfile.open(patcher_tar) as archive:archive.extractall(patcher_root,filter='data')
    matches=list(patcher_root.rglob('LemonLoader.Patcher.CLI'))
    if len(matches)!=1:raise ValueError('Unexpected Linux patcher layout')
    patcher=matches[0];patcher.chmod(0o755)
    interop=ROOT/'interop'
    run([patcher,'generate-interop',source,'--output',interop],'generate-interop')
    managed=loader_root/'assets/LemonLoader/runtime/loader/net6'
    run(['dotnet','run','--project',REPO/'scripts/android/ApiReview','-c','Release',f'-p:AndroidLoaderPath={managed}','--',interop,ROOT/'api-review.json'],'review-api')
    output=ROOT/'managed'
    run(['dotnet','build',REPO/'src/AndroidMod/AndroidMod.csproj','-c','Release',f'-p:AndroidLoaderPath={managed}',f'-p:AndroidInteropPath={interop}',f'-p:AndroidGameProfile={PROFILE}','-p:DebugType=None','-p:DebugSymbols=false','-o',output],'build-mod')
    deployment=ROOT/'deployment'
    for directory,filename in [('Mods','DivineLabs.IdleSlayer.Mod.dll'),('UserLibs','DivineLabs.IdleSlayer.Contracts.dll')]:
        (deployment/directory).mkdir(parents=True)
        shutil.copyfile(output/filename,deployment/directory/filename)
    environment=os.environ.copy()
    environment['LEMONLOADER_KEYSTORE_PASSWORD']=secrets.token_urlsafe(32)
    key=ROOT/'ci-ephemeral.p12'
    run([Path(os.environ['JAVA_HOME'])/'bin/keytool','-genkeypair','-keystore',key,'-storetype','PKCS12','-alias','ci','-keyalg','RSA','-keysize','3072','-validity','30','-dname','CN=DivineLabs CI Test','-storepass:env','LEMONLOADER_KEYSTORE_PASSWORD','-keypass:env','LEMONLOADER_KEYSTORE_PASSWORD'],'ephemeral-test-key',environment)
    apk=ROOT/'test.apk'
    run([patcher,'inject',source,'--output',apk,'--interop',interop,'--release',loader_zip,'--runtime','android','--deployment',deployment,'--profile','production','--align','--zipalign',tools/'zipalign','--keystore',key,'--key-alias','ci','--apksigner',tools/'apksigner'],'inject-sign',environment)
    environment.pop('LEMONLOADER_KEYSTORE_PASSWORD')
    run([tools/'apksigner','verify','--verbose',apk],'verify-signature')
    run([tools/'zipalign','-c','-P','16','4',apk],'verify-zip-alignment')
    result=audit(apk)
    with zipfile.ZipFile(apk) as archive:
        if sha_entry(archive,NATIVE,512*1024**2)!=profile['NativeSha256']:raise ValueError('Native game library changed')
        if sha_entry(archive,METADATA,256*1024**2)!=profile['MetadataSha256']:raise ValueError('Game metadata changed')
    write_report('build.json',{'Built':True,'Commit':os.environ.get('GITHUB_SHA'),
        'OriginalXapkSha256':profile['SourceXapkSha256'],'ApkSha256':result['ApkSha256'],
        'SignatureVerified':True,'ZipAlignmentVerified':True,'NativeLibrariesChecked':len(result['NativeLibraries']),
        'PairipApplicationDetected':result['PairipApplicationDetected'],'EphemeralCiCertificate':True,
        'ExactLocalApkTested':False,'RuntimeVerified':False})
    print('CI test APK prepared. APK, Interop and signing material will not be uploaded.',flush=True)


def adb(*arguments,timeout=45,binary=False,required=True):
    result=subprocess.run(['adb','-e',*arguments],capture_output=True,timeout=timeout)
    if required and result.returncode:raise RuntimeError('ADB operation failed: '+arguments[0])
    return result.stdout if binary else result.stdout.decode('utf-8',errors='replace').strip()


def sanitized_log(text):
    lines=[]
    for line in text.splitlines():
        if re.search(r'password|token|secret|authorization|cookie|api.?key|oauth|credential|client.?id',line,re.I):continue
        if re.search(r'DivineLabs|MelonLoader|LemonLoader|CoreCLR|AndroidRuntime|Fatal signal|FATAL EXCEPTION|UnsatisfiedLinkError|dlopen failed|pairip',line,re.I):
            lines.append(line[:700])
    return '\n'.join(lines[-350:])+'\n'


def probe():
    sdk=adb('shell','getprop','ro.build.version.sdk')
    abis=adb('shell','getprop','ro.product.cpu.abilist')
    result={'DeviceKind':'GitHub Android emulator','AndroidApi':sdk,'Abis':abis.split(','),
            'Installed':False,'ProcessAlive':False,'DivineLabsInitialized':False,
            'MenuVerified':False,'GameplayVerified':False,'PhysicalDeviceVerified':False,
            'ExactLocalApkTested':False,'EphemeralCiCertificate':True}
    try:
        if 'arm64-v8a' not in result['Abis']:raise RuntimeError('This emulator image does not expose ARM64 translation')
        installed=adb('install','--no-streaming',str(ROOT/'test.apk'),timeout=180)
        result['Installed']='Success' in installed
        if not result['Installed']:raise RuntimeError('APK installation not confirmed')
        adb('shell','am','start','-W','-n',PACKAGE+'/com.unity3d.player.UnityPlayerActivity',timeout=90)
        logs=''
        for iteration in range(20):
            time.sleep(3)
            alive=bool(adb('shell','pidof',PACKAGE,required=False))
            logs=adb('logcat','-d','-v','brief',timeout=45)
            result['ProcessAlive']=alive
            result['DivineLabsInitialized']='DivineLabs готов.' in logs
            if not alive or result['DivineLabsInitialized']:break
        clean=sanitized_log(logs)
        (REPORTS/'startup-filtered.log').write_text(clean,encoding='utf-8')
        resumed=adb('shell','dumpsys','activity','activities',required=False)
        result['TopActivity']=[line.strip()[:400] for line in resumed.splitlines() if 'mResumedActivity' in line]
        result['PairipMentionedInLog']='pairip' in clean.lower()
        image=adb('exec-out','screencap','-p',binary=True,required=False)
        if image.startswith(b'\x89PNG\r\n\x1a\n'):(REPORTS/'screen.png').write_bytes(image)
        result['StartupPassed']=result['ProcessAlive'] and result['DivineLabsInitialized']
        write_report('startup.json',result)
        print(json.dumps(result,ensure_ascii=False),flush=True)
        if not result['StartupPassed']:raise RuntimeError('Startup not confirmed; inspect filtered startup report')
    finally:
        write_report('startup.json',result)


def main():
    if os.environ.get('GITHUB_ACTIONS')!='true' or sys.platform!='linux':
        raise SystemExit('This script runs only on an isolated GitHub Linux runner, not on a user phone.')
    mode=sys.argv[1] if len(sys.argv)>1 else ''
    try:
        if mode=='build':build()
        elif mode=='probe':probe()
        else:raise ValueError('Expected build or probe')
    except Exception as error:
        write_report('failure.json',{'Stage':mode,'Type':type(error).__name__,'Message':str(error)[:300],'RuntimeVerified':False})
        print(f'{mode} failed: {type(error).__name__}: {error}',file=sys.stderr)
        raise SystemExit(1)

if __name__=='__main__':main()
