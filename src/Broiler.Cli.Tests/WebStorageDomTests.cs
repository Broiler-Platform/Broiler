namespace Broiler.Cli.Tests;

/// <summary>
/// The Web Storage areas as a page reaches them: <c>sessionStorage</c> and <c>localStorage</c>
/// unqualified, the <c>Storage</c> interface global a feature test asks for, and the item surface
/// behind both.
/// </summary>
/// <remarks>
/// <c>sessionStorage</c> was never registered, and because <c>window</c> IS the global object that
/// made the bare identifier a <c>ReferenceError</c> rather than an undefined property — which
/// aborts the whole script. <c>www.mediawiki.org</c> ships its modules as a single <c>load.php</c>
/// bundle, so the abort took ResourceLoader, the Vector skin and every module queued behind them
/// with it: "ReferenceError: sessionStorage is not defined" was the only symptom of a page that had
/// lost all of its script-driven behaviour.
/// </remarks>
public class WebStorageDomTests
{
    [Fact]
    public void Unqualified_SessionStorage_Does_Not_Abort_The_Script()
    {
        // The failure mode was not "this read returns undefined" but "the script dies here", so the
        // marker after the reads is the real assertion.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result"">script-died</div>
<script>
var seen = [];
seen.push(typeof sessionStorage);
seen.push(typeof localStorage);
sessionStorage.setItem('k', 'v');
seen.push(sessionStorage.getItem('k'));
seen.push(String(sessionStorage.getItem('absent')));
document.getElementById('result').textContent = seen.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("script-died", result);
        Assert.Contains("object|object|v|null", result);
    }

    [Fact]
    public void The_Two_Areas_Are_Separate_Stores()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result"">script-died</div>
<script>
localStorage.setItem('same-key', 'local');
sessionStorage.setItem('same-key', 'session');
document.getElementById('result').textContent =
    localStorage.getItem('same-key') + '|' + sessionStorage.getItem('same-key') +
    '|' + (localStorage === sessionStorage);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("local|session|false", result);
    }

    [Fact]
    public void An_Area_Enumerates_Through_Length_And_Key()
    {
        // How a page walks an area it did not write itself. The methods must not show up among the
        // keys: they are own properties here, where a browser keeps them on Storage.prototype.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result"">script-died</div>
<script>
sessionStorage.setItem('a', '1');
sessionStorage.setItem('b', '2');
var keys = [];
for (var i = 0; i < sessionStorage.length; i++) keys.push(sessionStorage.key(i));
document.getElementById('result').textContent =
    keys.join(',') + '|' + Object.keys(sessionStorage).join(',') + '|' + sessionStorage.length;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("a,b|a,b|2", result);
    }

    [Fact]
    public void Named_Access_And_setItem_Address_The_Same_Item()
    {
        // Storage is a legacy platform object with named property getters and setters, so pages use
        // the two spellings interchangeably — including across them, as this does.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result"">script-died</div>
<script>
localStorage.direct = 'assigned';
localStorage.setItem('viaMethod', 'set');
var seen = [
    localStorage.getItem('direct'),
    localStorage.viaMethod,
    localStorage.length
];
localStorage.clear();
seen.push(String(localStorage.getItem('direct')));
seen.push(localStorage.length);
document.getElementById('result').textContent = seen.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("assigned|set|2|null|0", result);
    }

    [Fact]
    public void The_Storage_Interface_Global_Answers_Feature_Tests()
    {
        // `typeof Storage !== 'undefined'` is the canonical Web Storage feature test: a page that
        // fails it takes its no-storage path however complete the areas themselves are.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result"">script-died</div>
<script>
var seen = [
    typeof Storage,
    localStorage instanceof Storage,
    sessionStorage instanceof Storage,
    ({}) instanceof Storage
];
document.getElementById('result').textContent = seen.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("function|true|true|false", result);
    }
}
