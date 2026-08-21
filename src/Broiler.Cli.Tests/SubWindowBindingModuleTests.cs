using System.Linq;
using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 slice P3.17: the nested-browsing-context
/// <c>window</c> (sub-window) object — its document/location/scroll/getComputedStyle surface and the
/// sub-window-scoped helpers — is now a co-located binding module (<see cref="SubWindowBinding"/>)
/// consumed through the explicit <see cref="ISubWindowHost"/> contract, and no longer scattered across
/// <c>SubDocuments.cs</c>. The characterization exercises the extracted surface end-to-end via an
/// <c>&lt;iframe srcdoc&gt;</c>'s <c>contentWindow</c>.
/// </summary>
public sealed class SubWindowBindingModuleTests
{
    private static DomBridge Attach(out JSContext context, string html)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        return bridge;
    }

    [Fact]
    public void SubWindow_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(SubWindowBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.False(typeof(ISubWindowHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Consumes_SubWindow_Through_The_Host_Contract()
    {
        Assert.True(typeof(ISubWindowHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(SubWindowBinding));
    }

    [Fact]
    public void ContentWindow_Exposes_SubWindow_Surface_Through_The_Module()
    {
        const string html =
            "<!DOCTYPE html><html><body>" +
            "<iframe id='f' srcdoc='<!DOCTYPE html><body></body>'></iframe>" +
            "</body></html>";
        using var bridge = Attach(out var context, html);

        var result = context.Eval("""
            (() => {
              const w = document.getElementById('f').contentWindow;
              return [
                typeof w.getComputedStyle,
                typeof w.scrollX,
                typeof w.scroll,
                w.self === w,
                w.window === w
              ].join('|');
            })()
            """);

        Assert.Equal("function|number|function|true|true", result.ToString());
    }

    /// <summary>
    /// A frame gets the idle-callback pair its parent has, alongside the animation-frame pair it was
    /// the only scheduling API missing next to. The bare name always resolved — it is a context
    /// global and every document shares the one context — so what was <c>undefined</c> is the
    /// qualified read, which is the one pages actually write: the standard feature test is
    /// <c>window.requestIdleCallback ? … : fallback</c>, and inside a frame <c>window</c> is the
    /// sub-window. A framed page therefore took its no-native path, and one that called
    /// <c>window.requestIdleCallback(cb)</c> unguarded got a TypeError.
    /// </summary>
    [Fact]
    public void A_Frame_Gets_The_Idle_Callback_Pair_Its_Parent_Has()
    {
        const string html =
            "<!DOCTYPE html><html><body>" +
            "<iframe id='f' srcdoc='<!DOCTYPE html><body>" +
            "<script>var seen = typeof window.requestIdleCallback;</script>" +
            "</body>'></iframe>" +
            "</body></html>";
        using var bridge = Attach(out var context, html);
        bridge.FireWindowLoadEvent();
        bridge.FlushTimers();

        var result = context.Eval("""
            (() => {
              const w = document.getElementById('f').contentWindow;
              return [
                'contentWindow=' + typeof w.requestIdleCallback,
                'cancel=' + typeof w.cancelIdleCallback,
                'frames=' + typeof frames[0].requestIdleCallback,
                'insideTheFrame=' + w.seen
              ].join('|');
            })()
            """);

        Assert.Equal("contentWindow=function|cancel=function|frames=function|insideTheFrame=function",
            result.ToString());
    }

    /// <summary>
    /// The mirrored pair is the parent's own function, so a frame that registers through it schedules
    /// on the bridge's one event loop and gets the same <c>IdleDeadline</c> a top-level caller does —
    /// the identity the whole mirror rests on. (Whether a sub-document's <em>own</em> scheduled
    /// callbacks are ever drained is a separate question this does not touch: they are not today, and
    /// a frame's <c>setTimeout</c> is not either.)
    /// </summary>
    [Fact]
    public void The_Mirrored_Idle_Pair_Is_The_Parents_Own_Function()
    {
        const string html =
            "<!DOCTYPE html><html><body>" +
            "<iframe id='f' srcdoc='<!DOCTYPE html><body></body>'></iframe>" +
            "</body></html>";
        using var bridge = Attach(out var context, html);

        var result = context.Eval("""
            (() => {
              const w = document.getElementById('f').contentWindow;
              return [
                w.requestIdleCallback === window.requestIdleCallback,
                w.cancelIdleCallback === window.cancelIdleCallback
              ].join('|');
            })()
            """);

        Assert.Equal("true|true", result.ToString());
    }
}
