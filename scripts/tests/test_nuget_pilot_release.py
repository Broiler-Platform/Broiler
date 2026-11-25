import importlib.util
import copy
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from zipfile import ZipFile

SCRIPT = Path(__file__).resolve().parents[2] / "Broiler.DateTime/eng/nuget_pilot_release.py"
SPEC = importlib.util.spec_from_file_location("nuget_pilot_release", SCRIPT)
release = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(release)


class LocalFeed:
    def __init__(self):
        self.available = {}
        self.hidden = set()
        self.pushes = []

    def find(self, package):
        return None if package["id"] in self.hidden else self.available.get(package["id"])

    def push(self, package):
        identity = package["id"]
        self.pushes.append(identity)
        self.available[identity] = identity.encode()

    def verify(self, package, served):
        if served != package["id"].encode():
            raise ValueError("Public package payload differs from the approved candidate")
        return {"source": "local rehearsal"}


def package(identity, *prerequisites):
    return {"id": identity, "version": "0.1.0-preview.2", "prerequisites": prerequisites}


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        temporary = TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.state = self.root / "state.json"
        self.feed = LocalFeed()
        self.plan = {"sourceRun": 123, "packages": [package("Base"), package("Consumer", "Base")]}

    def run_plan(self):
        passed = release.promote(self.plan, self.feed, self.state, "test-digest", checks=2, pause=0)
        return passed, json.loads(self.state.read_text())["packages"]

    def test_interrupted_upload_blocks_dependent_and_resumes_exact_existing_payload(self):
        self.feed.hidden.add("Base")
        passed, states = self.run_plan()
        self.assertFalse(passed)
        self.assertEqual(states["base"]["state"], "awaiting-index")
        self.assertEqual(states["consumer"]["state"], "blocked")
        self.assertEqual(self.feed.pushes, ["Base"])

        self.feed.hidden.clear()
        passed, states = self.run_plan()
        self.assertTrue(passed)
        self.assertTrue(states["base"]["alreadyPresent"])
        self.assertFalse(states["base"]["uploadAttempted"])
        self.assertEqual(self.feed.pushes, ["Base", "Consumer"])

        self.assertTrue(self.run_plan()[0])
        self.assertEqual(self.feed.pushes, ["Base", "Consumer"])

    def test_conflicting_duplicate_blocks_prerequisite(self):
        self.feed.available["Base"] = b"unexpected payload"
        passed, states = self.run_plan()
        self.assertFalse(passed)
        self.assertEqual(states["base"]["state"], "failed")
        self.assertEqual(states["consumer"]["state"], "blocked")
        self.assertEqual(self.feed.pushes, [])

    def test_release_order_is_checked_before_any_upload(self):
        self.plan["packages"].reverse()
        with self.assertRaisesRegex(ValueError, "prerequisite-first"):
            self.run_plan()
        self.assertEqual(self.feed.pushes, [])

    def test_status_alone_cannot_approve_another_revision(self):
        review = self.root / "review.md"
        approved = "a" * 40
        review.write_text(f"> **Status: APPROVED FOR PREVIEW.**\n- **Commit:** \x60{approved}\x60\n- **Reviewer:** Pilot fixture\n")
        release.require_review(review, approved)
        with self.assertRaisesRegex(ValueError, "exact source commit"):
            release.require_review(review, "b" * 40)
        review.write_text(review.read_text().replace("APPROVED FOR PREVIEW", "PENDING"))
        with self.assertRaisesRegex(ValueError, "exact source commit"):
            release.require_review(review, approved)

    def test_repository_signature_preserves_comparable_content(self):
        local, public = self.root / "local.nupkg", self.root / "public.nupkg"
        for file in (local, public):
            with ZipFile(file, "w") as archive:
                archive.writestr("a.nuspec", "same manifest")
                types = '<Default Extension="xml" ContentType="text/xml"/>'
                if file == public:
                    archive.writestr(".signature.p7s", "fake envelope")
                    types = '<Default Extension="p7s" ContentType="application/octet-stream"/>' + types
                archive.writestr("[Content_Types].xml", f"<Types>{types}</Types>")
        self.assertEqual(release.payload(local), release.payload(public))
        with ZipFile(public, "a") as archive:
            archive.writestr("changed.dll", b"new bytes")
        self.assertNotEqual(release.payload(local), release.payload(public))

    def test_non_release_matrix_cannot_be_prepared(self):
        reports = [{"profile": {"os": system}, "status": "passed", "dateTimeReleaseIntent": False}
                   for system in ("Windows", "Linux")]
        with self.assertRaisesRegex(ValueError, "explicitly staged release"):
            release.pick_candidate(reports)
        with self.assertRaisesRegex(ValueError, "Windows and Linux"):
            release.pick_candidate(reports[:1])

    def test_validated_linux_artifact_is_bound_to_both_profile_identities(self):
        identity, version, commit = release.ID, "0.1.0-preview.3", "a" * 40
        package = {"id": identity, "version": version, "dependencyGroups": {"net10.0": {}},
                   "file": f"{identity}.{version}.nupkg", "symbolsFile": f"{identity}.{version}.snupkg",
                   "sha512": "validated"}
        probe = {"consumer": identity, "status": "passed", "resolved": [
            {"package": identity, "version": version, "sha512": "validated", "source": "candidate-feed"}]}
        row = {"profile": {"os": "Windows"}, "status": "passed", "dateTimeReleaseIntent": True, "edits": {},
               "negativeControls": [{"check": key, "rejected": True} for key in release.REQUIRED_CONTROLS],
               "packageConsumers": [probe] + [{"consumer": str(i), "status": "passed"} for i in range(4)],
               "producers": {identity: {"tests": 1, "commit": commit, "packages": [package], "version": version}}}
        linux = copy.deepcopy(row)
        linux["profile"]["os"] = "Linux"
        linux["producers"][identity]["packages"][0]["sha256"] = "linux archive"
        self.assertEqual(release.pick_candidate([row, linux])[1]["sha256"], "linux archive")
        linux["producers"][identity]["commit"] = "b" * 40
        with self.assertRaisesRegex(ValueError, "disagree"):
            release.pick_candidate([row, linux])


if __name__ == "__main__":
    unittest.main()
