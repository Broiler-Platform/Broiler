import importlib.util
from pathlib import Path
import sys
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import office_desktop_demo as demo


class DesktopSelectionTests(unittest.TestCase):
    def release(self, version, draft=False, web=False):
        suffix = "browser-wasm.zip" if web else "win-x64-self-contained.zip"
        return dict(tag_name="writer-v" + version, draft=draft,
                    assets=[dict(name=f"Broiler.Writer-{version}-{suffix}")])

    def test_semantic_preview_order_and_draft_and_web_exclusion(self):
        candidates = [self.release("0.1.0-preview.9"), self.release("0.1.0-preview.10"),
                      self.release("0.1.0-preview.11", draft=True), self.release("0.2.0-preview.1", web=True)]
        self.assertEqual(demo.candidate("writer", candidates)[0], "0.1.0-preview.10")

    def test_explicit_override_is_honored(self):
        rows = [self.release("0.1.0-preview.9"), self.release("0.1.0-preview.10")]
        self.assertEqual(demo.candidate("writer", rows, "writer-v0.1.0-preview.9")[0], "0.1.0-preview.9")
        with self.assertRaises(ValueError):
            demo.candidate("writer", rows, "writer-v0.1.0-preview.8")

    def test_nonsemantic_tags_are_not_candidates(self):
        rows = [self.release("nightly"), self.release("0.1.0-preview.10")]
        self.assertEqual(demo.candidate("writer", rows)[0], "0.1.0-preview.10")

    def test_plate_legacy_archive_is_accepted(self):
        row = dict(tag_name="plate-v0.1.0-preview.2", draft=False,
                   assets=[dict(name="Broiler.Plate-0.1.0-preview.2-win-x64.zip")])
        self.assertEqual(demo.candidate("plate", [row])[0], "0.1.0-preview.2")

    def test_missing_native_assets_and_ambiguous_assets_fail(self):
        with self.assertRaises(ValueError):
            demo.candidate("writer", [self.release("0.1.0-preview.10", web=True)])
        row = dict(tag_name="plate-v0.1.0-preview.2", draft=False, assets=[
            dict(name="Broiler.Plate-0.1.0-preview.2-win-x64.zip"),
            dict(name="Broiler.Plate-0.1.0-preview.2-win-x64-self-contained.zip")])
        with self.assertRaises(ValueError):
            demo.candidate("plate", [row])
