"""Copy installed Idle Slayer APK files from an authorized phone; never modify the phone."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import zipfile
import xml.etree.ElementTree as ET

from inspect_apk import InvalidApk, inspect, manifest_values

DEFAULT_ADB = Path(r'W:\DEV\tools\android-sdk\platform-tools\adb.exe')
PRIVATE_ROOT = Path(r'W:\DEV\environments\idle-slayer\android')
PACKAGE_PATTERN = re.compile(r'[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+')


class CollectionError(ValueError):
    pass


class Adb:
    def __init__(self, executable, serial=None):
        self.executable = str(executable)
        self.serial = serial
        self.environment = os.environ.copy()
        self.environment['ANDROID_USER_HOME'] = r'W:\DEV\.state\android\.android'
        self.environment['ANDROID_SDK_HOME'] = r'W:\DEV\.state\android'
        self.environment['ADB_MDNS_AUTO_CONNECT'] = '0'

    def run(self, *arguments, timeout=30):
        command = [self.executable]
        if self.serial is not None:
            command += ['-s', self.serial]
        Path(self.environment['ANDROID_USER_HOME']).mkdir(parents=True, exist_ok=True)
        try:
            result = subprocess.run(command + list(arguments), capture_output=True,
                                    encoding='utf-8', errors='replace', timeout=timeout,
                                    env=self.environment, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        except subprocess.TimeoutExpired as error:
            raise CollectionError('ADB не ответил вовремя. Проверьте кабель и подключение телефона.') from error
        except OSError as error:
            raise CollectionError('Не удалось запустить ADB. Выполните Get-AndroidSdk.ps1.') from error
        if result.returncode != 0:
            # ADB diagnostics can contain serials and local user paths; do not echo them.
            raise CollectionError('Команда ADB не выполнена. Проверьте подключение и разрешение USB-отладки.')
        return result.stdout


def devices(client):
    result = []
    for line in client.run('devices').splitlines():
        fields = line.strip().split()
        if len(fields) == 2 and fields[1] in ('device', 'unauthorized', 'offline', 'recovery', 'sideload'):
            result.append((fields[0], fields[1]))
    return result


def select_device(client, index):
    found = devices(client)
    if not found:
        raise CollectionError('Телефон не обнаружен. Подключите его по USB и включите USB-отладку.')
    if index is None:
        if len(found) != 1:
            raise CollectionError('Подключено несколько устройств. Используйте --list-devices и --device-index.')
        index = 1
    if index < 1 or index > len(found):
        raise CollectionError('Номер устройства отсутствует в списке ADB.')
    serial, state = found[index - 1]
    if state == 'unauthorized':
        raise CollectionError('На телефоне требуется ваше разрешение USB-отладки. После подтверждения повторите команду.')
    if state != 'device':
        raise CollectionError('Телефон недоступен для чтения APK. Проверьте кабель и обычную загрузку Android.')
    return Adb(client.executable, serial)


def find_package(client, requested):
    if requested:
        if not PACKAGE_PATTERN.fullmatch(requested):
            raise CollectionError('Некорректное имя Android-пакета.')
        return requested
    packages = []
    for line in client.run('shell', 'pm', 'list', 'packages').splitlines():
        if line.startswith('package:'):
            package = line[len('package:'):].strip()
            if PACKAGE_PATTERN.fullmatch(package) and 'slayer' in package.lower():
                packages.append(package)
    packages = sorted(set(packages))
    if not packages:
        raise CollectionError('Установленная Idle Slayer не найдена. Можно явно указать --package.')
    if len(packages) != 1:
        raise CollectionError('Найдено несколько пакетов Slayer. Укажите --package: ' + ', '.join(packages))
    return packages[0]


def apk_paths(text):
    paths = []
    for line in text.splitlines():
        if not line.startswith('package:'):
            if line.strip():
                raise CollectionError('Android вернул неожиданный ответ на запрос APK.')
            continue
        path = line[len('package:'):].strip()
        if (not re.fullmatch(r'/[A-Za-z0-9_./+=,@%~-]+\.apk', path)
                or '..' in path.split('/') or '.' in path.split('/') or '//' in path
                or not path.startswith(('/data/app/', '/mnt/expand/', '/system/app/', '/product/app/'))):
            raise CollectionError('Android вернул некорректный путь APK.')
        if path in paths:
            raise CollectionError('Android вернул повторяющийся путь APK.')
        paths.append(path)
    if not paths or len(paths) > 100:
        raise CollectionError('Не найден корректный комплект APK установленного приложения.')
    return sorted(paths, key=lambda path: (PurePosixPath(path).name != 'base.apk', path))


def manifest_from_apk(path):
    with zipfile.ZipFile(path) as archive:
        entries = [entry for entry in archive.infolist() if entry.filename == 'AndroidManifest.xml']
        if len(entries) != 1 or entries[0].file_size > 8 * 1024 * 1024:
            raise CollectionError('Некорректный AndroidManifest в полученном APK.')
        try:
            return manifest_values(archive.read(entries[0]))
        except ET.ParseError as error:
            raise CollectionError('Повреждён AndroidManifest полученного APK.') from error


def collect(client, package, output):
    paths = apk_paths(client.run('shell', 'pm', 'path', package))
    api_text = client.run('shell', 'getprop', 'ro.build.version.sdk').strip()
    if not api_text.isdigit():
        raise CollectionError('Не удалось прочитать версию Android API.')
    abis = [abi for abi in client.run('shell', 'getprop', 'ro.product.cpu.abilist').strip().split(',') if re.fullmatch('[a-zA-Z0-9_-]+', abi)]
    if int(api_text) < 26 or 'arm64-v8a' not in abis:
        raise CollectionError('Закреплённый загрузчик требует ARM64 и Android API 26 или новее.')
    output = Path(output)
    output.mkdir(parents=True, exist_ok=False)
    files = []
    version = None
    for index, remote in enumerate(paths, 1):
        filename = f'{index:02d}-{PurePosixPath(remote).name}'
        partial = output / (filename + '.partial')
        client.run('pull', remote, str(partial), timeout=300)
        manifest = manifest_from_apk(partial)
        if manifest.get('Package') != package:
            raise CollectionError('Пакет полученного APK не совпадает с выбранной игрой.')
        if manifest.get('VersionCode') is None:
            raise CollectionError('В APK отсутствует versionCode.')
        if version is None:
            version = manifest['VersionCode']
        if manifest.get('VersionCode') != version:
            raise CollectionError('Части APK относятся к разным версиям. Повторите после завершения обновления игры.')
        with partial.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest().upper()
        partial.rename(output / filename)
        files.append({'File': filename, 'Size': (output / filename).stat().st_size, 'Sha256': digest})
    # Detect updates to the installed package during collection. No app data is read.
    if paths != apk_paths(client.run('shell', 'pm', 'path', package)):
        raise CollectionError('Во время чтения игра обновилась. Исходные части сохранены; повторите в новой папке.')
    report = {'SchemaVersion': 1, 'Complete': True, 'Package': package, 'VersionCode': version,
              'DeviceApi': int(api_text), 'DeviceAbis': abis, 'Files': files,
              'SplitApk': len(files) > 1, 'Approved': False}
    if len(files) == 1:
        try:
            candidate = inspect(output / files[0]['File'])
            (output / 'candidate.json').write_text(json.dumps(candidate, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
            report['BuildInput'] = files[0]['File']
        except InvalidApk as error:
            report['AnalysisRequired'] = str(error)
    else:
        report['AnalysisRequired'] = 'Split APK сохранены целиком. Следующий шаг: Prepare-AndroidInput.ps1 -GamePackage с путём этой папки.'
    (output / 'collection.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--adb', type=Path, default=DEFAULT_ADB)
    parser.add_argument('--list-devices', action='store_true', help='List device states without identifiers.')
    parser.add_argument('--device-index', type=int, help='One-based index from --list-devices.')
    parser.add_argument('--package', help='Optional explicit installed game package.')
    parser.add_argument('--output', type=Path, default=PRIVATE_ROOT / ('phone-'+datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')))
    args = parser.parse_args()
    try:
        if not args.adb.is_file():
            raise CollectionError('ADB отсутствует. Сначала выполните scripts/android/Get-AndroidSdk.ps1.')
        client = Adb(args.adb)
        if args.list_devices:
            found = devices(client)
            for index, (_, state) in enumerate(found, 1):
                print(f'{index}: {state}')
            if not found:
                print('Телефон не обнаружен.')
            return
        # resolve() also follows existing junctions: never write through an alias onto C:.
        output = args.output.resolve()
        if not output.is_relative_to(Path('W:/DEV').resolve()):
            raise CollectionError('APK и отчёты должны находиться под W:\\DEV.')
        selected = select_device(client, args.device_index)
        package = find_package(selected, args.package)
        report = collect(selected, package, output)
        print(f'Скопировано APK: {len(report["Files"])}. Телефон и сохранения не изменялись.')
        print(f'Локальная папка: {output}')
        print(report.get('AnalysisRequired', 'Кандидат профиля создан выключенным. Требуется анализ совместимости.'))
    except (CollectionError, InvalidApk, OSError, zipfile.BadZipFile) as error:
        parser.exit(2, str(error)+'\n')


if __name__ == '__main__':
    main()
