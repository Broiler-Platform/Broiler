using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 7 tail (increment 2): a <c>&lt;script type="module"&gt;</c> inside an iframe runs through the engine
/// module path when the page is engine-driven — i.e. the parent's JS context is a
/// <see cref="BridgeModuleContext"/> and <see cref="EngineModuleSupport.Available"/> — exactly like the main
/// page. Before this, <c>ExecuteSubDocumentScripts</c> always ran the linked <c>ModuleScripts</c> strings; now
/// it runs the authorised <c>ModuleRoots</c> through the engine's own module machinery. The string-rewriting
/// <c>EsModuleLinker</c> fallback was retired (Phase 7 tail), so a sub-document module needs an engine-driven
/// parent (a <c>JSModuleContext</c>); under a plain-<c>JSContext</c> parent it is not executed — a documented
/// limitation pinned by <see cref="Iframe_Module_Not_Run_Under_Plain_Context_Parent"/>.
/// </summary>
public sealed class SubDocumentEngineModuleTests
{
    private const string FrameHost = "<!DOCTYPE html><html><body><iframe id=\"fr\"></iframe></body></html>";

    // Builds an iframe srcdoc whose body has a <div id="t"> mutated by a module that statically imports a
    // value from a data: module. import/export only runs as a module — a classic-script eval would throw.
    private const string SetSrcdocWithModule = """
        (() => {
            var f = document.getElementById('fr');
            f.srcdoc = '<!DOCTYPE html><html><body><div id="t"></div>'
                + '<scr' + 'ipt type="module">'
                + 'import { v } from "data:text/javascript,export const v = \'eng\';";'
                + 'document.getElementById("t").setAttribute("data-m", v);'
                + '</scr' + 'ipt></body></html>';
        })()
        """;

    private const string ReadSubDocMarker =
        "document.getElementById('fr').contentDocument.getElementById('t').getAttribute('data-m')";

    [Fact]
    public void Iframe_Module_Runs_Through_Engine_Path_When_Parent_Is_ModuleContext()
    {
        Assert.True(EngineModuleSupport.Available,
            "engine module path expected active on the pinned engine (98b07636)");

        using var context = new BridgeModuleContext(csp: null, pageUrl: "file:///host.html");
        var bridge = new DomBridge();
        bridge.Attach(context, FrameHost, "file:///host.html");

        context.Eval(SetSrcdocWithModule);

        // The iframe's module imported the value and wrote it onto the sub-document DOM.
        Assert.Equal("eng", context.Eval(ReadSubDocMarker).StringValue);
    }

    [Fact]
    public void Iframe_Module_Not_Run_Under_Plain_Context_Parent()
    {
        // With the EsModuleLinker fallback retired, an iframe module needs an engine-driven (JSModuleContext)
        // parent. Under a plain JSContext parent the module is not executed, so its DOM effect is absent.
        // (This is the documented limitation of the engine-only module path.)
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, FrameHost, "file:///host.html");

        context.Eval(SetSrcdocWithModule);

        // The <div id="t"> exists but the module never ran, so no data-m attribute was written.
        Assert.NotEqual("eng", context.Eval(ReadSubDocMarker).StringValue);
    }
}
