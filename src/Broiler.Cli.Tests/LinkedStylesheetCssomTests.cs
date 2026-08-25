using System.Net;
using System.Text;

namespace Broiler.Cli.Tests;

/// <summary>
/// A <c>&lt;link rel="stylesheet"&gt;</c>'s rules must reach the CSSOM — <c>cssRules</c>,
/// <c>getComputedStyle</c>, and the sheet's <c>href</c> — exactly as an inline <c>&lt;style&gt;</c>
/// carrying the same text does.
/// </summary>
/// <remarks>
/// <para>
/// They did not. The bridge handed the raw <c>href</c> content attribute to the resource loader,
/// which takes absolute URLs only (<c>ResourceLoader.LoadTextDirect</c> returns <c>null</c> for
/// anything else), so every <em>relative</em> href — the ordinary case — fetched nothing: the sheet
/// appeared in <c>document.styleSheets</c> with no rules and no <c>href</c>, and computed style saw
/// an unstyled element. It was not a <c>file:</c>-scheme defect: <c>file:</c> and <c>http(s):</c>
/// failed identically for a relative href and worked identically for an absolute one, which is why
/// both schemes are pinned here.
/// </para>
/// <para>
/// It was invisible as a rendering bug because <c>HtmlRender</c> resolves and applies the link
/// itself, so paint and the CSSOM held two different stylesheet sets and only paint had the linked
/// one — the reason the pixel assertion in <see cref="StylesheetBaseHrefTests"/> passed throughout.
/// Each test below therefore asserts against an <b>inline control</b> carrying byte-identical CSS,
/// so it pins agreement between the two paths rather than a transcription of today's values.
/// </para>
/// </remarks>
public class LinkedStylesheetCssomTests
{
    private const string Css = "#a { display: flex; color: rgb(1, 2, 3); } .z { margin-top: 7px }";

    /// <summary>The probe: what the CSSOM says about the first sheet and about the styled element.
    /// Everything but <c>href</c> must be identical for a linked and an inline sheet.</summary>
    private const string Probe = """
<div id="a" class="z">x</div><div id="result"></div>
<script>
var s = document.styleSheets[0];
var cs = getComputedStyle(document.getElementById('a'));
document.getElementById('result').textContent =
  '[rules=' + (s ? s.cssRules.length : 'no-sheet') +
  ' display=' + cs.display + ' color=' + cs.color + ' mt=' + cs.marginTop +
  ' href=' + (s ? String(s.href) : 'no-sheet') + ']';
</script>
""";

    private static string Linked(string href) =>
        $"<!DOCTYPE html><html><head><link rel=\"stylesheet\" href=\"{href}\"></head><body>{Probe}</body></html>";

    private static string Inline() =>
        $"<!DOCTYPE html><html><head><style>{Css}</style></head><body>{Probe}</body></html>";

    /// <summary>Extracts the probe's bracketed report from the serialized document.</summary>
    private static string Report(string html, string pageUrl)
    {
        var serialized = CaptureService.ExecuteScriptsWithDom(html, pageUrl);
        var open = serialized.IndexOf('[');
        var close = serialized.IndexOf(']', open + 1);
        Assert.True(open >= 0 && close > open, $"probe did not run; document was:\n{serialized}");
        return serialized[open..(close + 1)];
    }

    /// <summary>The CSSOM report with the sheet's location stripped, so a linked sheet can be
    /// compared against an inline control whose <c>href</c> is legitimately <c>null</c>.</summary>
    private static string RulesAndStyle(string report) => report[..report.IndexOf(" href=", StringComparison.Ordinal)];

    private static string HrefOf(string report) =>
        report[(report.IndexOf(" href=", StringComparison.Ordinal) + " href=".Length)..].TrimEnd(']');

    [Fact(Timeout = 600000)]
    public void RelativeHref_Over_File_Reaches_CssRules_And_ComputedStyle()
    {
        var dir = NewTempDir();
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "sheet.css"), Css);
            var pageUrl = new Uri(System.IO.Path.Combine(dir, "page.html")).AbsoluteUri;
            var sheetUrl = new Uri(System.IO.Path.Combine(dir, "sheet.css")).AbsoluteUri;

            var linked = Report(Linked("sheet.css"), pageUrl);
            var inline = Report(Inline(), pageUrl);

            Assert.Equal(RulesAndStyle(inline), RulesAndStyle(linked));
            // …and it is not vacuously equal because both are empty: the control really is styled.
            Assert.Contains("rules=2 display=flex", inline);
            // CSSOM §2.1: a linked sheet reports its location; the inline control has none.
            Assert.Equal(sheetUrl, HrefOf(linked));
            Assert.Equal("null", HrefOf(inline));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact(Timeout = 600000)]
    public void AbsoluteHref_Over_File_Reaches_CssRules_And_ComputedStyle()
    {
        var dir = NewTempDir();
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "sheet.css"), Css);
            var pageUrl = new Uri(System.IO.Path.Combine(dir, "page.html")).AbsoluteUri;
            var sheetUrl = new Uri(System.IO.Path.Combine(dir, "sheet.css")).AbsoluteUri;

            var linked = Report(Linked(sheetUrl), pageUrl);

            Assert.Equal(RulesAndStyle(Report(Inline(), pageUrl)), RulesAndStyle(linked));
            Assert.Equal(sheetUrl, HrefOf(linked));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    /// <summary>
    /// The same over <c>http:</c>, against a local origin. This is the half that decided the
    /// diagnosis: had the relative case worked here, the defect would have been the file loader's.
    /// It failed identically, so the cause was the unresolved href on both.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void RelativeAndAbsoluteHref_Over_Http_Reach_CssRules_And_ComputedStyle()
    {
        using var origin = new CssOrigin(Css);
        var pageUrl = origin.Prefix + "page.html";
        var sheetUrl = origin.Prefix + "sheet.css";

        var inline = RulesAndStyle(Report(Inline(), pageUrl));
        var relative = Report(Linked("sheet.css"), pageUrl);
        var absolute = Report(Linked(sheetUrl), pageUrl);

        Assert.Equal(inline, RulesAndStyle(relative));
        Assert.Equal(inline, RulesAndStyle(absolute));
        Assert.Contains("rules=2 display=flex", inline);
        Assert.Equal(sheetUrl, HrefOf(relative));
        Assert.Equal(sheetUrl, HrefOf(absolute));
    }

    /// <summary>
    /// The href resolves against the <em>document</em> base URL, so a <c>&lt;base href&gt;</c>
    /// relocates the sheet the CSSOM reads — the same rule the render-bound
    /// <c>RewriteLinkStyleSheetHrefs</c> pass applies, and the trap
    /// <see cref="StylesheetBaseHrefTests"/> plants for paint: a same-named decoy sheet sits next to
    /// the document. Reading the decoy would style the element from the wrong file.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Href_Resolves_Against_BaseHref_Not_The_Documents_Own_Directory()
    {
        var dir = NewTempDir();
        try
        {
            var resources = System.IO.Path.Combine(dir, "resources");
            System.IO.Directory.CreateDirectory(resources);
            System.IO.File.WriteAllText(System.IO.Path.Combine(resources, "sheet.css"), Css);
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(dir, "sheet.css"), "#a { display: grid; color: rgb(9, 9, 9); }");

            var pageUrl = new Uri(System.IO.Path.Combine(dir, "page.html")).AbsoluteUri;
            var based = "<!DOCTYPE html><html><head><base href=\"resources/\">" +
                        "<link rel=\"stylesheet\" href=\"sheet.css\"></head><body>" + Probe + "</body></html>";

            var report = Report(based, pageUrl);

            Assert.Equal(RulesAndStyle(Report(Inline(), pageUrl)), RulesAndStyle(report));
            Assert.Equal(new Uri(System.IO.Path.Combine(resources, "sheet.css")).AbsoluteUri, HrefOf(report));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    private static string NewTempDir()
    {
        var dir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "broiler-linkcssom-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { System.IO.Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
    }

    /// <summary>A local origin that serves one CSS body for every request.</summary>
    private sealed class CssOrigin : IDisposable
    {
        private readonly HttpListener _listener = new();

        public CssOrigin(string css)
        {
            // Port 0 is not available to HttpListener, so bind by trial — the loopback range is
            // effectively free, and a collision just moves to the next candidate.
            for (var attempt = 0; ; attempt++)
            {
                Prefix = $"http://127.0.0.1:{18100 + Random.Shared.Next(2000)}/";
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add(Prefix);
                try
                {
                    _listener.Start();
                    break;
                }
                catch (HttpListenerException) when (attempt < 10)
                {
                }
            }

            var body = Encoding.UTF8.GetBytes(css);
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    while (_listener.IsListening)
                    {
                        var ctx = _listener.GetContext();
                        ctx.Response.ContentType = "text/css";
                        ctx.Response.ContentLength64 = body.Length;
                        ctx.Response.OutputStream.Write(body, 0, body.Length);
                        ctx.Response.Close();
                    }
                }
                catch
                {
                    // Shutdown races the accept loop; nothing to report.
                }
            });
        }

        public string Prefix { get; private set; }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { /* already torn down */ }
            try { _listener.Close(); } catch { /* already torn down */ }
        }
    }
}
