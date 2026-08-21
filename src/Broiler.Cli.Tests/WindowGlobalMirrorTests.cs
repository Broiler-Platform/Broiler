namespace Broiler.Cli.Tests;

/// <summary>
/// Guards <c>MirrorWindowMembersOntoGlobal</c>: in a browser <c>window</c> IS the global object,
/// so every window member is also valid unqualified. Here the two are distinct objects, and the
/// mirror list was maintained by hand and had drifted — <c>getComputedStyle</c>, <c>location</c>,
/// <c>matchMedia</c>, <c>innerWidth</c>, <c>scrollTo</c>, <c>self</c>, <c>localStorage</c> and the
/// rest existed only on <c>window</c>. An unqualified reference to one of those raised a
/// <c>ReferenceError</c>, which aborts the entire script rather than just that statement, so a WPT
/// page using the idiomatic unqualified spelling rendered as if none of its script had run.
/// </summary>
public class WindowGlobalMirrorTests
{
    [Fact(Timeout = 600000)]
    public void Every_Window_Member_Is_Reachable_Unqualified()
    {
        // The guard that keeps the two in step as window members are added: sweeping, not listing.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var missing = Object.getOwnPropertyNames(window).filter(function(n) { return !(n in globalThis); });
document.getElementById('result').textContent = 'missing=' + (missing.length ? missing.join(',') : 'none');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("missing=none", result);
    }

    [Fact(Timeout = 600000)]
    public void Unqualified_Window_Apis_Do_Not_Abort_The_Script()
    {
        // The failure mode was not "this call returns undefined" but "the script dies here", so
        // the marker after the calls is the real assertion.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"" style=""color: rgb(1, 2, 3)""></div>
<div id=""result"">script-died</div>
<script>
var seen = [];
seen.push(getComputedStyle(document.getElementById('target')).color);
seen.push(typeof location.href);
seen.push(typeof matchMedia('(min-width: 1px)').matches);
seen.push(typeof localStorage);
seen.push(self === window);
scrollTo(0, 0);
document.getElementById('result').textContent = seen.join('|');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.DoesNotContain("script-died", result);
        Assert.Contains("rgb(1, 2, 3)|string|boolean|object|true", result);
    }

    [Fact(Timeout = 600000)]
    public void Mirrored_Accessors_Stay_Live_Rather_Than_Snapshotting()
    {
        // Descriptors are copied, not values, so an accessor-backed member such as innerWidth
        // reads through the same getter and keeps agreeing with window.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var agree = innerWidth === window.innerWidth &&
            innerHeight === window.innerHeight &&
            typeof innerWidth === 'number' &&
            innerWidth > 0;
document.getElementById('result').textContent = 'agree=' + agree;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("agree=true", result);
    }

    [Fact(Timeout = 600000)]
    public void Engine_Builtins_Are_Not_Overwritten_By_The_Mirror()
    {
        // The sweep skips any name the global already owns, so builtins and the explicit aliases
        // registered before it always win.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var intact = typeof Object === 'function' &&
             typeof Array === 'function' &&
             typeof setTimeout === 'function' &&
             typeof document === 'object' &&
             typeof __broilerWindowForGlobalMirror === 'undefined';
document.getElementById('result').textContent = 'intact=' + intact;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("intact=true", result);
    }
}
