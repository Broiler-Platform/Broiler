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
/// it prefers the engine's own module machinery on the authorised <c>ModuleRoots</c>, keeping the linker as
/// the fallback for a non-module-context parent (verified by <see cref="Iframe_Module_Linker_Fallback_On_Plain_Context"/>).
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
    public void Iframe_Module_Linker_Fallback_On_Plain_Context()
    {
        // A plain (non-module) JSContext parent must still run the iframe's module — via the EsModuleLinker
        // fallback — so the migration does not regress the non-engine path.
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, FrameHost, "file:///host.html");

        context.Eval(SetSrcdocWithModule);

        Assert.Equal("eng", context.Eval(ReadSubDocMarker).StringValue);
    }
}
