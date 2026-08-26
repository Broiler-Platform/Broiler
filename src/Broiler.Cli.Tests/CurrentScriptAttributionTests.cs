namespace Broiler.Cli.Tests;

/// <summary>
/// Which <c>&lt;script&gt;</c> element the running script is attributed to — what
/// <c>document.currentScript</c> answers, and where <c>document.write</c> inserts.
/// </summary>
/// <remarks>
/// <para>
/// The host holds two lists that look parallel and are not: the program texts it will evaluate, and
/// the document's <c>&lt;script&gt;</c> elements. It paired them by position, reconstructing the
/// element list from the parsed tree with the same classification the extractor applies. That works
/// only while the two readings stay identical, and they cannot: the extractor drops a script for
/// reasons no reader of the parsed elements can know — a source blocked by CSP, a fetch that came
/// back empty — and every pairing after such a script shifted by one.
/// </para>
/// <para>
/// The effect was that each script was attributed to its <em>predecessor</em>. A page whose first
/// external script 404s had every later script's <c>document.currentScript</c> name the wrong
/// element, and <c>document.write</c> insert at the wrong place.
/// </para>
/// <para>
/// Every expectation is Chromium's measured answer to the same markup. The tests assert which
/// element each script names, never the order the scripts run in: Broiler honours <c>defer</c> on an
/// inline script where the HTML specification ignores it without a <c>src</c>, which is a separate
/// divergence belonging to track 3's task-model item.
/// </para>
/// </remarks>
public class CurrentScriptAttributionTests
{
    private static string Capture(string body)
        => CaptureService.ExecuteScriptsWithDom(
            $@"<!DOCTYPE html><html><body>
<div id=""result""></div>
{body}
<script>
window.addEventListener('load', function () {{
    document.getElementById('result').textContent = String(window.__report());
}});
</script>
</body></html>", "file:///t.html");

    private const string Collect =
        "<script>window.__r = []; window.__report = function () { return window.__r.join(','); };"
        + "window.__note = function (id) { window.__r.push(id + ':' "
        + "+ (document.currentScript ? document.currentScript.id : 'null')); };</script>";

    /// <summary>
    /// A script whose source never resolves is still an element, and must not displace the ones
    /// after it. This is the case the positional pairing could not see.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnUnresolvedSourceDoesNotShiftTheScriptsAfterIt()
        => Assert.Contains(">a:a,b:b<", Capture(Collect
            + @"<script id=""miss"" src=""definitely-not-here.js""></script>
<script id=""a"">window.__note('a');</script>
<script id=""b"">window.__note('b');</script>"));

    /// <summary>
    /// A data block is an element the browser never executes — a JSON-LD block, an import map, a
    /// template. Several in a row must not shift anything either.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void DataBlocksDoNotShiftTheScriptsAfterThem()
        => Assert.Contains(">a:a,b:b<", Capture(
            @"<script type=""application/ld+json"" id=""jsonld"">{""a"":1}</script>
<script type=""text/template"" id=""tpl"">not js</script>" + Collect
            + @"<script id=""a"">window.__note('a');</script>
<script type=""application/ld+json"" id=""jsonld2"">{""b"":2}</script>
<script id=""b"">window.__note('b');</script>"));

    /// <summary>A deferred script names itself too, from its own bucket.</summary>
    [Fact(Timeout = 600000)]
    public void ADeferredScriptNamesItself()
        => Assert.Contains(">d1:d1,d2:d2<", Capture(Collect
            + @"<script id=""miss"" src=""nope.js""></script>
<script id=""d1"" defer>window.__note('d1');</script>
<script id=""d2"" defer>window.__note('d2');</script>"));

    /// <summary>
    /// A module has no <c>currentScript</c> — HTML §4.12.1 leaves it null for one, because a module
    /// is not run against the element that declared it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AModuleHasNoCurrentScript()
        => Assert.Contains(">m:null<", Capture(Collect
            + @"<script id=""m"" type=""module"">window.__note('m');</script>"));

    /// <summary>
    /// The mixed page: data blocks, an unresolved source, classic, deferred and module scripts
    /// together. Every one names itself, and the module names none.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void EveryScriptOnAMixedPageNamesItself()
    {
        var result = Capture(
            @"<script type=""application/ld+json"" id=""jsonld"">{""a"":1}</script>" + Collect
            + @"<script id=""s1"">window.__note('s1');</script>
<script id=""miss"" src=""nope.js""></script>
<script id=""s2"">window.__note('s2');</script>
<script id=""d1"" defer>window.__note('d1');</script>
<script id=""m1"" type=""module"">window.__note('m1');</script>
<script id=""s3"">window.__note('s3');</script>");

        foreach (var expected in new[] { "s1:s1", "s2:s2", "d1:d1", "s3:s3", "m1:null" })
            Assert.Contains(expected, result);
    }

    /// <summary>
    /// <c>document.write</c> inserts at the running script's position, and reads the same
    /// attribution — so it landed in the wrong place for exactly the same reason.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void DocumentWriteInsertsAtTheRunningScript()
        => Assert.Contains(">w|after<", CaptureService.ExecuteScriptsWithDom(
            @"<!DOCTYPE html><html><body>
<div id=""result""></div>
<p id=""before"">before</p>
<script id=""miss"" src=""nope.js""></script>
<script id=""w"">document.write('<b id=""written"">W</b>');</script>
<p id=""after"">after</p>
<script>
window.addEventListener('load', function () {
    var w = document.getElementById('written');
    document.getElementById('result').textContent =
        (w && w.previousElementSibling ? w.previousElementSibling.id : '?') + '|' +
        (w && w.nextElementSibling ? w.nextElementSibling.id : '?');
});
</script>
</body></html>", "file:///t.html"));
}
