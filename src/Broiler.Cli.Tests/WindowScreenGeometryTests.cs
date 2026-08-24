using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

/// <summary>
/// The window and screen geometry that was absent — <c>screenX</c>/<c>screenY</c> (and their
/// <c>screenLeft</c>/<c>screenTop</c> spellings), <c>devicePixelRatio</c>, <c>screen.availLeft</c>/
/// <c>availTop</c> — and the six <c>BarProp</c> objects.
/// </summary>
/// <remarks>
/// Every value here follows from what the capture actually is rather than being a placeholder: the
/// viewport is the screen, so the window sits at its origin and nothing is reserved out of the
/// available area; a CSS pixel is a rendered pixel, so the device pixel ratio is 1; and no browser
/// user interface is painted, so no bar is visible — which the already-published
/// <c>outerWidth == innerWidth</c> says independently.
/// <para>
/// The tests that matter most are the arithmetic ones. These members are read inside expressions far
/// more often than they are read alone, and an absent member is <c>undefined</c>, so the popup-centring,
/// canvas-scaling and available-area idioms each silently produced <c>NaN</c>.
/// </para>
/// </remarks>
public class WindowScreenGeometryTests
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

    [Fact(Timeout = 600000)]
    public void Window_Screen_Position_Is_The_Origin_In_Both_Spellings()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'V:' +
                [window.screenX, window.screenY, window.screenLeft, window.screenTop].join(',');
        ");
        Assert.Contains("V:0,0,0,0", result);
    }

    [Fact(Timeout = 600000)]
    public void DevicePixelRatio_Is_One()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'V:' + typeof window.devicePixelRatio + ':' + window.devicePixelRatio;
        ");
        Assert.Contains("V:number:1", result);
    }

    [Fact(Timeout = 600000)]
    public void Screen_Available_Area_Starts_At_The_Screen_Origin()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'V:' +
                [screen.availLeft, screen.availTop].join(',') +
                ',SIZES:' + (screen.availWidth === screen.width && screen.availHeight === screen.height);
        ");
        Assert.Contains("V:0,0", result);
        Assert.Contains("SIZES:true", result);
    }

    // Each BarProp is its own object carrying a readable `visible`, as in a browser.
    [Fact(Timeout = 600000)]
    public void All_Six_BarProps_Exist_And_Report_Not_Visible()
    {
        var result = ExecJs(@"
            var bars = [window.locationbar, window.menubar, window.personalbar,
                        window.scrollbars, window.statusbar, window.toolbar];
            document.getElementById('result').textContent =
                'TYPES:' + bars.map(function (b) { return typeof b; }).join(',') +
                ',VISIBLE:' + bars.map(function (b) { return b.visible; }).join(',');
        ");
        Assert.Contains("TYPES:object,object,object,object,object,object", result);
        Assert.Contains("VISIBLE:false,false,false,false,false,false", result);
    }

    [Fact(Timeout = 600000)]
    public void Each_BarProp_Is_A_Distinct_Object()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent = 'V:' +
                (window.locationbar !== window.menubar && window.toolbar !== window.statusbar);
        ");
        Assert.Contains("V:true", result);
    }

    // The three idioms that silently produced NaN while the members were undefined.
    [Fact(Timeout = 600000)]
    public void The_Arithmetic_Idioms_Produce_Numbers_Rather_Than_NaN()
    {
        var result = ExecJs(@"
            var centre = window.screenX + (window.outerWidth - 400) / 2;   // centre-on-parent popup
            var backing = 100 * window.devicePixelRatio;                    // canvas backing store
            var right = screen.availLeft + screen.availWidth;               // available area edge
            document.getElementById('result').textContent =
                'NAN:' + [centre, backing, right].some(isNaN) +
                ',VALS:' + [centre, backing, right].join(',');
        ");
        Assert.Contains("NAN:false", result);
        Assert.Contains("VALS:312,100,1024", result);
    }

    // The geometry these were made to agree with must be unchanged.
    [Fact(Timeout = 600000)]
    public void The_Existing_Viewport_And_Screen_Geometry_Is_Unchanged()
    {
        var result = ExecJs(@"
            document.getElementById('result').textContent =
                'V:' + [window.innerWidth, window.outerWidth, screen.width, screen.colorDepth].join(',') +
                ',ORIENT:' + screen.orientation.type +
                ',NOCHROME:' + (window.outerWidth === window.innerWidth);
        ");
        Assert.Contains("V:1024,1024,1024,24", result);
        Assert.Contains("ORIENT:landscape-primary", result);
        Assert.Contains("NOCHROME:true", result);
    }
}
