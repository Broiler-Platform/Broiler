from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import patch
from zipfile import ZipFile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import nuget_cohorts as cohorts


def package(folder, identity, version, commit="a" * 40, dependency=None):
    path = folder / f"{identity}.{version}.nupkg"
    linked = (f'<dependencies><group targetFramework="net10.0">'
              f'<dependency id="{dependency[0]}" version="{dependency[1]}" />'
              f'</group></dependencies>') if dependency else ""
    with ZipFile(path, "w") as archive:
        archive.writestr(f"{identity}.nuspec",
                         f"<package><metadata><id>{identity}</id><version>{version}</version>"
                         f'<repository commit="{commit}" />{linked}</metadata></package>')
        archive.writestr(f"lib/net10.0/{identity}.dll", "fixture library")
    return path


def profile(deps=None):
    return {"configuration": "Release", "framework": "net10.0", "runtime": "",
            "includesBuildOutput": True, "includesSymbols": False,
            "packageReferences": deps or {}, "projectReferences": {}}


class CohortTests(unittest.TestCase):
    def test_archive_gate_rejects_wrong_commit_range_and_unlocked_cohort(self):
        with TemporaryDirectory() as directory:
            path = Path(directory)
            entry = {"commit": "a" * 40, "packages": {
                "a": {"id": "A", "profiles": [profile()]},
                "b": {"id": "B", "profiles": [profile({"a": "1.2.3"})]}}}
            package(path, "A", "1.0.0-preview.1")
            file = package(path, "B", "1.0.0-preview.1", dependency=("A", "[1.2.3,)"))
            self.assertEqual(set(cohorts.verify_archives(path, entry, "repo/publish.yml")), {"a", "b"})
            package(path, "B", "1.0.0-preview.1", dependency=("A", "[1.2.3]"))
            with self.assertRaisesRegex(ValueError, "dependency groups differ"):
                cohorts.verify_archives(path, entry, "repo/publish.yml")
            package(path, "B", "1.0.0-preview.1", commit="b" * 40, dependency=("A", "1.2.3"))
            with self.assertRaisesRegex(ValueError, "wrong source"):
                cohorts.verify_archives(path, entry, "repo/publish.yml")
            file.unlink()
            package(path, "B", "1.0.0-preview.2", dependency=("A", "1.2.3"))
            with self.assertRaisesRegex(ValueError, "do not share"):
                cohorts.verify_archives(path, entry, "repo/publish.yml")

    def test_skipped_platform_check_cannot_count_as_successful_evidence(self):
        required = cohorts.owner_checks("Broiler.Hosting", False)
        data = [{"name": name, "conclusion": "success", "steps": [
            {"name": step, "conclusion": "skipped" if step == "Install Android workload" else "success"}
            for step in steps]} for name, steps in required.items()]
        with self.assertRaisesRegex(ValueError, "Install Android workload"):
            cohorts.require_jobs(data, required)
        self.assertIn("Pack and verify every package",
                      cohorts.owner_checks("Broiler.DOM", False)["windows-latest / Release"])

    def test_routing_retains_unadmitted_and_root_only_packages(self):
        pilot, owner = "Broiler.DateTime/publish.yml", "Broiler.UI/publish.yml"
        def checks(passes):
            return {name: passes for name in ("audit", "pr", "dryRun", "publication", "admitted")} | {"blockers": []}
        with TemporaryDirectory() as directory:
            path = Path(directory)
            owned = package(path, "Broiler.UI", "1.0.0-preview.1")
            root = package(path, "Broiler.Root", "1.0.0-preview.1")
            receipt = {"cohorts": {
                pilot: {"wave": 1, "legacyOverlap": [], "prerequisites": []},
                owner: {"wave": 4, "legacyOverlap": ["broiler.ui"], "prerequisites": [pilot]}},
                "admission": {pilot: checks(False), owner: checks(True)}}
            routed = cohorts.route_packages(path, receipt)
            self.assertEqual(set(routed["retain"]), {owned.name, root.name})
            receipt["admission"][pilot] = checks(True)
            routed = cohorts.route_packages(path, receipt)
            self.assertEqual(routed["retain"], [root.name])
            self.assertEqual(routed["redirected"], [{"id": "Broiler.UI", "publisher": owner}])

    def test_workflow_provenance_requires_current_owner(self):
        run = {"path": ".github/workflows/ci.yml@refs/heads/main", "event": "workflow_dispatch",
               "head_branch": "main", "conclusion": "success",
               "head_repository": {"full_name": "Broiler-Platform/Broiler.CSS"}, "head_sha": "a" * 40}
        cohorts.verify_run("Broiler.CSS", "ci.yml", run, "a" * 40)
        with self.assertRaisesRegex(ValueError, "source revision"):
            cohorts.verify_run("Broiler.CSS", "ci.yml", run, "b" * 40)
        with self.assertRaisesRegex(ValueError, "source revision"):
            cohorts.verify_run("Broiler.DOM", "ci.yml", run, "a" * 40)

    def test_repository_can_rehearse_while_prerequisite_blocks_admission(self):
        pilot, owner = "Broiler.DateTime/publish.yml", "Broiler.CSS/publish.yml"
        sha = "a" * 40
        base = {"commit": sha, "promotion": "protected.yml"}
        entries = {pilot: dict(base, repository="Broiler.DateTime", wave=1, prerequisites=[]),
                   owner: dict(base, repository="Broiler.CSS", wave=2, prerequisites=[pilot])}
        audit = {"groups": [{"repository": "Broiler.CSS", "commit": sha, "pins": [{"status": "current"}]}]}
        with TemporaryDirectory() as directory, patch.object(cohorts, "verify_archives", return_value={}), \
                patch.object(cohorts, "restore_candidates", return_value=[]):
            path = Path(directory)
            report = cohorts.admission(entries, audit, {}, path, local={owner: path})
        self.assertTrue(report[owner]["dryRun"])
        self.assertFalse(report[owner]["admitted"])
        self.assertTrue(any("Unadmitted prerequisite" in item for item in report[owner]["blockers"]))


if __name__ == "__main__":
    unittest.main()
