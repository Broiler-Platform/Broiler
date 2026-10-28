import copy
import importlib.util
import json
from pathlib import Path
import stat
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import office_artifacts as office


class OfficeArtifactsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="broiler-artifact-test-")
        self.root = Path(self.temp.name)
        self.cache = self.root / "cache"
        self.cache.mkdir()
        self.lock = dict(schemaVersion=1, suite="1.0.0-demo.9", channel="preview",
                         minimumLauncherVersion="0.1.0-demo.1", components=[])
        for index, (product, (repository, entry)) in enumerate(office.PRODUCTS.items(), 1):
            archive = self.root / (product + ".zip")
            with zipfile.ZipFile(archive, "w") as output:
                output.writestr(entry, ("unchanged-" + product).encode())
                if product == "boss":
                    output.writestr("wwwroot/index.html", "fixture")
            sha = office.digest(archive)
            archive.rename(self.cache / (sha + ".zip"))
            self.lock["components"].append(dict(id=product, version=f"0.{index}.0-preview.1",
                repository="Broiler-Platform/" + repository, tag=product + "-v0.1.0-preview.1",
                commit="a" * 40, releaseId=index, assetId=index + 100,
                asset=product + ".zip", size=(self.cache / (sha + ".zip")).stat().st_size,
                sha256=sha, rid="win-x64", archive="zip", entryPoint=entry, invocation="native",
                publishMode="self-contained", prerelease=True))
        self.lock_path = self.root / "office.lock.json"
        office.write_json(self.lock_path, self.lock)

    def tearDown(self):
        self.assertTrue(self.root.resolve().is_relative_to(Path(tempfile.gettempdir()).resolve()))
        self.assertTrue(self.root.name.startswith("broiler-artifact-test-"))
        self.temp.cleanup()

    def test_assembles_independent_versions_and_preserves_every_archive_byte(self):
        with patch.object(office, "api", side_effect=AssertionError("offline fixtures must not use the network")):
            archive = office.assemble(self.lock_path, self.root / "out", self.cache, "b" * 40, offline=True)
        self.assertTrue(archive.is_file())
        assets = archive.parent
        manifest = office.read_json(assets / "office-release.json")
        self.assertEqual(self.lock["suite"], manifest["officeVersion"])
        self.assertEqual(6, len({p["version"] for p in manifest["components"]}))
        for pin in self.lock["components"]:
            self.assertEqual(pin["sha256"], office.digest(assets / pin["asset"]))
            with zipfile.ZipFile(archive) as bundle:
                exact = bundle.read("Broiler-Office-1.0.0-demo.9-win-x64/packages/" + pin["asset"])
                self.assertEqual((self.cache / (pin["sha256"] + ".zip")).read_bytes(), exact)
        info = office.read_json(assets / "BUILD-INFO.json")
        self.assertTrue(info["artifactFirst"])
        self.assertTrue(info["offlineRehearsal"])
        self.assertEqual(office.digest(self.lock_path), info["lockSha256"])
        catalog = office.read_json(assets / "downloads.json")
        self.assertEqual("office-approved-lock", catalog["compatibilityPolicy"])
        self.assertEqual(office.digest(archive), catalog["suite"]["sha256"])
        self.assertEqual({p["version"] for p in self.lock["components"]}, {p["version"] for p in catalog["components"]})

    def test_missing_component_never_falls_back_to_build_or_latest(self):
        self.lock["components"].pop()
        with self.assertRaisesRegex(ValueError, "missing"):
            office.validate_lock(self.lock)
        office.validate_lock(self.lock, allow_incomplete=True)

    def test_duplicate_and_unknown_repository_are_rejected(self):
        self.lock["components"][0]["repository"] = "Other/Launcher"
        with self.assertRaisesRegex(ValueError, "repository"):
            office.validate_lock(self.lock)
        self.lock["components"][0] = copy.deepcopy(self.lock["components"][1])
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            office.validate_lock(self.lock)

    def test_stable_cannot_approve_preview_components(self):
        self.lock["suite"], self.lock["channel"] = "1.0.0", "stable"
        with self.assertRaisesRegex(ValueError, "Stable"):
            office.validate_lock(self.lock)

    def test_corrupted_cache_is_rejected_before_assembly(self):
        pin = self.lock["components"][0]
        with (self.cache / (pin["sha256"] + ".zip")).open("ab") as output:
            output.write(b"tamper")
        with self.assertRaisesRegex(ValueError, "Archive bytes"):
            office.assemble(self.lock_path, self.root / "out", self.cache, "b" * 40, offline=True)
        self.assertFalse((self.root / "out").exists())

    def malicious_zip(self, names, mode=None):
        archive = self.root / "malicious.zip"
        with zipfile.ZipFile(archive, "w") as output:
            for name in names:
                entry = zipfile.ZipInfo(name)
                if mode is not None:
                    entry.create_system = 3
                    entry.external_attr = mode << 16
                output.writestr(entry, "fixture")
        # Python's Windows ZIP writer normalizes backslashes; patch the fixture's
        # equal-length filename bytes to exercise an actual malicious ZIP.
        raw = archive.read_bytes()
        for name in names:
            if "\\" in name:
                raw = raw.replace(name.replace("\\", "/").encode(), name.encode())
        archive.write_bytes(raw)
        return archive

    def test_archive_escapes_aliases_and_receipts_are_rejected(self):
        for index, name in enumerate(("../outside", "C:/outside", "/outside", "a\\b", "a/../b", "a/./b", "a//b", "CON.exe", "a. /x", ".office-receipt.json", "a/.office-receipt.json")):
            with self.subTest(name=name), self.assertRaises(ValueError):
                office.extract(self.malicious_zip([name]), self.root / ("extract" + str(index)))

    def test_links_case_collisions_and_file_directory_collisions_are_rejected(self):
        with self.assertRaises(ValueError):
            office.extract(self.malicious_zip(["link"], stat.S_IFLNK | 0o777), self.root / "link-out")
        with self.assertRaises(ValueError):
            office.extract(self.malicious_zip(["a.dll", "A.dll"]), self.root / "case-out")
        with self.assertRaises(ValueError):
            office.extract(self.malicious_zip(["dir", "dir/child"]), self.root / "collision-out")

    def test_online_cache_still_verifies_release_ids_and_immutable_tag(self):
        pin = self.lock["components"][0]
        release = dict(draft=False, tag_name=pin["tag"], prerelease=True, assets=[dict(
            id=pin["assetId"], name=pin["asset"], size=pin["size"], digest="sha256:" + pin["sha256"],
            browser_download_url="https://github.com/" + pin["repository"] + "/releases/download/" + pin["tag"] + "/" + pin["asset"])])
        with patch.object(office, "api", return_value=release), patch.object(office, "resolve_commit", return_value=pin["commit"]):
            self.assertTrue(office.acquire(pin, self.cache).is_file())
        for changed in ("tag", "asset", "draft"):
            broken = copy.deepcopy(release)
            if changed == "draft": broken["draft"] = True
            if changed == "asset": broken["assets"][0]["id"] += 1
            with patch.object(office, "api", return_value=broken), patch.object(office, "resolve_commit", return_value="b" * 40 if changed == "tag" else pin["commit"]), self.assertRaises(ValueError):
                office.acquire(pin, self.cache)

    def test_no_product_compiler_or_latest_endpoint_in_assembler(self):
        source = Path(office.__file__).read_text()
        self.assertNotIn("subprocess", source)
        self.assertNotIn("releases/latest", source)
        self.assertNotIn("dotnet publish", source)

    def test_suite_envelope_is_reproducible_for_the_same_lock_and_integrator(self):
        first = office.assemble(self.lock_path, self.root / "first", self.cache, "b" * 40, offline=True)
        second = office.assemble(self.lock_path, self.root / "second", self.cache, "b" * 40, offline=True)
        self.assertEqual(office.digest(first), office.digest(second))

    def test_minimum_launcher_and_semantic_prerelease_comparison(self):
        self.assertGreater(office.compare_versions("1.0.0-preview.10", "1.0.0-preview.9"), 0)
        self.assertGreater(office.compare_versions("1.0.0", "1.0.0-preview.99"), 0)
        self.assertEqual(office.compare_versions("1.0.0+one", "1.0.0+two"), 0)
        self.lock["minimumLauncherVersion"] = "99.0.0"
        with self.assertRaisesRegex(ValueError, "launcher"):
            office.validate_lock(self.lock)

    def test_component_producer_manifest_hashes_existing_desktop_and_web_assets(self):
        path = Path(office.__file__).resolve().parent.parent / "eng/write-component-manifest.py"
        spec = importlib.util.spec_from_file_location("component_manifest", path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        output = self.root / "producer"
        output.mkdir()
        for name in ("Broiler.Writer-1.0.0-preview.1-win-x64-self-contained.zip", "Broiler.Writer-1.0.0-preview.1-browser-wasm.zip"):
            (output / name).write_bytes(b"existing archive bytes")
        args = [str(path), "--directory", str(output), "--product", "writer", "--version", "1.0.0-preview.1",
                "--repository", "Broiler-Platform/Broiler.Writer", "--tag", "writer-v1.0.0-preview.1", "--commit", "a" * 40, "--self-contained-mode", "native-aot"]
        with patch.object(sys, "argv", args):
            module.main()
        manifest = office.read_json(output / "component-release.json")
        self.assertEqual({"writer", "writer-web"}, {p["id"] for p in manifest["artifacts"]})
        for pin in manifest["artifacts"]:
            self.assertEqual(office.digest(output / pin["asset"]), pin["sha256"])
            self.assertEqual(b"existing archive bytes", (output / pin["asset"]).read_bytes())

    def test_producer_contract_is_identical_in_each_independent_repository(self):
        root = Path(office.__file__).resolve().parent.parent
        contract = (root / "eng/write-component-manifest.py").read_bytes()
        for repository in ("Broiler.Browser", "Broiler.Writer", "Broiler.Cells", "Broiler.Plate"):
            self.assertEqual(contract, (root / repository / "eng/write-component-manifest.py").read_bytes())


if __name__ == "__main__":
    unittest.main()
