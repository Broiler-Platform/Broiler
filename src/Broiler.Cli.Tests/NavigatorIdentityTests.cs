using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>navigator</c> identity constants, <c>webdriver</c>, and the hardware members, plus the
/// stray legacy <c>window.offscreenBuffering</c> that belonged to the same audited block as the
/// window/screen geometry.
/// </summary>
/// <remarks>
/// All read <c>undefined</c> before, which is the one answer none of them may have: five are
/// constants HTML §8.9 mandates for every user agent, and the rest are read inside arithmetic and
/// comparisons where an absent value propagates silently — <c>navigator.appVersion.indexOf(…)</c>,
/// still the shape of a great deal of legacy sniffing, threw outright.
/// </remarks>
public class NavigatorIdentityTests
{
    private static string ExecJs(string jsCode)
    {
        var html = $@"<!doctype html>
<html><head><title>T</title></head><body><div id=""result""></div>
<script>
{jsCode}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
    }

    // §8.9 fixes these for every browser regardless of engine, so that sniffing them tells a page
    // nothing. Returning anything else would be the deviation.
    [Fact(Timeout = 600000)]
    public void The_Legacy_Identity_Constants_Are_The_Specified_Values()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'V:' +
                [navigator.appCodeName, navigator.appName, navigator.product, navigator.productSub].join('|');
        ");
        Assert.Contains("V:Mozilla|Netscape|Gecko|20030107", result);
    }

    // appVersion is derived from the one user-agent string, so the two provably cannot drift.
    [Fact(Timeout = 600000)]
    public void AppVersion_Is_The_User_Agent_Without_Its_Mozilla_Prefix()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'V:' + (('Mozilla/' + navigator.appVersion) === navigator.userAgent);
        ");
        Assert.Contains("V:true", result);
    }

    // The shape a great deal of legacy sniffing still takes; it threw while appVersion was absent.
    [Fact(Timeout = 600000)]
    public void The_Legacy_AppVersion_Sniff_Does_Not_Throw()
    {
        var result = ExecJs(@"
            var out;
            try { out = 'ok:' + (navigator.appVersion.indexOf('Win') >= 0); }
            catch (e) { out = 'threw:' + e.message; }
            document.getElementById('result').textContent = 'V:' + out;
        ");
        Assert.Contains("V:ok:true", result);
    }

    // The honest answer, not the flattering one: a capture engine IS controlled by automation.
    [Fact(Timeout = 600000)]
    public void Webdriver_Is_True()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'V:' + typeof navigator.webdriver + ':' + navigator.webdriver;
        ");
        Assert.Contains("V:boolean:true", result);
    }

    [Fact(Timeout = 600000)]
    public void MaxTouchPoints_Is_Zero()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'V:' + typeof navigator.maxTouchPoints + ':' + navigator.maxTouchPoints;
        ");
        Assert.Contains("V:number:0", result);
    }

    // Measured from the real machine, and deviceMemory is coarsened to the values the Device Memory
    // specification allows rather than reported precisely.
    [Fact(Timeout = 600000)]
    public void The_Hardware_Members_Are_Numbers_In_Their_Specified_Ranges()
    {
        var result = ExecJs(@"
            var allowed = [0.25, 0.5, 1, 2, 4, 8];
            document.getElementById('result').textContent = 'V:' +
                (typeof navigator.hardwareConcurrency) + ',' + (navigator.hardwareConcurrency >= 1) +
                '|' + (typeof navigator.deviceMemory) + ',' + (allowed.indexOf(navigator.deviceMemory) >= 0);
        ");
        Assert.Contains("V:number,true|number,true", result);
    }

    // vendor was already a conforming, truthful value and is deliberately unchanged: §8.9 permits
    // "" and Broiler's user agent does not claim to be Chrome.
    [Fact(Timeout = 600000)]
    public void Vendor_Remains_The_Empty_String()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'V:' + typeof navigator.vendor + ':[' + navigator.vendor + ']';
        ");
        Assert.Contains("V:string:[]", result);
    }

    [Fact(Timeout = 600000)]
    public void OffscreenBuffering_Reads_As_A_Boolean()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'V:' + typeof window.offscreenBuffering + ':' + window.offscreenBuffering;
        ");
        Assert.Contains("V:boolean:true", result);
    }

    // The members that already answered, and the neighbouring bindings, must be untouched.
    [Fact(Timeout = 600000)]
    public void The_Existing_Navigator_Surface_Is_Unchanged()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'V:' +
                [typeof navigator.userAgent, navigator.platform, navigator.language,
                 navigator.cookieEnabled, navigator.onLine].join('|') +
                '|CAP:' + [typeof navigator.javaEnabled, typeof navigator.getGamepads,
                           navigator.plugins.length].join(',');
        ");
        Assert.Contains("V:string|Win32|en-US|true|true|CAP:function,function,0", result);
    }
}
