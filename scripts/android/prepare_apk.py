"""Prepare API 26+/ARM64 for this loader; never extract or overwrite the source APK."""
import argparse
from pathlib import Path
import shutil
import struct
import zipfile
from inspect_apk import InvalidApk, inspect, manifest_values

def minimum_sdk(manifest, api=26):
    positions={}
    values=manifest_values(manifest,positions)
    try:
        current=int(values['MinSdk'])
    except (KeyError,TypeError,ValueError) as error:
        raise InvalidApk('Не удалось определить minSdk.') from error
    if current>=api:
        return manifest
    if 'MinSdk' not in positions:
        raise InvalidApk('Для APK нужен бинарный AndroidManifest с явным minSdk.')
    at=positions['MinSdk'];patched=bytearray(manifest)
    struct.pack_into('<I',patched,at+8,0xffffffff)
    struct.pack_into('<HBBI',patched,at+12,8,0,0x10,api)
    if manifest_values(patched)['MinSdk']!=api:
        raise InvalidApk('Не удалось проверить изменение minSdk.')
    return bytes(patched)

def prepare(source,output):
    source,output=Path(source),Path(output)
    inspect(source)
    if output.exists() or output.resolve()==source.resolve():
        raise InvalidApk('Исходный или прежний APK не перезаписывается.')
    part=output.with_suffix(output.suffix+'.partial')
    if part.exists():
        raise InvalidApk('Существующий промежуточный APK сохранён. Выберите другой путь.')
    output.parent.mkdir(parents=True,exist_ok=True)
    try:
        with zipfile.ZipFile(source) as original,zipfile.ZipFile(part,'x',allowZip64=True) as result:
            for entry in original.infolist():
                name=entry.filename
                upper=name.upper()
                if name.startswith('lib/') and len(name.split('/'))>=3 and name.split('/')[1]!='arm64-v8a':
                    continue
                if upper.startswith('META-INF/') and (upper=='META-INF/MANIFEST.MF' or upper.endswith(('.SF','.RSA','.DSA','.EC'))):
                    continue
                if name=='AndroidManifest.xml':
                    result.writestr(entry,minimum_sdk(original.read(entry)))
                else:
                    with original.open(entry) as src,result.open(entry,'w',force_zip64=entry.file_size>2**31) as dst:
                        shutil.copyfileobj(src,dst,1024*1024)
        # Both game hashes and package identity must remain the same.
        before,after=inspect(source),inspect(part)
        for field in ['NativeSha256','MetadataSha256','Package','VersionName']:
            if before[field]!=after[field]:
                raise InvalidApk('Изменился игровой компонент: '+field)
        part.rename(output)
    except BaseException:
        if part.exists():part.unlink()
        raise

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('apk',type=Path);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    try:
        prepare(args.apk,args.output)
        print('Создана локальная копия APK для ARM64 с minSdk не ниже 26. Её ещё нужно выровнять и подписать.')
    except (InvalidApk,OSError,zipfile.BadZipFile) as error:
        parser.exit(2,str(error)+'\n')
if __name__=='__main__':main()
