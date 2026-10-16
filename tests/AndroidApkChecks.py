import importlib.util
import io
import os
from pathlib import Path
import struct
import sys
import tempfile
import unittest
import warnings
import zipfile

source = Path(__file__).resolve().parents[1] / 'scripts' / 'android' / 'inspect_apk.py'
spec = importlib.util.spec_from_file_location('inspect_apk', source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
sys.modules['inspect_apk']=module
prepare_spec=importlib.util.spec_from_file_location('prepare_apk', source.with_name('prepare_apk.py'))
preparation=importlib.util.module_from_spec(prepare_spec);prepare_spec.loader.exec_module(preparation)

def axml(utf8=True):
    strings = ['manifest','package','com.pabloleban.IdleSlayer','http://schemas.android.com/apk/res/android','versionName','7.3.4','versionCode','uses-sdk','minSdkVersion','targetSdkVersion']
    offsets, body = [], bytearray()
    for value in strings:
        offsets.append(len(body))
        if utf8:
            raw = value.encode('utf-8'); body += bytes([len(value),len(raw)])+raw+b'\0'
        else:
            body += struct.pack('<H',len(value))+value.encode('utf-16-le')+b'\0\0'
    start = 28+len(strings)*4
    pool = struct.pack('<HHIIIIII',1,28,start+len(body),len(strings),0,0x100 if utf8 else 0,start,0)+struct.pack('<'+'I'*len(strings),*offsets)+body
    def element(name, attrs):
        items = b''.join(struct.pack('<IIIHBBI',namespace,key,0xffffffff,8,0,kind,value) for namespace,key,kind,value in attrs)
        return struct.pack('<HHIIIIIHHHHHH',0x102,16,36+len(items),1,0xffffffff,0xffffffff,name,20,20,len(attrs),0,0,0)+items
    main = element(0,[(0xffffffff,1,3,2),(3,4,3,5),(3,6,0x10,734)])
    sdk = element(7,[(3,8,0x10,26),(3,9,0x10,35)])
    end = lambda name: struct.pack('<HHIIIII',0x103,16,24,1,0xffffffff,0xffffffff,name)
    chunks = pool+main+sdk+end(7)+end(0)
    return struct.pack('<HHI',3,8,len(chunks)+8)+chunks

class ApkInspection(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(dir=os.environ['TEMP'],prefix='DivineLabs-android-checks-')
        self.path = Path(self.temp.name)/'fixture.apk'
    def tearDown(self):
        self.temp.cleanup()
    def make(self,extra=None,elf=None):
        header = bytearray(64);header[:6]=b'\x7fELF\x02\x01';struct.pack_into('<H',header,18,183)
        entries={'AndroidManifest.xml':axml(),module.NATIVE:elf if elf is not None else bytes(header),module.METADATA:struct.pack('<II',0xfab11baf,39),'assets/bin/Data/globalgamemanagers':b'\0Unity 6000.3.14f1\0'}
        entries.update(extra or {})
        with zipfile.ZipFile(self.path,'w') as apk:
            for key,value in entries.items():apk.writestr(key,value)
    def test_binary_utf8_and_utf16(self):
        for utf8 in [True,False]:
            value=module.manifest_values(axml(utf8));self.assertEqual(value['Package'],'com.pabloleban.IdleSlayer');self.assertEqual(value['VersionName'],'7.3.4');self.assertEqual(value['VersionCode'],734);self.assertEqual(value['MinSdk'],26)
    def test_candidate_does_not_approve_or_modify(self):
        self.make();before=self.path.read_bytes();value=module.inspect(self.path);self.assertFalse(value['Approved']);self.assertEqual(value['UnityVersion'],'6000.3.14f1');self.assertEqual(value['Abis'],['arm64-v8a']);self.assertEqual(self.path.read_bytes(),before)
    def test_raise_sdk_preserves_game_and_case(self):
        raw=axml();positions={};module.manifest_values(raw,positions);raw=bytearray(raw);struct.pack_into('<I',raw,positions['MinSdk']+16,23)
        self.make({'AndroidManifest.xml':bytes(raw),'assets/A':b'one','assets/a':b'two','META-INF/OLD.RSA':b'old signature','lib/armeabi-v7a/libunused.so':b'32-bit fixture'})
        before=self.path.read_bytes();output=self.path.with_name('prepared.apk');preparation.prepare(self.path,output)
        self.assertEqual(module.inspect(output)['MinSdk'],26);self.assertEqual(module.inspect(output)['Abis'],['arm64-v8a']);self.assertEqual(self.path.read_bytes(),before)
        with zipfile.ZipFile(output) as apk:self.assertEqual(apk.read('assets/A'),b'one');self.assertEqual(apk.read('assets/a'),b'two');self.assertNotIn('META-INF/OLD.RSA',apk.namelist())
        with self.assertRaises(module.InvalidApk):preparation.prepare(self.path,output)
    def test_wrong_abi(self):
        self.make(elf=b'\x7fELF\x01\x01'+b'\0'*58)
        with self.assertRaises(module.InvalidApk):module.inspect(self.path)
    def test_traversal_rejected(self):
        self.make({'../escape':b'x'})
        with self.assertRaises(module.InvalidApk):module.inspect(self.path)
    def test_duplicate_rejected(self):
        self.make()
        with warnings.catch_warnings():
            warnings.simplefilter('ignore')
            with zipfile.ZipFile(self.path,'a') as apk:apk.writestr(module.NATIVE,b'duplicate')
        with self.assertRaises(module.InvalidApk):module.inspect(self.path)
    def test_existing_loader_rejected(self):
        self.make({'assets/LemonLoader/payload.json':b'{}'})
        with self.assertRaises(module.InvalidApk):module.inspect(self.path)
    def test_nonstandard_metadata_rejected(self):
        self.make({module.METADATA:b'encrypted'})
        with self.assertRaises(module.InvalidApk):module.inspect(self.path)
    def test_case_sensitive_game_entries_are_not_extracted(self):
        self.make({'assets/A':b'one','assets/a':b'two'});self.assertFalse(module.inspect(self.path)['Approved'])
    def test_malformed_manifest_bounds(self):
        for value in [axml()[:20],b'\x03\x00\x08\x00\xff\xff\xff\xff']:
            with self.assertRaises(module.InvalidApk):module.manifest_values(value)

if __name__=='__main__':unittest.main()
