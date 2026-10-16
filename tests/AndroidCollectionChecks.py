"""Focused tests for the read-only Android APK collection contract (no physical phone)."""
import io
import json
import os
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import Mock
import zipfile

scripts = Path(__file__).resolve().parents[1] / 'scripts/android'
sys.path.insert(0, str(scripts))
import collect_game as collector
from inspect_apk import NATIVE, METADATA

PACKAGE = 'com.pabloleban.IdleSlayer'
BASE = '/data/app/~~abc/pkg/base.apk'
SPLIT = '/data/app/~~abc/pkg/split_config.arm64_v8a.apk'


def apk_bytes(package=PACKAGE, version=734, full=False):
    output = io.BytesIO()
    with zipfile.ZipFile(output, 'w') as archive:
        archive.writestr('AndroidManifest.xml', f'<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="{package}" android:versionCode="{version}" android:versionName="fixture"><uses-sdk android:minSdkVersion="26" /></manifest>')
        if full:
            elf = bytearray(64); elf[:6] = b'\x7fELF\x02\x01'; struct.pack_into('<H', elf, 18, 183)
            archive.writestr(NATIVE, elf)
            archive.writestr(METADATA, struct.pack('<II', 0xfab11baf, 39))
    return output.getvalue()


class FakeAdb:
    def __init__(self, apks=None, changed=False):
        self.apks = apks or {BASE: apk_bytes(full=True)}
        self.calls = []
        self.changed = changed
        self.path_calls = 0

    def run(self, *arguments, **kwargs):
        self.calls.append(arguments)
        if arguments == ('shell', 'pm', 'path', PACKAGE):
            self.path_calls += 1
            paths = list(self.apks)
            if self.changed and self.path_calls > 1:
                paths = [BASE.replace('pkg', 'updated')]
            return ''.join('package:'+path+'\n' for path in paths)
        if arguments == ('shell', 'getprop', 'ro.build.version.sdk'):
            return '34\n'
        if arguments == ('shell', 'getprop', 'ro.product.cpu.abilist'):
            return 'arm64-v8a,armeabi-v7a\n'
        if arguments[0] == 'pull':
            Path(arguments[2]).write_bytes(self.apks[arguments[1]])
            return ''
        raise AssertionError('Unexpected/non-read-only ADB operation: '+str(arguments))


class AndroidCollection(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(dir=os.environ['TEMP'], prefix='DivineLabs-adb-checks-')
        self.output = Path(self.temp.name) / 'collected'

    def tearDown(self):
        self.temp.cleanup()

    def test_complete_apk_remains_unapproved_and_has_hash(self):
        client = FakeAdb()
        report = collector.collect(client, PACKAGE, self.output)
        self.assertTrue(report['Complete']); self.assertFalse(report['Approved'])
        self.assertFalse(report['SplitApk'])
        candidate = json.loads((self.output/'candidate.json').read_text(encoding='utf-8'))
        self.assertFalse(candidate['Approved']); self.assertEqual(candidate['Package'], PACKAGE)
        self.assertEqual(len(report['Files'][0]['Sha256']), 64)
        self.assertEqual((self.output/report['BuildInput']).read_bytes(), client.apks[BASE])

    def test_all_split_parts_are_retained_without_claiming_build_input(self):
        client = FakeAdb({SPLIT: apk_bytes(), BASE: apk_bytes()})
        report = collector.collect(client, PACKAGE, self.output)
        self.assertTrue(report['SplitApk']); self.assertEqual(len(report['Files']), 2)
        self.assertNotIn('BuildInput', report); self.assertFalse((self.output/'candidate.json').exists())
        self.assertEqual(report['Files'][0]['File'], '01-base.apk')
        for file in report['Files']:
            self.assertTrue((self.output/file['File']).is_file())

    def test_update_during_pull_does_not_produce_success_report(self):
        with self.assertRaises(collector.CollectionError):
            collector.collect(FakeAdb(changed=True), PACKAGE, self.output)
        self.assertFalse((self.output/'collection.json').exists())

    def test_wrong_package_or_mixed_versions_do_not_produce_success(self):
        for apks in [{BASE: apk_bytes(package='com.other.game')},
                     {BASE: apk_bytes(version=734), SPLIT: apk_bytes(version=735)}]:
            output = self.output / str(len(list(self.output.glob('*'))) if self.output.exists() else 0)
            with self.assertRaises(collector.CollectionError):
                collector.collect(FakeAdb(apks), PACKAGE, output)
            self.assertFalse((output/'collection.json').exists())

    def test_prior_files_are_never_overwritten(self):
        self.output.mkdir(); original = self.output/'keep.txt'; original.write_text('keep')
        with self.assertRaises(FileExistsError):
            collector.collect(FakeAdb(), PACKAGE, self.output)
        self.assertEqual(original.read_text(), 'keep')

    def test_unsafe_duplicate_or_unexpected_remote_paths_are_rejected(self):
        for response in ['package:/data/app/../base.apk', 'package:/data/app/a.apk;reboot',
                         'package:/data/app/a\nb.apk', 'package:/sdcard/base.apk',
                         'package:'+BASE+'\npackage:'+BASE, 'Error: missing package']:
            with self.assertRaises(collector.CollectionError, msg=response):
                collector.apk_paths(response)
        self.assertEqual(collector.apk_paths('package:/data/app/ABC/Base.apk'), ['/data/app/ABC/Base.apk'])

    def test_unauthorized_or_ambiguous_device_is_not_selected(self):
        client = Mock(executable='adb.exe')
        for response in ['List of devices attached\nprivate-serial\tunauthorized\n',
                         'List of devices attached\nfirst\tdevice\nsecond\tdevice\n']:
            client.run.return_value = response
            with self.assertRaises(collector.CollectionError) as error:
                collector.select_device(client, None)
            self.assertNotIn('private-serial', str(error.exception))

    def test_package_arguments_cannot_inject_shell_commands(self):
        client = Mock()
        for package in ['com.game;reboot', 'com.game $(id)', '-x', 'com/game']:
            with self.assertRaises(collector.CollectionError):
                collector.find_package(client, package)
        client.run.assert_not_called()


if __name__ == '__main__':
    unittest.main()
