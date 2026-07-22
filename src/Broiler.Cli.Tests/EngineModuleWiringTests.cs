using System.Linq;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Verifies the DomBridge → engine module path wiring. A page's <c>&lt;script type="module"&gt;</c> with a
/// static import runs and mutates the DOM through whichever path is active: the JS engine's own module
/// machinery (<see cref="BridgeModuleContext"/>) when the engine binds imports
/// (<see cref="EngineModuleSupport.Available"/>, i.e. submodule patches 0010/0011 present), or the
/// <c>EsModuleLinker</c> fallback otherwise. The test drives <see cref="ScriptEngine"/> the same way
/// <c>RenderingPipeline</c> does — engine roots + linker-free deferred when available, linked strings
/// otherwise — so it is green on both engine states and pins the gating contract.
/// </summary>
public class EngineModuleWiringTests
{
    private const string Url = "file:///page.html";

    private const string ModuleHtml =
        "<html><head>" +
        "<script type=\"module\">" +
        "import { tag } from 'data:text/javascript,export const tag = \"wired\";';" +
        "document.getElementById('x').setAttribute('data-msg', tag);" +
        "</script></head><body><div id=\"x\"></div></body></html>";

    [Fact]
    public void ExtractAll_Exposes_Module_Roots()
    {
        var extraction = ScriptExtractionService.ExtractAll(ModuleHtml, Url);
        Assert.NotEmpty(extraction.ModuleRoots);
        Assert.All(extraction.ModuleRoots, r => Assert.False(string.IsNullOrEmpty(r.Source)));
    }

    [Fact]
    public void Module_Import_Binds_And_Touches_Dom_Via_Active_Path()
    {
        var extraction = ScriptExtractionService.ExtractAll(ModuleHtml, Url);

        // Mirror RenderingPipeline's capability gate: when the engine binds imports, run the roots via the
        // engine and keep the linked module strings out of the deferred bucket; otherwise fall back to the
        // linker (linked strings appended to deferred, no roots).
        var useEngine = extraction.ModuleRoots.Count > 0 && EngineModuleSupport.Available;
        var deferred = useEngine
            ? extraction.DeferredScripts
            : extraction.DeferredScripts.Concat(extraction.ModuleScripts).ToArray();
        var roots = useEngine ? extraction.ModuleRoots : null;

        var output = new ScriptEngine().Execute(extraction.Scripts, deferred, ModuleHtml, Url, roots);

        Assert.NotNull(output);
        Assert.Contains("data-msg=\"wired\"", output);
    }
}
