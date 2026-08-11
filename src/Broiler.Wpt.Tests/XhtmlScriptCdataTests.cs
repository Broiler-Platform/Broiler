using Xunit;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Inline scripts in an XHTML test, which the corpus writes inside an XML CDATA section.
/// </summary>
/// <remarks>
/// The runner finds a document's scripts by scanning its source, so it sees the CDATA markers an
/// XML parser would have consumed. <c>&lt;![CDATA[</c> is a syntax error, and the throw took the
/// whole script with it — including any function an <c>onload</c> attribute went on to call, which
/// is how <c>css/CSS2</c>'s scripted tests are written. Stripping the wrapper is what lets them run
/// at all; it took <c>css/CSS2</c> from 4863 to 4908 of 6216 reftests, the gains concentrated in
/// <c>box-display</c>'s insert/delete-block DOM tests and <c>tables</c>.
/// </remarks>
public sealed class XhtmlScriptCdataTests
{
    [Fact]
    public void A_Cdata_Section_Leaves_Only_The_Script()
    {
        Assert.Equal(
            "var a = 1;",
            WptTestRunner.StripCdataSection("\n  <![CDATA[\n  var a = 1;\n  ]]>\n").Trim());
    }

    // The spellings that survive an HTML parser as well as an XML one: the markers commented out,
    // and the whole block wrapped in an HTML comment. Both are noise to the engine either way.
    [Theory]
    [InlineData("//<![CDATA[\nvar a = 1;\n//]]>")]
    [InlineData("// <![CDATA[\nvar a = 1;\n// ]]>")]
    [InlineData("<!-- <![CDATA[\nvar a = 1;\n]]> -->")]
    public void The_Commented_Spellings_Are_Stripped_Too(string content) =>
        Assert.Equal("var a = 1;", WptTestRunner.StripCdataSection(content).Trim());

    // A script that carries no wrapper is handed over untouched — including one that merely
    // mentions the markers inside a string, since only a wrapper at the very edges is removed.
    [Theory]
    [InlineData("var a = 1;")]
    [InlineData("var a = \"<![CDATA[\";")]
    [InlineData("")]
    public void A_Script_Without_A_Wrapper_Is_Unchanged(string content) =>
        Assert.Equal(content.Trim(), WptTestRunner.StripCdataSection(content).Trim());
}
