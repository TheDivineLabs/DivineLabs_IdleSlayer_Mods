"""Check Android loader payload and ARM64 ELF load segments, without running game code."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import zipfile
from inspect_apk import InvalidApk, manifest_values

PAGE = 16384
RUNTIME = 'assets/LemonLoader/runtime/'
REQUIRED = (
    'lib/arm64-v8a/libmain.so', 'lib/arm64-v8a/libunity.so', 'lib/arm64-v8a/libil2cpp.so',
    RUNTIME+'loader/net6/MelonLoader.dll', RUNTIME+'loader/net6/0Harmony.dll',
    RUNTIME+'loader/net6/Il2CppInterop.Runtime.dll',
    RUNTIME+'dotnet/shared/Microsoft.NETCore.App/11.0.0/libcoreclr.so',
    RUNTIME+'dotnet/shared/Microsoft.NETCore.App/11.0.0/libclrjit.so',
    RUNTIME+'dotnet/shared/Microsoft.NETCore.App/11.0.0/System.Private.CoreLib.dll',
    RUNTIME+'interop/Assembly-CSharp.dll',
    'assets/LemonLoader/deployment/Mods/DivineLabs.IdleSlayer.Mod.dll',
    'assets/LemonLoader/deployment/UserLibs/DivineLabs.IdleSlayer.Contracts.dll',
)


def elf_segments(stream, length):
    header = stream.read(64)
    if len(header) != 64 or header[:7] != b'\x7fELF\x02\x01\x01':
        raise InvalidApk('Нужен обычный ELF64 little-endian.')
    kind, machine = struct.unpack_from('<HH', header, 16)
    if kind != 3 or machine != 183:
        raise InvalidApk('Нативная библиотека не является ARM64 shared object.')
    phoff = struct.unpack_from('<Q', header, 32)[0]
    size, count = struct.unpack_from('<HH', header, 54)
    if phoff < 64 or size < 56 or count == 0 or count > 4096 or phoff+size*count > length:
        raise InvalidApk('Таблица ELF-сегментов выходит за границы библиотеки.')
    stream.seek(phoff)
    table = stream.read(size*count)
    if len(table) != size*count:
        raise InvalidApk('Таблица ELF-сегментов усечена.')
    loads, relro = [], []
    for index in range(count):
        kind, flags, offset, address, _, file_size, memory_size, alignment = struct.unpack_from('<IIQQQQQQ', table, index*size)
        if kind not in (1, 0x6474e552):
            continue
        if offset+file_size > length or memory_size < file_size:
            raise InvalidApk('Некорректные границы ELF-сегмента.')
        if kind == 1:
            if (alignment < PAGE or alignment & (alignment-1)
                    or address % PAGE != offset % PAGE):
                raise InvalidApk('ELF LOAD не выровнен для страниц 16 KiB.')
            loads.append(alignment)
        else:
            # Informational only: the end may be padded by the surrounding LOAD layout.
            # A raw RELRO end is not sufficient evidence of a crash or runtime compatibility.
            relro.append((address+memory_size) % PAGE == 0)
    if not loads:
        raise InvalidApk('В ELF отсутствуют загружаемые сегменты.')
    return {'LoadSegments':len(loads), 'MinimumLoadAlignment':min(loads),
            'RelroRawEndAligned16KiB':relro}


def audit(path):
    libraries = []
    with zipfile.ZipFile(path) as apk:
        names = apk.namelist()
        if len(names) != len(set(names)):
            raise InvalidApk('Повторяющиеся пути в итоговом APK.')
        missing = set(REQUIRED) - set(names)
        if missing:
            raise InvalidApk('Нет обязательного компонента загрузки: '+', '.join(sorted(missing)))
        for name, maximum in [('AndroidManifest.xml',8*1024**2), ('assets/LemonLoader/payload.json',1024**2)]:
            if apk.getinfo(name).file_size > maximum:
                raise InvalidApk('Слишком большой manifest.')
        manifest = manifest_values(apk.read('AndroidManifest.xml'))
        payload = json.loads(apk.read('assets/LemonLoader/payload.json'))
        if payload.get('formatVersion') != 9 or payload.get('runtimeRid') != 'android-arm64':
            raise InvalidApk('Неизвестный Android runtime payload.')
        deployment = payload.get('deploymentFiles', [])
        paths = [item['path'] for item in deployment]
        if len(paths) != len(set(paths)):
            raise InvalidApk('Неоднозначный deployment payload.')
        for item in deployment:
            name = item['path']
            if (name.startswith('/') or '\\' in name or ':' in name
                    or any(part in ('', '.', '..') for part in name.split('/'))):
                raise InvalidApk('Некорректный путь deployment payload.')
            if 'assets/LemonLoader/deployment/'+name not in names:
                raise InvalidApk('Deployment объявляет отсутствующий файл.')
        expected = {'Mods/DivineLabs.IdleSlayer.Mod.dll','UserLibs/DivineLabs.IdleSlayer.Contracts.dll'}
        if not expected.issubset(paths):
            raise InvalidApk('Модули DivineLabs не объявлены для извлечения.')
        for entry in apk.infolist():
            if not entry.filename.endswith('.so'):
                continue
            if entry.file_size > 512*1024**2:
                raise InvalidApk('Слишком большая нативная библиотека.')
            with apk.open(entry) as stream:
                libraries.append({'Path':entry.filename, **elf_segments(stream,entry.file_size)})
        application = manifest.get('ApplicationClass') or ''
        pairip = application.startswith('com.pairip.') and 'lib/arm64-v8a/libpairipcore.so' in names
    with Path(path).open('rb') as stream:
        digest = hashlib.file_digest(stream,'sha256').hexdigest().upper()
    return {'SchemaVersion':1,'ApkSha256':digest,'Manifest':manifest,
            'LoaderPayloadPresent':True,'ElfLoadSegmentsAligned16KiB':True,
            'NativeLibraries':libraries,'PairipApplicationDetected':pairip,
            'RuntimeVerified':False,'Physical16KiBDeviceVerified':False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('apk',type=Path)
    parser.add_argument('--output',type=Path,required=True)
    args = parser.parse_args()
    try:
        output = args.output.resolve()
        if not output.is_relative_to(Path('W:/DEV').resolve()):
            raise InvalidApk('Отчёт должен находиться под W:\\DEV.')
        report = audit(args.apk)
        with output.open('x',encoding='utf-8') as stream:
            json.dump(report,stream,ensure_ascii=False,indent=2)
            stream.write('\n')
        print(f'Проверены payload и ELF LOAD: {len(report["NativeLibraries"])} ARM64-библиотеки.')
        if report['PairipApplicationDetected']:
            print('Обнаружена оболочка PairIP. Запуск переподписанного APK требует отдельной проверки на устройстве.')
        print('Это статическая проверка; запуск игры и работа функций не подтверждены.')
    except (InvalidApk,OSError,ValueError,KeyError,zipfile.BadZipFile) as error:
        parser.exit(2,str(error)+'\n')


if __name__ == '__main__':
    main()
