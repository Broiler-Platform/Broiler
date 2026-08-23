using System.IO;

namespace Broiler.Cli.Tests;

/// <summary>
/// A <c>&lt;link rel="stylesheet" href="data:text/css,…"&gt;</c> carries its own sheet: there is
/// nothing to fetch, and the payload is the stylesheet. The bridge handed the href to the resource
/// loader like any other URL, and the loader dispatches only <c>file</c> and <c>http(s)</c> — so a
/// data: sheet contributed nothing to the cascade and its link dispatched <c>error</c> instead of
/// <c>load</c>.
/// <para>
/// The renderer's own stylesheet loader has always decoded data: URIs, which is what made this hard
/// to see: a data: sheet written in the markup painted, while the CSSOM, <c>getComputedStyle</c>,
/// and the link's load event all disagreed with the picture. A page that builds the link from
/// script and waits on <c>link.onload</c> — <c>css-backgrounds/background-image-shared-stylesheet</c>
/// is the WPT case, and the idiom is common enough on its own — never got the callback and stalled.
/// </para>
/// </summary>
public sealed class DataUriStylesheetLinkTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "broiler-data-css-" + Guid.NewGuid().ToString("N"));

    public DataUriStylesheetLinkTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>Runs <paramref name="bodyHtml"/> as a page in the temp directory and returns the
    /// serialized result, so an <c>#out</c> element's text can be asserted on.</summary>
    private string Run(string bodyHtml) =>
        CaptureService.ExecuteScriptsWithDom(
            "<!DOCTYPE html><html><head></head><body>" + bodyHtml + "</body></html>",
            new Uri(Path.Combine(_dir, "page.html")).AbsoluteUri);

    [Fact(Timeout = 600000)]
    public void A_Script_Injected_Data_Sheet_Applies_To_The_Injecting_Document()
    {
        var result = Run("""
<div id="probe">styled</div>
<div id="out">not run</div>
<script>
window.onload = function() {
  var link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = 'data:text/css,' + encodeURI('#probe { color: rgb(0, 128, 0); }');
  document.head.appendChild(link);
  document.getElementById('out').textContent =
    getComputedStyle(document.getElementById('probe')).color;
};
</script>
""");

        Assert.Contains(">rgb(0, 128, 0)<", result);
    }

    [Fact(Timeout = 600000)]
    public void A_Script_Injected_Data_Sheet_Fires_Load_Not_Error()
    {
        // The half the shared-stylesheet test turns on: it removes its iframe and starts its image
        // load from link.onload, so an `error` here leaves the page waiting forever.
        var result = Run("""
<div id="out">no event</div>
<script>
window.onload = function() {
  var link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = 'data:text/css,' + encodeURI('#out { color: rgb(0, 128, 0); }');
  link.addEventListener('load', function(e) { document.getElementById('out').textContent = 'load ' + (e.currentTarget === link); });
  link.addEventListener('error', function() { document.getElementById('out').textContent = 'error'; });
  document.head.appendChild(link);
};
</script>
""");

        Assert.Contains(">load true<", result);
    }

    [Fact(Timeout = 600000)]
    public void A_Data_Sheet_In_The_Markup_Reaches_Computed_Style()
    {
        // Not script-specific: the same sheet spelled in the markup painted (the renderer decodes
        // data: URIs) while computed style read the unstyled value, so the two disagreed.
        var result = Run("""
<link rel="stylesheet" href="data:text/css,%23probe%20%7B%20color%3A%20rgb(0%2C%20128%2C%200)%3B%20%7D">
<div id="probe">styled</div>
<div id="out">not run</div>
<script>
document.getElementById('out').textContent = getComputedStyle(document.getElementById('probe')).color;
</script>
""");

        Assert.Contains(">rgb(0, 128, 0)<", result);
    }

    [Fact(Timeout = 600000)]
    public void A_Base64_Data_Sheet_Applies_Too()
    {
        // "#probe { color: rgb(0, 128, 0); }" base64-encoded — the other payload spelling RFC 2397
        // allows, and the one a build tool emits.
        var result = Run("""
<link rel="stylesheet" href="data:text/css;base64,I3Byb2JlIHsgY29sb3I6IHJnYigwLCAxMjgsIDApOyB9">
<div id="probe">styled</div>
<div id="out">not run</div>
<script>
document.getElementById('out').textContent = getComputedStyle(document.getElementById('probe')).color;
</script>
""");

        Assert.Contains(">rgb(0, 128, 0)<", result);
    }

    [Fact(Timeout = 600000)]
    public void The_Shared_Sheet_Case_Applies_To_Both_Documents()
    {
        // The shape the WPT test is named for: one data: URL used as a stylesheet by an iframe and,
        // separately, by the page that built the iframe. The child's copy applying must not be the
        // only one that does — that was the exact symptom, a green iframe on an unstyled page.
        var result = Run("""
<iframe id="frame"></iframe>
<div id="probe">styled</div>
<div id="out">not run</div>
<script>
window.onload = function() {
  var sheet = 'data:text/css,' + encodeURI('#probe { color: rgb(0, 128, 0); }');
  document.getElementById('frame').srcdoc = '<link rel="stylesheet" href="' + sheet + '"><div id="probe">child</div>';
  var link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = sheet;
  document.head.appendChild(link);
  document.getElementById('out').textContent =
    getComputedStyle(document.getElementById('probe')).color;
};
</script>
""");

        Assert.Contains(">rgb(0, 128, 0)<", result);
    }
}
