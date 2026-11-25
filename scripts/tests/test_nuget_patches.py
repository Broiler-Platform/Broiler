import gzip
import hashlib
import io
import json
import subprocess
import sys
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import nuget_patch_drafts as drafts
import nuget_patches as patches


class PatchPolicyTests(unittest.TestCase):
    def test_blocker_report_groups_reasons_by_owner_without_counting_current_pins(self):
        pin = {"status": "guarded", "reason": "Historical fixture", "eligible": [],
               "excluded": [], "version": "1.0.0"}
        plan = {"source": patches.FEED, "runUrl": "https://github.com/owner/repo/actions/runs/1",
                "errors": [], "groups": [
                    {"repository": "Broiler.DOM", "package": "one", "commit": "a" * 40,
                     "status": "no-edit", "pins": [pin, pin]},
                    {"repository": "Broiler.DateTime", "package": "two", "commit": "b" * 40,
                     "status": "blocked", "reason": "Candidate restore drift", "pins": [
                         {**pin, "status": "eligible", "eligible": ["1.0.1"]}],
                     "attempts": [{"reason": ["out-of-policy transitive change"]}]},
                    {"repository": "Broiler.CSS", "package": "three", "commit": "c" * 40,
                     "status": "no-edit", "pins": [{**pin, "status": "current"}]}]}
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            patches.write_blocker_report(output, plan)
            report = json.loads((output / "patch-blockers.json").read_text())
            self.assertEqual(set(report["owners"]), {"Broiler.DOM", "Broiler.DateTime"})
            self.assertEqual(report["owners"]["Broiler.DOM"][0]["issues"], [
                {"status": "guarded", "reason": "Historical fixture"}])
            self.assertEqual(report["owners"]["Broiler.DateTime"][0]["attempts"][0]["reason"],
                             ["out-of-policy transitive change"])
            self.assertEqual(report["runUrl"], plan["runUrl"])
            self.assertIn("guarded (1)", (output / "patch-blockers.md").read_text())

    def test_only_listed_same_line_patches_are_eligible(self):
        versions = [{"version": number} for number in ("2.4.3", "2.4.6", "2.4.8", "2.5.0", "3.0.0")]
        versions.extend([
            {"version": "2.4.9", "listed": False},
            {"version": "2.4.10", "vulnerabilities": [{"severity": "2"}]},
            {"version": "2.4.11", "deprecation": {"reasons": ["Legacy"]}},
        ])
        eligible, blocked, newer = patches.candidates("2.4.3", versions, False)
        self.assertEqual(eligible, ["2.4.8", "2.4.6"])
        self.assertEqual([item["reason"] for item in blocked], ["unlisted", "known vulnerability", "deprecated"])
        self.assertEqual(newer, {"minor": "2.5.0", "major": "3.0.0"})
        for pin in ("[2.4.3]", "[2.4.3,2.5)", "2.4.*", "2.4.3-vendor", "2.4.3.1"):
            self.assertFalse(patches.patch_allowed(pin, "2.4.8", False), pin)

    def test_preview_is_an_opt_in_on_the_same_version_base(self):
        self.assertFalse(patches.patch_allowed("0.1.0-preview.9", "0.1.0-preview.10", False))
        self.assertTrue(patches.patch_allowed("0.1.0-preview.9", "0.1.0-preview.10", True))
        self.assertFalse(patches.patch_allowed("0.1.0-preview.9", "0.1.1-preview.10", True))
        self.assertFalse(patches.patch_allowed("0.1.0-preview.9", "0.1.0", True))

    def test_literal_edits_preserve_xml_and_leave_properties_untouched(self):
        content = (b'<Project>\r\n<ItemGroup>\r\n'
                   b'<PackageVersion Include="One" Version="2.4.3" Condition="a" />\r\n'
                   b'<PackageVersion Update="ONE" Condition="b"><Version>2.4.3</Version></PackageVersion>\r\n'
                   b'<PackageVersion Include="One" Version="$(SharedVersion)" />\r\n'
                   b'<PackageVersion Include="Other" Version="2.4.3" />\r\n'
                   b'<PackageReference Include="One" VersionOverride="2.4.3" />\r\n'
                   b'</ItemGroup>\r\n</Project>\r\n')
        spans = patches.declaration_spans(content, "one", "PackageVersion", "Version", "2.4.3")
        self.assertEqual(len(spans), 2)
        output = content
        for start, end in reversed(spans):
            output = output[:start] + b"2.4.12" + output[end:]
        self.assertEqual(output.count(b"2.4.12"), 2)
        self.assertEqual(output.count(b"\r\n"), content.count(b"\r\n"))
        self.assertIn(b'Version="$(SharedVersion)"', output)
        self.assertIn(b'Include="Other" Version="2.4.3"', output)
        override = patches.declaration_spans(content, "one", "PackageReference", "VersionOverride", "2.4.3")
        self.assertEqual(len(override), 1)

    def test_out_of_policy_transitive_change_and_same_version_hash_are_blocked(self):
        key = ("Repo/app.csproj", "Release", "net10.0")
        pin = {"uses": [(key[0], key[1], key[2], "")]}
        before = {key: {"one": {"version": "1.0.0", "hash": "a"}, "two": {"version": "2.0.1", "hash": "b"}}}
        after = {key: {"one": {"version": "1.0.1", "hash": "c"}, "two": {"version": "2.1.0", "hash": "d"}}}
        changes, flags = patches.diff_graphs(before, after, "one", [(pin, "1.0.1")], set())
        self.assertEqual(len(changes), 2)
        self.assertIn("out-of-policy resolved change for two", flags[0])
        after[key]["two"] = {"version": "2.0.1", "hash": "different"}
        self.assertTrue(patches.diff_graphs(before, after, "one", [(pin, "1.0.1")], set())[1])
        after[key]["two"] = {"version": "2.0.2", "hash": "d"}
        self.assertFalse(patches.diff_graphs(before, after, "one", [(pin, "1.0.1")], set())[1])

    def test_feed_failure_is_an_error_and_current_pins_do_not_restore(self):
        source = "Repo/Directory.Packages.props"
        snapshot = {"revisions": {"Repo": {"commit": "a" * 40}}, "projects": [{
            "repository": "Repo", "project": "Repo/a.csproj", "profiles": [{
                "configuration": "Release", "framework": "net10.0", "runtime": "", "packages": [{
                    "id": "One", "version": "1.0.0", "declaration": source,
                    "itemType": "PackageVersion", "versionMetadata": "Version"}]}]}]}
        policy = {"previewPackages": [], "guardedDirectories": []}
        with patch.object(patches, "get_json", side_effect=OSError("offline")):
            self.assertIn("offline", patches.audit(snapshot, policy, set(), set(), Path("."))[1][0])
        service = {"resources": [{"@type": "RegistrationsBaseUrl/3.6.0", "@id": "https://api.nuget.org/example"}]}
        with patch.object(patches, "get_json", return_value=service), \
                patch.object(patches, "registration", return_value=[{"version": "1.0.0"}]), \
                patch.object(patches, "validate_repo") as validate:
            groups, errors = patches.audit(snapshot, policy, set(), {"Repo"}, Path("."))
            self.assertEqual(groups[0]["status"], "no-edit")
            self.assertFalse(errors)
            validate.assert_not_called()

    def test_reads_gzip_registration_responses(self):
        data = gzip.compress(b'{"items": []}')
        with patch.object(patches, "urlopen", return_value=io.BytesIO(data)):
            self.assertEqual(patches.get_json(patches.FEED), {"items": []})


class DraftTests(unittest.TestCase):
    def test_patch_job_rejects_other_xml_edits(self):
        base = b'<Project><ItemGroup><PackageVersion Include="One" Version="1.0.0" /></ItemGroup></Project>'
        proposal = base.replace(b"1.0.0", b"1.0.1")
        group = {"repository": "Repo", "package": "one", "updates": [{
            "declaration": "Repo/Directory.Packages.props", "itemType": "PackageVersion",
            "versionMetadata": "Version", "version": "1.0.0", "target": "1.0.1"}]}
        paths = ["Directory.Packages.props"]
        with patch.object(drafts.subprocess, "run", side_effect=[
                subprocess.CompletedProcess([], 0, base), subprocess.CompletedProcess([], 0, proposal)]):
            drafts.verify_edits(Path("."), group, paths, set())
        injected = proposal.replace(b"</Project>", b"<Import Project='Other.props' /></Project>")
        with patch.object(drafts.subprocess, "run", side_effect=[
                subprocess.CompletedProcess([], 0, base), subprocess.CompletedProcess([], 0, injected)]):
            with self.assertRaisesRegex(ValueError, "changes beyond"):
                drafts.verify_edits(Path("."), group, paths, set())

    def test_repeated_identical_scan_reuses_open_draft(self):
        commit = "a" * 40
        suffix = hashlib.sha256(b"Repo/one").hexdigest()
        group = {"repository": "Repo", "package": "one", "commit": commit, "patch": suffix + ".patch",
                 "paths": ["Directory.Packages.props"], "versions": ["1.0.1"], "pins": [{"version": "1.0.0"}]}
        branch = f"refs/heads/codex/nuget-patch/{suffix[:16]}"
        responses = {
            ("rev-parse", "refs/remotes/origin/main"): commit,
            ("diff", "--cached", "--name-status", "--no-renames"): "M\tDirectory.Packages.props",
            ("diff", "--cached", "--raw", "--no-renames"): ":100644 100644 a b M\tDirectory.Packages.props",
            ("ls-remote", "origin", branch): "branch-commit\t" + branch,
            ("write-tree",): "same-tree",
        }
        pr = {"state": "open", "base": {"ref": "main"}, "body": "<!-- Broiler-NuGet-Patch: one -->",
              "html_url": "https://github.com/Owner/Repo/pull/1"}
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(drafts, "git", side_effect=lambda _, *args: responses.get(args, "")) as git, \
                patch.object(drafts, "github", return_value=[pr]) as github, \
                patch.object(drafts, "verify_edits"), \
                patch.object(drafts, "existing_tree", return_value="same-tree"):
            drafts.draft(Path(directory), "Owner/Repo", "main", group, Path(directory), set())
            github.assert_called_once()
            self.assertFalse(any(call.args[1] in {"push", "commit"} for call in git.call_args_list))


if __name__ == "__main__":
    unittest.main()
