"""Stage APK parts for signature verification and merging; never overwrite input."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile
from inspect_apk import InvalidApk, manifest_values, sha_entry, NATIVE, METADATA


def safe_name(name):
    if (not name or name.startswith('/') or '\\' in name or ':' in name
            or any(ord(c) < 32 for c in name)
            or any(part in ('', '.', '..') for part in name.rstrip('/').split('/'))):
        raise InvalidApk('Небезопасный путь в комплекте APK.')


def stage(source, output):
    source, output = Path(source), Path(output)
    if output.exists():
        raise InvalidApk('Папка подготовки уже существует. Прежние файлы не перезаписываются.')
    output.mkdir(parents=True)
    files = []
    if source.is_dir():
        sources = sorted(source.glob('*.apk'))
        if not sources or len(sources) > 100:
            raise InvalidApk('В папке должен быть полный комплект, не более 100 APK.')
        # A phone collection includes an integrity inventory. Never silently consume altered copies.
        inventory = source / 'collection.json'
        if inventory.exists():
            collection = json.loads(inventory.read_text(encoding='utf-8-sig'))
            if collection.get('Complete') is not True:
                raise InvalidApk('Получение APK с телефона не завершено.')
            declared = collection.get('Files', [])
            if {item['File'] for item in declared} != {p.name for p in sources} or len(declared) != len(sources):
                raise InvalidApk('Комплект APK не совпадает с отчётом получения.')
            for item in declared:
                safe_name(item['File'])
                with (source/item['File']).open('rb') as stream:
                    if hashlib.file_digest(stream, 'sha256').hexdigest().upper() != item['Sha256'].upper():
                        raise InvalidApk('Исходный APK изменён после получения с телефона.')
        for index, path in enumerate(sources):
            if path.stat().st_size > 1024**3:
                raise InvalidApk('Слишком большой APK.')
            target = output / f'{index:03d}.apk'
            with path.open('rb') as src, target.open('xb') as dst:
                shutil.copyfileobj(src, dst)
            files.append(target)
    else:
        with zipfile.ZipFile(source) as archive:
            entries = archive.infolist()
            names = [entry.filename for entry in entries]
            if len(names) > 10000 or len(names) != len(set(names)):
                raise InvalidApk('Неоднозначный комплект APK.')
            for name in names:
                safe_name(name)
            parts = [entry for entry in entries if entry.filename.lower().endswith('.apk')]
            if not parts or len(parts) > 100 or sum(p.file_size for p in parts) > 2*1024**3:
                raise InvalidApk('Некорректный размер комплекта APK.')
            if 'manifest.json' in names:
                manifest = archive.getinfo('manifest.json')
                if manifest.file_size > 1024**2:
                    raise InvalidApk('Манифест XAPK слишком велик.')
                bundle = json.loads(archive.read(manifest))
                declared = bundle.get('split_apks')
                if declared is not None and ({item['file'] for item in declared} != {p.filename for p in parts} or len(declared) != len(parts)):
                    raise InvalidApk('В комплекте отсутствуют объявленные части XAPK.')
            for index, entry in enumerate(parts):
                if entry.file_size > 1024**3:
                    raise InvalidApk('Слишком большой APK.')
                target = output / f'{index:03d}.apk'
                with archive.open(entry) as src, target.open('xb') as dst:
                    shutil.copyfileobj(src, dst)
                files.append(target)
    report, identity, found = [], None, {}
    for path in files:
        with zipfile.ZipFile(path) as apk:
            names = [entry.filename for entry in apk.infolist()]
            if len(names) > 100000 or len(names) != len(set(names)):
                raise InvalidApk('Неоднозначные пути внутри APK.')
            for name in names:
                safe_name(name)
            if any(name.startswith(('assets/LemonLoader/', 'assets/MelonLoader/')) for name in names):
                raise InvalidApk('В исходном комплекте уже установлен загрузчик.')
            manifest = apk.getinfo('AndroidManifest.xml')
            if manifest.file_size > 8*1024**2:
                raise InvalidApk('AndroidManifest слишком велик.')
            values = manifest_values(apk.read(manifest))
            current = (values.get('Package'), values.get('VersionCode'))
            if not current[0] or current[1] is None:
                raise InvalidApk('Не прочитаны package/versionCode части APK.')
            if identity is None:
                identity = current
            elif current != identity:
                raise InvalidApk('Части APK относятся к разным приложениям или версиям.')
            for name in (NATIVE, METADATA):
                if name in names:
                    if name in found:
                        raise InvalidApk('Игровая библиотека/metadata повторяется в разных частях.')
                    found[name] = sha_entry(apk, name, 512*1024**2)
        with path.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest().upper()
        report.append({'File': path.name, 'Sha256': digest, **values})
    if set(found) != {NATIVE, METADATA}:
        raise InvalidApk('Нужен полный ARM64 IL2CPP комплект с metadata и игровыми ресурсами.')
    return {'SchemaVersion': 1, 'Package': identity[0], 'VersionCode': identity[1],
            'Approved': False, 'Files': report, 'NativeSha256': found[NATIVE], 'MetadataSha256': found[METADATA]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        output = args.output.resolve()
        if not output.is_relative_to(Path('W:/DEV').resolve()):
            raise InvalidApk('Подготовка разрешена только под W:\\DEV.')
        report = stage(args.source, output)
        (output/'parts.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
        print('Части подготовлены. Следующий шаг — проверка подписей всех APK перед объединением.')
    except (InvalidApk, OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
        parser.exit(2, str(error)+'\n')


if __name__ == '__main__':
    main()
