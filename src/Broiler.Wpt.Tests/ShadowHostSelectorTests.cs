using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression guard for shadow-tree rendering (the WPT <c>css/css-shadow</c> family, issue #1473).
///
/// A shadow root's <c>&lt;style&gt;</c> is serialized inline as a global rule, and the renderer's
/// selector matcher treats <c>:host</c> as a recognised-but-unmodelled pseudo-class, which is
/// lenient and matches every element — so a single <c>:host</c> rule painted the whole document.
/// <c>DomBridge.ScopeShadowHostSelectors</c> rewrites each <c>:host</c> to target its own root's
/// host; <c>HideUnslottedShadowHostChildren</c> drops light children a slotless shadow tree can
/// never assign; and <c>UnwrapShadowRootsForRender</c> flattens the <c>#shadow-root</c> wrapper,
/// whose literal <c>&lt;#shadow-root&gt;</c> serialization the renderer painted as text.
/// </summary>
public class ShadowHostSelectorTests : IDisposable
{
    public void Dispose() => Program.ResetTestHooks();

    private static (int Red, int Green, int MinX, int MinY, int MaxX, int MaxY) Render(
        string body, string script, int w = 300, int h = 300)
    {
        string dir = Path.Combine(Path.GetTempPath(), "broiler-shadowhost-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string html = "<!DOCTYPE html><html><head></head><body>" + body +
                          "<script>" + script + "</script></body></html>";
            string file = Path.Combine(dir, "t.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(w, h);
            using var bmp = runner.RenderHtmlFileBitmapPublic(file, dir);

            int red = 0, green = 0, minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.R > 150 && p.G < 100) red++;
                    if (p.G > 100 && p.R < 100)
                    {
                        green++;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }

            return (red, green, minX, minY, maxX, maxY);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    // The WPT test css/css-shadow/css-scoping-shadow-host-functional-rule: five hosts, each with a
    // shadow root whose style uses :host(). The reference is a single 100x100 green box, so every
    // host must end up green and no FAIL text may show.
    private const string FiveHostsBody = """
<style>
host-1, host-2, host-3, host-4, host-5 { display: block; width: 100px; height: 20px; background: red; }
host-3, host-4, host-5 { background: green; }
</style>
<p>Test passes if you see a single 100px by 100px green box below.</p>
<host-1><div>FAIL1</div></host-1>
<host-2 id="bar" class="foo" name="baz"><div>FAIL2</div></host-2>
<div><host-3>FAIL3</host-3></div>
<host-4><div class="child">FAIL4</div></host-4>
<host-5><div>FAIL5</div></host-5>
""";

    private const string FiveHostsScript = """
var h=document.querySelector('host-1');var r=h.attachShadow({mode:'open'});
r.innerHTML='<style> :host(host-1) { background: green !important; } </style>';
h=document.querySelector('host-2');r=h.attachShadow({mode:'open'});
r.innerHTML='<style> :host(host-2.foo#bar[name=baz]) { background: green !important; } </style>';
h=document.querySelector('host-3');r=h.attachShadow({mode:'open'});
r.innerHTML='<style> :host(div host-3) { background: red !important; } </style>';
h=document.querySelector('host-4');r=h.attachShadow({mode:'open'});
r.innerHTML='<style> :host(.child) { background: red !important; } </style>';
h=document.querySelector('host-5');r=h.attachShadow({mode:'open'});
r.innerHTML='<style> :host(host-1) { background: red !important; } </style>';
""";

    [Fact]
    public void HostFunctionalRule_RendersSingleSolidGreenBox()
    {
        var (red, green, minX, minY, maxX, maxY) = Render(FiveHostsBody, FiveHostsScript);

        // :host(host-1) and :host(host-2...) must match their own hosts; :host(div host-3) is an
        // invalid complex argument, :host(.child) matches a child not the host, and host-5's
        // :host(host-1) must not reach host-1 — so nothing is red.
        Assert.Equal(0, red);

        // A single solid 100x100 green box: no FAIL text and no "<#shadow-root>" text inside it.
        Assert.Equal(100, maxX - minX + 1);
        Assert.Equal(100, maxY - minY + 1);
        Assert.Equal(100 * 100, green);
    }

    [Fact]
    public void HostSelector_DoesNotLeakToTheWholeDocument()
    {
        // Guard the original failure directly: a bare :host rule in one shadow tree must style
        // only its host, not every element on the page.
        var (red, _, _, _, _, _) = Render(
            "<style>host-1{display:block;width:50px;height:20px}</style><p>text</p><host-1>x</host-1>",
            "document.querySelector('host-1').attachShadow({mode:'open'})" +
            ".innerHTML='<style>:host{background:red}</style>';");

        // Only the 50x20 host is red — not the 300x300 canvas.
        Assert.InRange(red, 1, 50 * 20);
    }

    [Fact]
    public void SlotlessShadowRoot_HidesHostLightChildren()
    {
        // With no <slot> in the shadow tree the light children can never be assigned, so they
        // generate no boxes — the green host box must be unbroken by the light-DOM text.
        var (_, green, minX, minY, maxX, maxY) = Render(
            "<style>host-1{display:block;width:100px;height:40px;background:green}</style>" +
            "<host-1><div>FAILTEXT</div></host-1>",
            "document.querySelector('host-1').attachShadow({mode:'open'}).innerHTML='<style>x{}</style>';");

        Assert.Equal(100, maxX - minX + 1);
        Assert.Equal(40, maxY - minY + 1);
        Assert.Equal(100 * 40, green);
    }
}
