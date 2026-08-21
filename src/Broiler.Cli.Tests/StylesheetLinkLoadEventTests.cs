using System.IO;

namespace Broiler.Cli.Tests;

/// <summary>
/// HTML §4.2.4 "link type stylesheet": a <c>&lt;link rel="stylesheet"&gt;</c> fires <c>load</c> once
/// its sheet has been fetched, and <c>error</c> when the fetch fails. Nothing dispatched these at
/// all, so a page that waits on <c>link.onload</c> before declaring itself ready never got the
/// callback (WPT issue #1497 problem 26, <c>uievents/…/UIEvent.load.stylesheet</c>, which sets the
/// href from <c>window.onload</c> and asserts the event's <c>currentTarget</c> is the link).
/// <para>
/// These need the sheet to exist on disk, because whether the event is <c>load</c> or <c>error</c> is
/// decided by the same fetch the cascade uses — so each test writes a real file and points a
/// <c>file://</c> page URL at it.
/// </para>
/// </summary>
public sealed class StylesheetLinkLoadEventTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "broiler-link-load-" + Guid.NewGuid().ToString("N"));

    public StylesheetLinkLoadEventTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "sheet.css"), "body { background-color: rgb(1, 2, 3); }");
        File.WriteAllText(Path.Combine(_dir, "other.css"), "body { color: rgb(4, 5, 6); }");
    }

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

    // The UIEvent.load.stylesheet shape exactly: the listener is registered from window.onload and
    // the href set immediately after, and the test's own check is on currentTarget.
    [Fact(Timeout = 600000)]
    public void Load_Event_Fires_When_A_Live_Link_Gets_An_Href()
    {
        var result = Run("""
<link id="target" rel="stylesheet">
<div id="out">no event</div>
<script>
window.onload = function() {
  var t = document.getElementById('target');
  t.addEventListener('load', function(e) {
    document.getElementById('out').textContent =
      'type=' + e.type + ' currentTarget=' + (e.currentTarget === t);
  }, false);
  t.href = 'sheet.css';
};
</script>
""");

        Assert.Contains(">type=load currentTarget=true<", result);
    }

    // The negative half: a link pointed at a sheet that is not there fires `error`, not `load`.
    // Without this the test above would pass against an implementation that fired `load`
    // unconditionally, which is the easy wrong version of this feature.
    [Fact(Timeout = 600000)]
    public void Error_Event_Fires_When_The_Sheet_Is_Missing()
    {
        var result = Run("""
<link id="target" rel="stylesheet">
<div id="out">no event</div>
<script>
window.onload = function() {
  var t = document.getElementById('target');
  t.addEventListener('load', function() { document.getElementById('out').textContent = 'load'; });
  t.addEventListener('error', function() { document.getElementById('out').textContent = 'error'; });
  t.href = 'does-not-exist.css';
};
</script>
""");

        Assert.Contains(">error<", result);
    }

    [Fact(Timeout = 600000)]
    public void Load_Event_Fires_For_A_Link_Already_In_The_Markup()
    {
        var result = Run("""
<link id="target" rel="stylesheet" href="sheet.css">
<div id="out">no event</div>
<script>
document.getElementById('target').addEventListener('load', function(e) {
  document.getElementById('out').textContent = 'markup ' + e.type;
});
</script>
""");

        Assert.Contains(">markup load<", result);
    }

    // A link created and populated while detached has not fetched anything, so the event is due on
    // insertion — not on createElement, and not on the href write that preceded it.
    [Fact(Timeout = 600000)]
    public void Detached_Link_Fires_On_Insertion_Not_Before()
    {
        var result = Run("""
<div id="out">no event</div>
<script>
var log = [];
window.onload = function() {
  var l = document.createElement('link');
  l.rel = 'stylesheet';
  l.href = 'sheet.css';
  l.addEventListener('load', function() { log.push('fired'); });
  log.push('beforeInsert=' + log.length);
  document.head.appendChild(l);
  document.getElementById('out').textContent = log.join(',');
};
</script>
""");

        Assert.Contains(">beforeInsert=0,fired<", result);
    }

    // Once per fetch: re-writing the same href is not a new fetch and must stay silent, while
    // pointing the link at a different sheet is, and fires again.
    [Fact(Timeout = 600000)]
    public void The_Event_Fires_Once_Per_Href()
    {
        var result = Run("""
<link id="target" rel="stylesheet">
<div id="out">no event</div>
<script>
window.onload = function() {
  var t = document.getElementById('target');
  var n = 0;
  t.addEventListener('load', function() { n++; });
  t.href = 'sheet.css';
  t.href = 'sheet.css';
  var afterRepeat = n;
  t.href = 'other.css';
  document.getElementById('out').textContent = 'repeat=' + afterRepeat + ' repoint=' + n;
};
</script>
""");

        Assert.Contains(">repeat=1 repoint=2<", result);
    }

    // A link that is not a stylesheet is not a stylesheet fetch, so it fires nothing.
    [Fact(Timeout = 600000)]
    public void A_Non_Stylesheet_Link_Fires_Nothing()
    {
        var result = Run("""
<link id="target" rel="author">
<div id="out">no event</div>
<script>
window.onload = function() {
  var t = document.getElementById('target');
  t.addEventListener('load', function() { document.getElementById('out').textContent = 'fired'; });
  t.addEventListener('error', function() { document.getElementById('out').textContent = 'fired'; });
  t.href = 'sheet.css';
};
</script>
""");

        Assert.Contains(">no event<", result);
    }
}
