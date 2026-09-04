import importlib.util
from pathlib import Path
import tempfile
import unittest


SCRIPT_PATH = (
    Path(__file__).resolve().parents[1] / "generate-aggregate-reference-redirects.py"
)
SPEC = importlib.util.spec_from_file_location(
    "generate_aggregate_reference_redirects", SCRIPT_PATH
)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class NearestBuildTargetsTests(unittest.TestCase):
    """Whether a component's Directory.Build.targets hides the root one.

    Getting this wrong in the "chains upward" direction is the script's worst failure: the
    component looks covered by the root file, redirects are emitted for its projects, and
    those then compete with the nested checkouts its own solutions enumerate -- inventing
    the duplicate assemblies the redirects exist to remove.
    """

    def setUp(self) -> None:
        self._temporary = tempfile.TemporaryDirectory()
        self.root = Path(self._temporary.name).resolve()
        self.addCleanup(self._temporary.cleanup)

        # The script anchors on the repository root it was loaded from.
        self._original_root = MODULE.ROOT
        MODULE.ROOT = str(self.root)
        self.addCleanup(setattr, MODULE, "ROOT", self._original_root)

        (self.root / "Directory.Build.targets").write_text(
            "<Project />", encoding="utf-8"
        )
        self.component = self.root / "Broiler.Component"
        (self.component / "src" / "Thing").mkdir(parents=True)
        self.project = self.component / "src" / "Thing" / "Thing.csproj"
        self.project.write_text("<Project />", encoding="utf-8")

    def _component_targets(self, body: str) -> None:
        (self.component / "Directory.Build.targets").write_text(body, encoding="utf-8")

    def test_no_component_file_leaves_the_root_one_in_charge(self) -> None:
        self.assertIsNone(MODULE.nearest_build_targets(str(self.project)))

    def test_a_plain_component_file_breaks_the_chain(self) -> None:
        self._component_targets("<Project />")
        self.assertEqual(
            MODULE.nearest_build_targets(str(self.project)), "Broiler.Component"
        )

    def test_a_file_that_chains_upward_does_not_break_the_chain(self) -> None:
        self._component_targets(
            "<Project>"
            "  <Import Project=\"$([MSBuild]::GetPathOfFileAbove("
            "'Directory.Build.targets', '$(MSBuildThisFileDirectory)../'))\" />"
            "</Project>"
        )
        self.assertIsNone(MODULE.nearest_build_targets(str(self.project)))

    def test_the_split_property_and_import_spelling_still_counts_as_chaining(
        self,
    ) -> None:
        """The idiom Broiler.UI's props file uses: a property holds the call.

        Looking only at Import/@Project would miss this and wrongly report the file as
        breaking the chain.
        """
        self._component_targets(
            "<Project>"
            "  <PropertyGroup>"
            "    <Parent>$([MSBuild]::GetPathOfFileAbove("
            "'Directory.Build.targets', '$(MSBuildThisFileDirectory)../'))</Parent>"
            "  </PropertyGroup>"
            "  <Import Project=\"$(Parent)\" Condition=\"'$(Parent)' != ''\" />"
            "</Project>"
        )
        self.assertIsNone(MODULE.nearest_build_targets(str(self.project)))

    def test_a_comment_naming_the_function_does_not_count_as_chaining(self) -> None:
        """The regression this test exists for.

        Broiler.Code's shield file warned against chaining and named the function to do
        so. Read as a chaining file, it stopped shielding, and the script emitted 55
        redirects for that component.
        """
        self._component_targets(
            "<Project>"
            "  <!-- Do NOT fix this by chaining upward with GetPathOfFileAbove. -->"
            "</Project>"
        )
        self.assertEqual(
            MODULE.nearest_build_targets(str(self.project)), "Broiler.Component"
        )

    def test_a_multi_line_comment_naming_the_function_is_also_ignored(self) -> None:
        self._component_targets(
            "<Project>\n"
            "  <!--\n"
            "    This file exists to be found.\n"
            "    Do NOT chain it upward with GetPathOfFileAbove.\n"
            "  -->\n"
            "</Project>\n"
        )
        self.assertEqual(
            MODULE.nearest_build_targets(str(self.project)), "Broiler.Component"
        )

    def test_a_commented_out_import_does_not_resurrect_the_chain(self) -> None:
        self._component_targets(
            "<Project>\n"
            "  <!-- <Import Project=\"$([MSBuild]::GetPathOfFileAbove(\n"
            "       'Directory.Build.targets', '$(MSBuildThisFileDirectory)../'))\" /> -->\n"
            "</Project>\n"
        )
        self.assertEqual(
            MODULE.nearest_build_targets(str(self.project)), "Broiler.Component"
        )

    def test_a_real_import_beside_a_comment_naming_it_still_chains(self) -> None:
        self._component_targets(
            "<Project>\n"
            "  <!-- Chained on purpose; see GetPathOfFileAbove below. -->\n"
            "  <Import Project=\"$([MSBuild]::GetPathOfFileAbove(\n"
            "     'Directory.Build.targets', '$(MSBuildThisFileDirectory)../'))\" />\n"
            "</Project>\n"
        )
        self.assertIsNone(MODULE.nearest_build_targets(str(self.project)))

    def test_the_root_file_itself_never_hides_itself(self) -> None:
        (self.root / "Root.csproj").write_text("<Project />", encoding="utf-8")
        self.assertIsNone(MODULE.nearest_build_targets(str(self.root / "Root.csproj")))


if __name__ == "__main__":
    unittest.main()
