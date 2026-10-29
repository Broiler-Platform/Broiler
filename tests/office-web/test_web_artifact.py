"""Producer archive integrity and failure-path tests; no network or app rebuilds."""
import base64
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("web_artifact", ROOT / "eng/web-producer/artifact.py")
artifact = importlib.util.module_from_spec(spec)
spec.loader.exec_module(artifact)


class ProducerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.web = self.root / "wwwroot"
        (self.web / "_framework").mkdir(parents=True)
        files = {"main.hash.js": b"import './_framework/dotnet.js';", "_framework/dotnet.js": b"runtime",
            "_framework/App.hash.wasm": b"\0asm", "broiler.graphics.webassembly.js": b"replay",
            "fonts/font.woff2": b"font", "media/picture.png": b"picture", ".nojekyll": b"", "main.hash.js.br": b"compressed"}
        for name,data in files.items():
            path = self.web / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        mapping = {"imports":{"./main.js":"./main.hash.js"},"integrity":{"./main.js":"sha256-"+base64.b64encode(hashlib.sha256(files["main.hash.js"]).digest()).decode()}}
        self.html = '<script type="importmap">'+json.dumps(mapping)+'</script><script type="module" src="main.hash.js"></script>'
        (self.web / "index.html").write_text(self.html)
        self.identity = dict(product="writer",productName="Writer",repository="Broiler-Platform/Broiler.Writer",version="0.1.0-preview.1",tag="writer-web-v0.1.0-preview.1",commit="a"*40,
            sdk=artifact.SDK,workload=artifact.WORKLOAD,workloadSet="10.403.0",runtimeMode=artifact.MODE,publishTrimmed=True,runAotCompilation=False,dirty=False,verificationOnly=False)

    def package(self, identity=None):
        metadata = artifact.pack(self.web, self.root / "release", copy.deepcopy(identity or self.identity))
        manifest = json.loads(metadata.read_text())
        return metadata.parent / manifest["archive"], metadata

    def rewrite(self, archive, metadata, entries):
        with zipfile.ZipFile(archive,"w",zipfile.ZIP_DEFLATED) as target:
            for name,data in entries:
                target.writestr(name,data)
        manifest=json.loads(metadata.read_text())
        manifest.update(archiveSize=archive.stat().st_size,archiveSha256=artifact.digest(archive.read_bytes()))
        metadata.write_text(json.dumps(manifest))

    def test_complete_bytes_survive_archive_and_extraction(self):
        archive,metadata=self.package()
        extracted=self.root / "extracted"
        artifact.verify(archive,metadata,extracted,require_release=True)
        self.assertEqual(artifact.tree(self.web),artifact.tree(extracted / "wwwroot"))

    def test_no_overwrite(self):
        self.package()
        with self.assertRaises(ValueError): self.package()

    def test_missing_entry_fails(self):
        (self.web / "main.hash.js").unlink()
        with self.assertRaises(ValueError): self.package()

    def test_fingerprinted_runtime_alias_is_preserved(self):
        (self.web/'_framework/dotnet.js').rename(self.web/'_framework/dotnet.hash.js')
        mapping={"imports":{"./_framework/dotnet.js":"./_framework/dotnet.hash.js"}}
        (self.web/'index.html').write_text('<script type="importmap">'+json.dumps(mapping)+'</script><script type="module" src="main.hash.js"></script>')
        archive,metadata=self.package()
        artifact.verify(archive,metadata)
        self.assertFalse((self.web/'_framework/dotnet.js').exists())

    def test_unresolved_fingerprint_fails(self):
        (self.web / "index.html").write_text(self.html + '#[.{fingerprint}]')
        with self.assertRaises(ValueError): self.package()

    def test_integrity_mismatch_fails(self):
        (self.web / "main.hash.js").write_bytes(b"changed")
        with self.assertRaises(ValueError): self.package()

    def test_archive_tampering_fails(self):
        archive,metadata=self.package()
        archive.write_bytes(archive.read_bytes()+b"tamper")
        with self.assertRaises(ValueError): artifact.verify(archive,metadata)

    def test_per_file_digest_still_checked_after_outer_digest_change(self):
        archive,metadata=self.package()
        with zipfile.ZipFile(archive) as source: entries=[(name,source.read(name))for name in source.namelist()]
        entries=[(name,b"replaY" if name.endswith('broiler.graphics.webassembly.js') else data)for name,data in entries]
        self.rewrite(archive,metadata,entries)
        with self.assertRaises(ValueError): artifact.verify(archive,metadata)

    def test_traversal_fails(self):
        archive,metadata=self.package()
        self.rewrite(archive,metadata,[("wwwroot/../../escape",b"escape")])
        with self.assertRaises(ValueError): artifact.verify(archive,metadata)

    def test_missing_inventory_members_fail(self):
        archive,metadata=self.package()
        with zipfile.ZipFile(archive) as source: entries=[(name,source.read(name))for name in source.namelist()if not name.endswith('.woff2')]
        self.rewrite(archive,metadata,entries)
        with self.assertRaises(ValueError): artifact.verify(archive,metadata)

    def test_aot_cannot_masquerade_as_initial_cohort(self):
        self.identity["runAotCompilation"]=True
        with self.assertRaises(ValueError): self.package()

    def test_source_identity_must_match_release(self):
        archive,metadata=self.package()
        with self.assertRaises(ValueError): artifact.verify(archive,metadata,expected={"commit":"b"*40})

    def test_full_release_keeps_web_zip_bytes_and_attaches_descriptor(self):
        bash = "C:/Program Files/Git/bin/bash.exe" if os.name == 'nt' else shutil.which('bash')
        if not bash or not Path(bash).is_file(): self.skipTest('Bash required for release-script fixture')
        for name,product,slug in [('Writer','writer','writer'),('Cells','cell','cells')]:
            with self.subTest(product=product):
                fixture=self.root/name;inputs=fixture/'inputs';bin_dir=fixture/'bin';bin_dir.mkdir(parents=True)
                version=self.identity['version'];identity=copy.deepcopy(self.identity)
                identity.update(product=product,productName=name,repository='Broiler-Platform/Broiler.'+name,tag=slug+'-v'+version,
                    commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT/f'Broiler.{name}',text=True).strip())
                web=inputs/f'broiler-{slug}-web-{version}';metadata=artifact.pack(self.web,web,identity)
                original=json.loads(metadata.read_text())['archiveSha256']
                for rid in ['win-x64','linux-x64']:
                    executable=f'Broiler.{name}.Windows.exe' if rid=='win-x64' else f'Broiler.{name}.Linux'
                    for variant in ['self-contained','framework-dependent']:
                        source=inputs/f'broiler-{slug}-{rid}-{variant}-{version}';source.mkdir(parents=True);(source/executable).write_bytes(b'fixture')
                capture=fixture/'capture.py';log=fixture/'gh.json'
                capture.write_text('import sys,json,hashlib,os\nfrom pathlib import Path\na=sys.argv[1:]\nassert a[:2]==["release","create"]\np=next(Path(x) for x in a if x.endswith("-browser-wasm.zip"))\nPath(os.environ["MOCK_GH_LOG"]).write_text(json.dumps({"sha":hashlib.sha256(p.read_bytes()).hexdigest(),"descriptor":any(x.endswith(".web-artifact.json") for x in a)}))\n')
                for command,body in [('python3',f'exec "{Path(sys.executable).as_posix()}" "$@"'),('gh',f'exec "{Path(sys.executable).as_posix()}" "{capture.as_posix()}" "$@"')]:
                    shim=bin_dir/command;shim.write_text('#!/usr/bin/env bash\n'+body+'\n');shim.chmod(0o755)
                env={**os.environ,'PATH':str(bin_dir)+os.pathsep+os.environ['PATH'],'MOCK_GH_LOG':str(log),'GH_TOKEN':'fixture-not-a-token','GITHUB_REPOSITORY':identity['repository']}
                result=subprocess.run([bash,'eng/release-draft.sh',version,inputs.as_posix()],cwd=ROOT/f'Broiler.{name}',env=env,capture_output=True,text=True)
                self.assertEqual(result.returncode,0,result.stdout+result.stderr)
                self.assertEqual(json.loads(log.read_text()),{'sha':original,'descriptor':True})

    def test_dirty_release_fails_but_local_verification_is_explicit(self):
        self.identity["dirty"]=True
        with self.assertRaises(ValueError): self.package()
        self.identity["verificationOnly"]=True
        archive,metadata=self.package()
        self.assertFalse(json.loads(metadata.read_text())["releaseEligible"])
        with self.assertRaises(ValueError): artifact.verify(archive,metadata,require_release=True)

    def test_vendored_producers_match_canonical_code(self):
        for name in ("Writer","Cells","Launcher"):
            for filename in ("artifact.py","test_artifact.py","publish.ps1","smoke.mjs","global.json","package.json","package-lock.json"):
                self.assertEqual((ROOT/'eng/web-producer'/filename).read_bytes(),(ROOT/f'Broiler.{name}/eng/web'/filename).read_bytes(),name+'/'+filename)

if __name__ == "__main__": unittest.main()
