using Broiler.HtmlBridge.Scripting;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 7 tail (increment 3): the CLI capture path (<see cref="CaptureService.ExecuteScriptsWithDom"/>) runs
/// <c>&lt;script type="module"&gt;</c> through the engine module path when the engine binds imports. Before
/// this, capture had no module support at all — a module script was eval'd as a classic script and threw, so
/// its DOM effects were dropped. The change is additive: a page with no modules is unaffected, and when the
/// engine cannot bind imports the modules are simply not run (same net effect as before).
/// </summary>
public sealed class CaptureServiceModuleTests
{
    [Fact]
    public void Capture_Runs_Inline_Module_That_Imports_And_Mutates_Dom()
    {
        Assert.True(EngineModuleSupport.Available,
            "engine module path expected active on the pinned engine (98b07636)");

        var html =
            "<!DOCTYPE html><html><body><div id=\"t\"></div>" +
            "<script type=\"module\">" +
            "import { v } from 'data:text/javascript,export const v = \"cap\";';" +
            "document.getElementById('t').setAttribute('data-m', v);" +
            "</script></body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // The module imported the value and wrote it onto the captured DOM.
        Assert.Contains("data-m=\"cap\"", result);
    }

    [Fact]
    public void Capture_With_No_Scripts_Passes_Through_Untouched()
    {
        // The module bucket is part of the "nothing to run" guard: a page with neither scripts nor a
        // style-affecting CSP returns unchanged.
        const string html = "<!DOCTYPE html><html><body><p>hi</p></body></html>";
        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Equal(html, result);
    }
}
