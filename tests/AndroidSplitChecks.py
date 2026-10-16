"""Input integrity checks for split APK staging; APKEditor is checked on a real local input."""
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'scripts/android'))
import stage_splits
from AndroidCollectionChecks import apk_bytes

class AndroidSplitStaging(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(dir=os.environ['TEMP'],prefix='DivineLabs-split-checks-')
        self.root=Path(self.temp.name);self.source=self.root/'input.xapk';self.output=self.root/'output'
    def tearDown(self):
        self.temp.cleanup()
    def bundle(self,parts=None,declared=None,extra=None):
        parts=parts or {'base.apk':apk_bytes(full=True),'config.hdpi.apk':apk_bytes()}
        with zipfile.ZipFile(self.source,'w') as z:
            for name,data in parts.items():z.writestr(name,data)
            if declared is not None:z.writestr('manifest.json',json.dumps({'split_apks':[{'file':name} for name in declared]}))
            for name,data in (extra or {}).items():z.writestr(name,data)
    def test_valid_bundle_keeps_source_and_stages_exact_bytes(self):
        self.bundle();before=self.source.read_bytes()
        report=stage_splits.stage(self.source,self.output)
        self.assertFalse(report['Approved']);self.assertEqual(len(report['Files']),2)
        self.assertEqual(self.source.read_bytes(),before)
        with zipfile.ZipFile(self.source) as z:self.assertEqual((self.output/'000.apk').read_bytes(),z.read('base.apk'))
    def test_missing_declared_part_is_rejected(self):
        self.bundle(declared=['base.apk','config.hdpi.apk','UnityDataAssetPack.apk'])
        with self.assertRaises(stage_splits.InvalidApk):stage_splits.stage(self.source,self.output)
    def test_mixed_versions_are_rejected(self):
        self.bundle(parts={'base.apk':apk_bytes(full=True),'config.hdpi.apk':apk_bytes(version=735)})
        with self.assertRaises(stage_splits.InvalidApk):stage_splits.stage(self.source,self.output)
    def test_duplicate_native_component_is_rejected(self):
        self.bundle(parts={'base.apk':apk_bytes(full=True),'other.apk':apk_bytes(full=True)})
        with self.assertRaises(stage_splits.InvalidApk):stage_splits.stage(self.source,self.output)
    def test_unsafe_archive_path_is_rejected_before_extracting(self):
        self.bundle(extra={'../outside.txt':b'bad'})
        with self.assertRaises(stage_splits.InvalidApk):stage_splits.stage(self.source,self.output)
        self.assertFalse((self.root/'outside.txt').exists());self.assertEqual(list(self.output.iterdir()),[])
    def test_modified_phone_collection_is_rejected(self):
        folder=self.root/'phone';folder.mkdir();(folder/'01-base.apk').write_bytes(apk_bytes(full=True))
        (folder/'collection.json').write_text(json.dumps({'Complete':True,'Files':[{'File':'01-base.apk','Sha256':'0'*64}]}))
        with self.assertRaises(stage_splits.InvalidApk):stage_splits.stage(folder,self.output)
    def test_existing_output_is_preserved(self):
        self.bundle();self.output.mkdir();(self.output/'keep').write_text('keep')
        with self.assertRaises(stage_splits.InvalidApk):stage_splits.stage(self.source,self.output)
        self.assertEqual((self.output/'keep').read_text(),'keep')

if __name__=='__main__':unittest.main()
