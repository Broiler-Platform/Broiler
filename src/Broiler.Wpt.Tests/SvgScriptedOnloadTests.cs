using System;
using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// The three gaps that kept a scripted inline <c>&lt;svg&gt;</c> from running its own test code,
/// found while triaging the <c>conformance-checkers/html-svg</c> cluster of the 2026-08-21 WPT
/// run. The SVG 1.1 suite the corpus is generated from writes every case the same way — a
/// function in a <c>&lt;script&gt;</c> inside the fragment, called from
/// <c>&lt;svg onload="domTest(evt)"&gt;</c>, which clears a red "not supported" rectangle the
/// markup paints up front — so all three had to hold before any of them rendered.
///
/// <list type="number">
/// <item>
/// The runner's own script scan accepted three MIME types where HTML §4.12.1 defines sixteen, so
/// <c>type="text/ecmascript"</c> — what the SVG suite writes — was skipped, along with
/// <c>text/jscript</c>, the versioned <c>text/javascript1.x</c> aliases, and any type carrying a
/// <c>; charset=…</c> parameter. The engine underneath runs all of them; only the harness
/// disagreed, so the test was scored on a page whose script had never executed.
/// </item>
/// <item>
/// Nothing fired <c>load</c> at an outermost inline <c>&lt;svg&gt;</c> (SVG 1.1 §16.2), so the
/// <c>onload</c> attribute on it never ran.
/// </item>
/// <item>
/// An inline handler bound the event as <c>event</c> only. SVG 1.1 §16.2.2 names it <c>evt</c>,
/// so <c>onload="domTest(evt)"</c> threw <c>ReferenceError</c> before its first statement.
/// </item>
/// </list>
/// </summary>
[Xunit.Collection("NativeAnchorWpt")]
public class SvgScriptedOnloadTests
{
    private const int Size = 200;

    private static BBitmap Render(string html)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-svgscript-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);
            return new WptTestRunner(Size, Size).RenderHtmlFileBitmapPublic(file, dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>
    /// A 200×200 box that starts red. Script that runs turns it green, so one pixel at the centre
    /// says whether the script executed — the same "no red" convention the SVG suite itself uses.
    /// </summary>
    private static string RedBoxPage(string markup) =>
        "<!DOCTYPE html><html><body style=\"margin:0\">"
        + "<div id=\"box\" style=\"width:200px;height:200px;background:red\"></div>"
        + markup
        + "</body></html>";

    private const string PaintGreen = "document.getElementById('box').style.background = 'green';";

    private static void AssertGreen(BBitmap bmp, string because)
    {
        var p = bmp.GetPixel(Size / 2, Size / 2);
        Assert.True(p.R < 80 && p.G > 100 && p.B < 80,
            $"{because} — expected the box to be green, got rgb({p.R},{p.G},{p.B})");
    }

    private static void AssertRed(BBitmap bmp, string because)
    {
        var p = bmp.GetPixel(Size / 2, Size / 2);
        Assert.True(p.R > 180 && p.G < 80 && p.B < 80,
            $"{because} — expected the box to stay red, got rgb({p.R},{p.G},{p.B})");
    }

    // ---------------------------------------------------------------
    //  1. The JavaScript MIME essences HTML §4.12.1 defines
    // ---------------------------------------------------------------

    [Theory(Timeout = 600000)]
    [InlineData("text/ecmascript")]
    [InlineData("application/ecmascript")]
    [InlineData("text/x-ecmascript")]
    [InlineData("text/jscript")]
    [InlineData("text/livescript")]
    [InlineData("text/javascript1.5")]
    [InlineData("application/x-javascript")]
    [InlineData("text/javascript; charset=utf-8")]
    [InlineData("text/javascript")]
    [InlineData("application/javascript")]
    public void LegacyJavaScriptMimeType_Executes(string type)
    {
        var bmp = Render(RedBoxPage($"<script type=\"{type}\">{PaintGreen}</script>"));
        AssertGreen(bmp, $"type=\"{type}\" is a JavaScript MIME essence and must run");
    }

    /// <summary>
    /// The other half of the same rule: a data block is not a script. Widening the accepted set
    /// must not start executing JSON-LD, import maps or client-side templates.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("text/template")]
    [InlineData("application/ld+json")]
    [InlineData("application/json")]
    [InlineData("text/x-handlebars-template")]
    [InlineData("importmap")]
    [InlineData("speculationrules")]
    public void DataBlockType_DoesNotExecute(string type)
    {
        var bmp = Render(RedBoxPage($"<script type=\"{type}\">{PaintGreen}</script>"));
        AssertRed(bmp, $"type=\"{type}\" marks a data block and must never be executed");
    }

    // ---------------------------------------------------------------
    //  2. load fires at an outermost inline <svg>
    // ---------------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void SvgRoot_OnloadAttribute_Fires()
    {
        var bmp = Render(RedBoxPage(
            $"<svg width=\"10\" height=\"10\" onload=\"{PaintGreen}\"></svg>"));
        AssertGreen(bmp, "SVG 1.1 §16.2 fires load at the outermost <svg>, running its onload");
    }

    [Fact(Timeout = 600000)]
    public void SvgRoot_LoadListener_Fires()
    {
        var bmp = Render(RedBoxPage(
            "<svg id=\"s\" width=\"10\" height=\"10\"></svg>"
            + "<script>document.getElementById('s').addEventListener('load', function () { "
            + PaintGreen + " });</script>"));
        AssertGreen(bmp, "an addEventListener('load') on the <svg> root must fire too");
    }

    // ---------------------------------------------------------------
    //  3. `evt` inside an SVG event-handler attribute
    // ---------------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void SvgInlineHandler_BindsEvt()
    {
        var bmp = Render(RedBoxPage(
            "<svg width=\"10\" height=\"10\" onload=\"if (evt &amp;&amp; evt.type === 'load') { "
            + PaintGreen + " }\"></svg>"));
        AssertGreen(bmp, "SVG 1.1 §16.2.2 binds the event object as `evt` in a handler attribute");
    }

    [Fact(Timeout = 600000)]
    public void SvgInlineHandler_StillBindsEvent()
    {
        var bmp = Render(RedBoxPage(
            "<svg width=\"10\" height=\"10\" onload=\"if (event &amp;&amp; event.type === 'load') { "
            + PaintGreen + " }\"></svg>"));
        AssertGreen(bmp, "the HTML name `event` keeps working alongside the SVG alias");
    }

    /// <summary>
    /// The alias is SVG-only. Binding <c>evt</c> on an HTML element would shadow a page's own
    /// global of that name inside its handlers, which no browser does — so an HTML handler must
    /// still see the global.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void HtmlInlineHandler_DoesNotShadowAGlobalNamedEvt()
    {
        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0\" onload=\"if (evt === 'the-global') { "
            + PaintGreen + " }\">"
            + "<div id=\"box\" style=\"width:200px;height:200px;background:red\"></div>"
            + "<script>var evt = 'the-global';</script>"
            + "</body></html>");
        AssertGreen(bmp, "an HTML handler must still resolve `evt` to the page's own global");
    }

    // ---------------------------------------------------------------
    //  All three together — the shape the SVG conformance suite uses
    // ---------------------------------------------------------------

    /// <summary>
    /// <c>conformance-checkers/html-svg/struct-dom-06-b-isvalid</c> in miniature: a red rectangle
    /// inside the fragment, a <c>text/ecmascript</c> script defining the handler, and
    /// <c>onload="domTest(evt)"</c> reaching the document through <c>evt.target.ownerDocument</c>
    /// to clear it. Any one of the three gaps leaves the rectangle painted.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ScriptedSvgConformanceShape_ClearsItsOwnFailureState()
    {
        var bmp = Render(
            "<!DOCTYPE html><html><body style=\"margin:0\">"
            + "<svg width=\"200\" height=\"200\" viewBox=\"0 0 200 200\" onload=\"domTest(evt)\">"
            + "<script type=\"text/ecmascript\"><![CDATA[\n"
            + "  function domTest(evt) {\n"
            + "    var doc = evt.target.ownerDocument;\n"
            + "    doc.getElementById('errorRect').setAttribute('width', '0');\n"
            + "  }\n"
            + "]]></script>"
            + "<rect width=\"200\" height=\"200\" fill=\"green\"/>"
            + "<rect id=\"errorRect\" width=\"200\" height=\"200\" fill=\"red\"/>"
            + "</svg></body></html>");

        AssertGreen(bmp, "the onload handler must run and shrink the red rectangle away");
    }
}
