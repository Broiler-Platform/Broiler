using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression test for the <c>Element.remove()</c> bridge bug: the callback read
/// <c>element.Parent</c> (computed from the canonical <c>ParentNode</c>) *after*
/// <c>Children.RemoveAt</c> had already detached it, so the captured parent was null →
/// <c>InvalidateStyleScope(null)</c> threw a <c>NullReferenceException</c> (surfaced to
/// JS as a <c>ReferenceError</c>). This broke any script calling <c>el.remove()</c> —
/// e.g. the CSSOM zoom tests' <c>measure()</c> cleanup. Fixed by capturing the parent
/// before removal, mirroring the working <c>removeChild</c> path.
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class ElementRemoveTests
{
    private static string Eval(string script)
    {
        const string html = "<!DOCTYPE html><html><head></head><body></body></html>";
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///remove.html");
        return ctx.Eval(script).ToString();
    }

    [Fact]
    public void Remove_Detaches_Element_Without_Throwing()
    {
        Assert.Equal("true|0|ok", Eval(
            "(function(){" +
            "var y=document.createElement('div'); document.body.appendChild(y);" +
            "y.remove();" +
            "return (y.parentNode===null) + '|' + document.body.children.length + '|ok';" +
            "})()"));
    }

    [Fact]
    public void Remove_On_Detached_Element_Is_A_Noop()
    {
        Assert.Equal("ok", Eval(
            "(function(){var y=document.createElement('div'); y.remove(); return 'ok';})()"));
    }
}
