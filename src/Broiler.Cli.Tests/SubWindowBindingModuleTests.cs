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
    public void SubWindow_Builder_Moved_Off_The_Bridge()
    {
        // The sub-window object builder + its scroll/getComputedStyle callbacks now live on the module;
        // the bridge no longer declares them.
        var bridgeMethods = typeof(DomBridge)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet();
        foreach (var gone in new[]
                 {
                     "GetOrCreateSubWindow", "GetSubWindowScrollOffset", "SetSubWindowScrollOffsets",
                     "GetParentWindowForSubDocument", "GetSubDocumentScrollingElement",
                     "JsSubDocumentsScroll006Core", "JsSubDocumentsGetComputedStyle009Core",
                 })
            Assert.DoesNotContain(gone, bridgeMethods);

        Assert.NotNull(typeof(SubWindowBinding).GetMethod("GetOrCreate",
            BindingFlags.Public | BindingFlags.Instance));
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
}
