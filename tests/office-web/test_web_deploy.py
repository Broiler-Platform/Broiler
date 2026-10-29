"""Web lock, staging, host verification and reversible promotion failure paths."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from unittest.mock import MagicMock
from email.message import Message
import base64

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("office_web", ROOT / "scripts/office_web.py")
web = importlib.util.module_from_spec(spec)
spec.loader.exec_module(web)


def lock(version="0.1.0-preview.1", catalog="office-demo-1"):
    rows = []
    for index, (product, (repo, host)) in enumerate(web.PRODUCTS.items()):
        rows.append(dict(product=product, repository=repo, version=version, tag=product + "-web-v" + version,
                         sdk=web.producer.SDK, workload=web.producer.WORKLOAD, workloadSet="10.403.0", runtimeMode=web.producer.MODE,
                         replayProtocol="broiler.canvas/1", inputProtocol="broiler.host/1" if product=="launcher" else product+".browser/1",
                         commit="a" * 40, releaseId=index + 1, entryUrl=f"https://{host}/releases/{version}/",
                         archive=dict(id=10 + index, name=product + ".zip", size=1, sha256="b" * 64),
                         metadata=dict(id=20 + index, name=product + ".json", size=1, sha256="c" * 64)))
    return dict(schemaVersion=1, artifactKind="broiler-office-web-lock", status="locked", channel="preview", catalogVersion=catalog, components=rows)


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.site = Path(self.temp.name) / "site"
        self.site.mkdir()

    def test_pending_lock_cannot_deploy(self):
        with self.assertRaises(ValueError):
            web.validate({"schemaVersion": 1, "artifactKind": "broiler-office-web-lock", "status": "pending-producer-releases", "components": []})

    def test_exact_three_products_and_origins(self):
        self.assertEqual(web.validate(lock())["channel"], "preview")
        for change in (lambda l: l["components"].pop(),
                       lambda l: l["components"][0].update(entryUrl="https://evil.example/"),
                       lambda l: l["components"][1].update(version="../bad"),
                       lambda l: l["components"][2].update(repository="other/repository"),
                       lambda l: l["components"][0]["archive"].update(id=True),
                       lambda l: l["components"][0]["metadata"].update(sha256="unknown")):
            candidate = lock()
            change(candidate)
            with self.assertRaises(ValueError):
                web.validate(candidate)

    def test_github_credentials_are_removed_on_asset_host_redirect(self):
        captured=[]
        response=MagicMock()
        response.__enter__.return_value.read.return_value=b'{}'
        opener=MagicMock()
        opener.open.return_value=response
        def build(handler):
            captured.append(handler)
            return opener
        with patch.dict(web.os.environ,{'GH_TOKEN':'test-fixture-token'}), patch.object(web.urllib.request,'build_opener',side_effect=build):
            web.fetch('https://api.github.com/repos/example/project/releases/1')
        request=opener.open.call_args.args[0]
        self.assertEqual(request.get_header('Authorization'),'Bearer test-fixture-token')
        redirected=captured[0].redirect_request(request,None,302,'Found',{},'https://release-assets.githubusercontent.com/file')
        self.assertIsNone(redirected.get_header('Authorization'))
        with self.assertRaises(ValueError):
            captured[0].redirect_request(request,None,302,'Found',{},'http://example.com/file')

    def test_host_health_checks_every_producer_byte(self):
        candidate = lock()
        data = b"published bytes"
        inventory = [dict(path="wwwroot/index.html", size=len(data), sha256=web.digest(data))]
        receipts = {}
        for row in candidate["components"]:
            receipts[row["entryUrl"].split('/releases/')[0] + '/receipts/' + row['version'] + '.json'] = json.dumps(dict(component=row, runtimeMode=web.producer.MODE, inputProtocol=row['inputProtocol'], replayProtocol=row['replayProtocol'], files=inventory)).encode()
        def fetch(url, *args):
            return receipts.get(url, data)
        response=MagicMock()
        response.headers=Message()
        response.headers['Content-Type']='text/html'
        def head(request, **kwargs):
            response.geturl.return_value=request.full_url
            manager=MagicMock()
            manager.__enter__.return_value=response
            return manager
        with patch.object(web, "fetch", side_effect=fetch), patch.object(web, "obtain", return_value={"files": inventory}), patch.object(web.urllib.request,'urlopen',side_effect=head):
            ledger = web.health(candidate)
            self.assertEqual(ledger["disabledProducts"], ["browser", "plate"])
            receipts[candidate["components"][1]["entryUrl"] + "index.html"] = b"tampered"
            with self.assertRaises(ValueError):
                web.health(candidate)

    def test_first_promotion_and_catalog_only_rollback(self):
        first, second = lock(), lock("0.2.0-preview.1", "office-demo-2")
        retained = self.site / 'releases' / '0.1.0-preview.1' / 'index.html'
        retained.parent.mkdir(parents=True)
        retained.write_bytes(b"immutable application")
        with patch.object(web, 'health', side_effect=lambda l: {"status": "staged", "components": l['components']}):
            web.promote(first, self.site, 'none')
            old = (self.site / 'office-web.json').read_bytes()
            web.promote(second, self.site, web.digest(old))
            self.assertIn('0.2.0-preview.1', (self.site / 'office-web.json').read_text())
            web.promote(first, self.site, web.digest((self.site / 'office-web.json').read_bytes()))
            self.assertEqual(old, (self.site / 'office-web.json').read_bytes())
        self.assertEqual(retained.read_bytes(), b"immutable application")
        self.assertEqual(len(list((self.site / 'catalogs').glob('*.json'))), 2)

    def test_failed_health_and_stale_pointer_leave_approval_unchanged(self):
        pointer = self.site / 'office-web.json'
        pointer.write_bytes(b"previous approved bytes")
        with patch.object(web, 'health', side_effect=ValueError('unhealthy Writer')):
            with self.assertRaises(ValueError):
                web.promote(lock(), self.site, web.digest(pointer.read_bytes()))
        with patch.object(web, 'health', return_value={}):
            with self.assertRaises(ValueError):
                web.promote(lock(), self.site, 'none')
        self.assertEqual(pointer.read_bytes(), b"previous approved bytes")

    def test_existing_version_cannot_be_overwritten(self):
        destination = self.site / 'releases' / lock()['components'][0]['version']
        destination.mkdir(parents=True)
        (destination / 'index.html').write_bytes(b"original")
        with patch.object(web, 'obtain', return_value={"files": [dict(path='wwwroot/index.html', sha256=web.digest(b'changed'))]}):
            with self.assertRaises(ValueError):
                web.stage(lock(), 'launcher', self.site, self.site / 'cache')
        self.assertEqual((destination / 'index.html').read_bytes(), b'original')

    def test_immutable_receipt_is_bound_to_exact_lock(self):
        destination = self.site / 'releases' / lock()['components'][0]['version']
        destination.mkdir(parents=True)
        (destination / 'index.html').write_bytes(b'original')
        receipt = self.site / 'receipts' / (lock()['components'][0]['version'] + '.json')
        web.write(receipt, {"different": "source"})
        with patch.object(web, 'obtain', return_value={"runtimeMode": web.producer.MODE, "files": [dict(path='wwwroot/index.html', sha256=web.digest(b'original'))]}):
            with self.assertRaises(ValueError):
                web.stage(lock(), 'launcher', self.site, self.site / 'cache')
        self.assertEqual(web.read(receipt), {"different": "source"})

    def test_deployment_tools_and_workflows_are_vendored_identically(self):
        for directory in ('eng/web-deploy', 'Broiler.Writer/eng/web-deploy', 'Broiler.Cells/eng/web-deploy'):
            if not (ROOT / directory).exists():
                continue  # Artifact-only CI deliberately does not initialize application submodules.
            self.assertEqual((ROOT / directory / 'office_web.py').read_bytes(), (ROOT / 'scripts/office_web.py').read_bytes())
            self.assertEqual((ROOT / directory / 'artifact.py').read_bytes(), (ROOT / 'eng/web-producer/artifact.py').read_bytes())
        for prefix in ('', 'Broiler.Writer/', 'Broiler.Cells/'):
            if prefix and not (ROOT / prefix / '.github/workflows').exists():
                continue
            self.assertEqual((ROOT / prefix / '.github/workflows/office-web-pages.yml').read_bytes(), (ROOT / 'eng/web-producer/pages.yml').read_bytes())

    def test_released_archive_round_trip_through_stage_without_rebuild(self):
        source = Path(self.temp.name) / 'producer'
        (source / '_framework').mkdir(parents=True)
        entry=b'export const officeInputProtocolVersion = 1;'
        (source/'main.hash.js').write_bytes(entry)
        (source/'_framework/dotnet.js').write_bytes(b'runtime')
        (source/'_framework/app.wasm').write_bytes(b'\x00asm')
        (source/'broiler.graphics.webassembly.js').write_bytes(b'replay')
        mapping={'imports':{'./main.js':'./main.hash.js'},'integrity':{'./main.js':'sha256-'+base64.b64encode(__import__('hashlib').sha256(entry).digest()).decode()}}
        (source/'index.html').write_text('<script type="importmap">'+json.dumps(mapping)+'</script><script type="module" src="main.hash.js"></script>')
        identity=dict(product='writer',productName='Writer',repository=web.PRODUCTS['writer'][0],version='0.1.0-preview.1',tag='writer-web-v0.1.0-preview.1',commit='a'*40,
                      sdk=web.producer.SDK,workload=web.producer.WORKLOAD,workloadSet='10.403.0',runtimeMode=web.producer.MODE,publishTrimmed=True,runAotCompilation=False,dirty=False,verificationOnly=False,
                      inputProtocol='writer.browser/1',replayProtocol='broiler.canvas/legacy-preview14',dependencies=['Broiler.Graphics.WebAssembly/0.1.0-preview.14'])
        metadata=web.producer.pack(source,Path(self.temp.name)/'release',identity)
        manifest=web.read(metadata)
        archive=metadata.parent/manifest['archive']
        candidate=lock()
        row=candidate['components'][1]
        row['replayProtocol']=identity['replayProtocol']
        release_assets=[]
        responses={}
        for kind,path in (('archive',archive),('metadata',metadata)):
            data=path.read_bytes()
            row[kind].update(name=path.name,size=len(data),sha256=web.digest(data))
            asset={**row[kind],'url':'https://api.github.com/fixture/'+kind}
            release_assets.append(asset)
            responses[asset['url']]=data
        release=dict(draft=False,tag_name=row['tag'],assets=release_assets)
        responses[f'https://api.github.com/repos/{row["repository"]}/releases/{row["releaseId"]}']=json.dumps(release).encode()
        with patch.object(web,'fetch',side_effect=lambda url,*args:responses[url]):
            web.stage(candidate,'writer',self.site,Path(self.temp.name)/'cache')
            web.stage(candidate,'writer',self.site,Path(self.temp.name)/'cache')
            self.assertEqual((self.site/'releases'/row['version']/'main.hash.js').read_bytes(),entry)
            self.assertEqual(web.read(self.site/'receipts'/(row['version']+'.json'))['component'],row)
            # A release asset cannot be silently replaced even at the same ID.
            responses[release_assets[0]['url']]=b'tampered'
            with self.assertRaises(ValueError):
                web.obtain(row,Path(self.temp.name)/'new-cache')


if __name__ == '__main__':
    unittest.main()
