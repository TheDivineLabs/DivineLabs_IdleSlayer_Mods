"""Focused native/payload checks: no runtime or game execution is simulated."""
import io
import json
import os
from pathlib import Path
import struct
import sys
import tempfile
import unittest
import zipfile
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'scripts/android'))
import audit_runtime as checker
from AndroidApkChecks import axml


def elf(alignment=16384, address=0, offset=0, relro=False):
    count=2 if relro else 1
    data=bytearray(64+count*56)
    data[:7]=b'\x7fELF\x02\x01\x01'
    struct.pack_into('<HH',data,16,3,183)
    struct.pack_into('<Q',data,32,64)
    struct.pack_into('<HH',data,54,56,count)
    struct.pack_into('<IIQQQQQQ',data,64,1,5,offset,address,0,0,0,alignment)
    if relro:struct.pack_into('<IIQQQQQQ',data,120,0x6474e552,4,0,1,0,0,1,1)
    return bytes(data)


class AndroidNativeChecks(unittest.TestCase):
    def test_valid_load_and_raw_relro_are_separate(self):
        data=elf(relro=True)
        result=checker.elf_segments(io.BytesIO(data),len(data))
        self.assertEqual(result['MinimumLoadAlignment'],16384)
        self.assertEqual(result['RelroRawEndAligned16KiB'],[False])
    def test_4k_or_misaligned_load_rejected(self):
        for data in [elf(alignment=4096),elf(address=4096),elf(alignment=24576)]:
            with self.assertRaises(checker.InvalidApk):checker.elf_segments(io.BytesIO(data),len(data))
    def test_truncated_or_outside_program_headers_rejected(self):
        for data in [elf()[:50],elf()[:70]]:
            with self.assertRaises(checker.InvalidApk):checker.elf_segments(io.BytesIO(data),len(data))
    def test_wrong_architecture_rejected(self):
        data=bytearray(elf());struct.pack_into('<H',data,18,62)
        with self.assertRaises(checker.InvalidApk):checker.elf_segments(io.BytesIO(data),len(data))
    def test_binary_and_plain_manifest_application_class(self):
        name='com.pairip.application.Application'
        for utf8 in (True,False):
            self.assertEqual(checker.manifest_values(axml(utf8,application=name))['ApplicationClass'],name)
        xml=f'<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="x"><application android:name="{name}" /></manifest>'
        self.assertEqual(checker.manifest_values(xml.encode())['ApplicationClass'],name)
    def test_payload_and_integrity_detection(self):
        with tempfile.TemporaryDirectory(dir=os.environ['TEMP'],prefix='DivineLabs-native-checks-') as temp:
            path=Path(temp)/'fixture.apk'
            with zipfile.ZipFile(path,'w') as apk:
                for name in checker.REQUIRED:apk.writestr(name,elf() if name.endswith('.so') else b'fixture')
                apk.writestr('lib/arm64-v8a/libpairipcore.so',elf())
                apk.writestr('AndroidManifest.xml',axml(application='com.pairip.application.Application'))
                apk.writestr('assets/LemonLoader/payload.json',json.dumps({'formatVersion':9,'runtimeRid':'android-arm64','deploymentFiles':[{'path':'Mods/DivineLabs.IdleSlayer.Mod.dll'},{'path':'UserLibs/DivineLabs.IdleSlayer.Contracts.dll'}]}))
            report=checker.audit(path)
            self.assertTrue(report['PairipApplicationDetected'])
            self.assertFalse(report['RuntimeVerified']);self.assertFalse(report['Physical16KiBDeviceVerified'])
    def test_missing_loader_is_rejected(self):
        with tempfile.TemporaryDirectory(dir=os.environ['TEMP'],prefix='DivineLabs-native-checks-') as temp:
            path=Path(temp)/'fixture.apk'
            with zipfile.ZipFile(path,'w') as apk:apk.writestr('AndroidManifest.xml',axml())
            with self.assertRaises(checker.InvalidApk):checker.audit(path)

if __name__=='__main__':unittest.main()
