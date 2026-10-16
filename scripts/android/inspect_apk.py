"""Inspect a local Unity ARM64 APK without extracting it or changing game/signing data."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import zipfile
import xml.etree.ElementTree as ET

METADATA = 'assets/bin/Data/Managed/Metadata/global-metadata.dat'
NATIVE = 'lib/arm64-v8a/libil2cpp.so'
ANDROID_NS = '{http://schemas.android.com/apk/res/android}'

class InvalidApk(ValueError):
    pass

def manifest_values(data, locations=None):
    if len(data) > 8 * 1024 * 1024:
        raise InvalidApk('AndroidManifest слишком велик.')
    if data.lstrip().startswith(b'<'):
        if b'<!DOCTYPE' in data.upper() or b'<!ENTITY' in data.upper():
            raise InvalidApk('DTD в AndroidManifest не поддерживается.')
        root = ET.fromstring(data)
        if root.tag != 'manifest':
            raise InvalidApk('Не найден manifest.')
        values = {'Package': root.get('package'), 'VersionName': root.get(ANDROID_NS+'versionName'), 'VersionCode': root.get(ANDROID_NS+'versionCode')}
        application = root.find('application')
        if application is not None:
            values['ApplicationClass'] = application.get(ANDROID_NS+'name')
        sdk = root.find('uses-sdk')
        if sdk is not None:
            values.update(MinSdk=sdk.get(ANDROID_NS+'minSdkVersion'), TargetSdk=sdk.get(ANDROID_NS+'targetSdkVersion'))
        return values
    try:
        kind, header, total = struct.unpack_from('<HHI', data)
        if kind != 3 or header < 8 or total != len(data):
            raise InvalidApk('Некорректный бинарный AndroidManifest.')
        strings, values, pos = [], {}, header
        def string_at(index):
            if index == 0xffffffff:
                return None
            if index >= len(strings):
                raise InvalidApk('Неверная ссылка на строку AndroidManifest.')
            return strings[index]
        while pos < total:
            kind, chunk_header, size = struct.unpack_from('<HHI', data, pos)
            if chunk_header < 8 or size < chunk_header or pos+size > total:
                raise InvalidApk('Повреждённый блок AndroidManifest.')
            if kind == 1:
                if chunk_header < 28:
                    raise InvalidApk('Повреждена таблица строк.')
                count, _, flags, start, _ = struct.unpack_from('<IIIII', data, pos+8)
                if count > 100000 or chunk_header+count*4 > size or start < chunk_header+count*4:
                    raise InvalidApk('Повреждены смещения строк.')
                strings = []
                def length8(offset):
                    value = data[offset]
                    return (((value & 127) << 8) | data[offset+1], offset+2) if value & 128 else (value, offset+1)
                for i in range(count):
                    relative = struct.unpack_from('<I', data, pos+chunk_header+4*i)[0]
                    offset = pos+start+relative
                    if not pos+start <= offset < pos+size:
                        raise InvalidApk('Строка за пределами таблицы.')
                    if flags & 0x100:
                        _, offset = length8(offset)
                        length, offset = length8(offset)
                        end = offset+length
                        if end >= pos+size or data[end] != 0:
                            raise InvalidApk('Повреждена UTF-8 строка.')
                        strings.append(data[offset:end].decode('utf-8'))
                    else:
                        length = struct.unpack_from('<H', data, offset)[0]; offset += 2
                        if length & 0x8000:
                            length = ((length & 0x7fff) << 16) | struct.unpack_from('<H', data, offset)[0]; offset += 2
                        end = offset+length*2
                        if end+2 > pos+size or data[end:end+2] != b'\0\0':
                            raise InvalidApk('Повреждена UTF-16 строка.')
                        strings.append(data[offset:end].decode('utf-16-le'))
            elif kind == 0x102:
                if chunk_header < 16 or size < 36:
                    raise InvalidApk('Повреждён XML-элемент.')
                name = string_at(struct.unpack_from('<I', data, pos+20)[0])
                attr_start, attr_size, count = struct.unpack_from('<HHH', data, pos+24)
                if attr_size < 20 or attr_start < 20 or 16+attr_start+count*attr_size > size:
                    raise InvalidApk('Повреждён список XML-атрибутов.')
                for i in range(count):
                    at = pos+16+attr_start+i*attr_size
                    ns, key, raw = struct.unpack_from('<III', data, at)
                    key = string_at(key); namespace = string_at(ns)
                    value_type = data[at+15]; typed = struct.unpack_from('<I', data, at+16)[0]
                    value = string_at(raw) if raw != 0xffffffff else string_at(typed) if value_type == 3 else typed
                    mapping = {'package':'Package','versionName':'VersionName','versionCode':'VersionCode'} if name == 'manifest' else {'minSdkVersion':'MinSdk','targetSdkVersion':'TargetSdk'} if name == 'uses-sdk' else {'name':'ApplicationClass'} if name == 'application' else {}
                    if key in mapping and ((namespace is None) if key == 'package' else namespace == ANDROID_NS[1:-1]):
                        values[mapping[key]] = value
                        if locations is not None:
                            locations[mapping[key]] = at
            pos += size
        return values
    except (IndexError, struct.error, UnicodeError) as error:
        raise InvalidApk('Повреждён бинарный AndroidManifest.') from error

def sha_entry(archive, name, maximum):
    info = archive.getinfo(name)
    if info.file_size > maximum:
        raise InvalidApk('Слишком большой компонент: '+name)
    digest = hashlib.sha256()
    with archive.open(info) as stream:
        for chunk in iter(lambda: stream.read(1024*1024), b''):
            digest.update(chunk)
    return digest.hexdigest().upper()

def inspect(path):
    path = Path(path)
    with zipfile.ZipFile(path) as apk:
        names = [entry.filename for entry in apk.infolist()]
        if len(names) > 100000 or len(names) != len(set(names)):
            raise InvalidApk('Неоднозначные ZIP-пути в APK.')
        for name in names:
            if name.startswith('/') or '\\' in name or ':' in name or any(ord(c)<32 for c in name) or any(p in ('.','..','') for p in name.rstrip('/').split('/')):
                raise InvalidApk('Небезопасный ZIP-путь.')
        if any(name.startswith(('assets/LemonLoader/','assets/MelonLoader/')) for name in names):
            raise InvalidApk('Уже установлен загрузчик. Нужен исходный APK игры.')
        if 'AndroidManifest.xml' not in names or NATIVE not in names or METADATA not in names:
            raise InvalidApk('Нужен полный Unity IL2CPP APK с ARM64-библиотекой и metadata; одного base.apk из split-установки недостаточно.')
        if apk.getinfo('AndroidManifest.xml').file_size > 8*1024*1024:
            raise InvalidApk('AndroidManifest слишком велик.')
        values = manifest_values(apk.read('AndroidManifest.xml'))
        if not isinstance(values.get('Package'), str) or not values['Package'] or not isinstance(values.get('VersionName'),str) or not values['VersionName']:
            raise InvalidApk('Не удалось прочитать пакет и версию игры.')
        with apk.open(NATIVE) as stream:
            elf = stream.read(20)
        if len(elf)<20 or elf[:6]!=b'\x7fELF\x02\x01' or struct.unpack_from('<H',elf,18)[0]!=183:
            raise InvalidApk('libil2cpp.so не является ARM64 ELF64.')
        with apk.open(METADATA) as stream:
            metadata_header = stream.read(8)
        if len(metadata_header)!=8 or struct.unpack_from('<I',metadata_header)[0]!=0xfab11baf:
            raise InvalidApk('Metadata зашифрована или не имеет обычного заголовка IL2CPP.')
        version = None
        global_managers = 'assets/bin/Data/globalgamemanagers'
        if global_managers in names and apk.getinfo(global_managers).file_size <= 16*1024*1024:
            match = re.search(rb'\b(?:20\d{2}|6000)\.\d+\.\d+[abfp]\d+\b', apk.read(global_managers))
            if match:
                version = match[0].decode('ascii')
        values.update(SchemaVersion=1, Approved=False, NativeSha256=sha_entry(apk,NATIVE,512*1024*1024),MetadataSha256=sha_entry(apk,METADATA,256*1024*1024),UnityVersion=version,Abis=sorted({n.split('/')[1] for n in names if n.startswith('lib/') and len(n.split('/'))==3}))
    with path.open('rb') as stream:
        values['OriginalApkSha256'] = hashlib.file_digest(stream,'sha256').hexdigest().upper()
    return values

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('apk',type=Path)
    parser.add_argument('--output',type=Path,required=True,help='Private compatibility candidate, disabled until reviewed.')
    args = parser.parse_args()
    try:
        report = inspect(args.apk)
        args.output.parent.mkdir(parents=True,exist_ok=True)
        if args.output.exists():
            raise InvalidApk('Отчёт уже существует. Выберите новый путь; утверждённые профили не перезаписываются.')
        args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print('APK проверен структурно. Профиль выключен; совместимость игровых функций ещё не подтверждена.')
    except (InvalidApk, OSError, zipfile.BadZipFile, ET.ParseError) as error:
        parser.exit(2, str(error)+'\n')

if __name__ == '__main__':
    main()
