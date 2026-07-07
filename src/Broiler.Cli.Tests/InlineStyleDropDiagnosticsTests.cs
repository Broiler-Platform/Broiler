using Broiler.CSS.Dom;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Diagnostic #1b regression tests: inline <c>style</c> declarations the DomBridge
/// drops (via its own <c>IsAcceptableCssValue</c> filter) must be surfaced through
/// <see cref="CssEngineDiagnostics.DeclarationRejected"/>.
///
/// <para>
/// The bridge rewrites the serialized <c>style</c> attribute from the survivors of
/// that filter, so a dropped inline declaration disappears from the rendered output
/// before the renderer's style engine ever sees it — it is silent unless reported
/// here. These tests drive the JavaScript ingestion paths (<c>setAttribute("style")</c>,
/// <c>element.style = "…"</c>, <c>element.style.cssText = "…"</c>) that mutate
/// <c>element.Style</c> and assert the drop is both reported and silent in the output.
/// </para>
/// </summary>
public sealed class InlineStyleDropDiagnosticsTests
{
    private const string Html =
        "<!DOCTYPE html><html><body><div id=\"t\"></div></body></html>";

    [Fact]
    public void SetAttribute_Style_With_Invalid_Value_Reports_The_Drop()
    {
        var rejected = RunWithDiagnostics(
            "document.getElementById('t').setAttribute('style', 'position: wobble; color: red');");

        Assert.Contains(("position", "wobble"), rejected);
        // Valid declarations must never be reported.
        Assert.DoesNotContain(rejected, e => e.Property == "color");
    }

    [Fact]
    public void Style_CssText_With_Invalid_Value_Reports_The_Drop()
    {
        var rejected = RunWithDiagnostics(
            "document.getElementById('t').style.cssText = 'display: nonsense; width: 10px';");

        Assert.Contains(("display", "nonsense"), rejected);
        Assert.DoesNotContain(rejected, e => e.Property == "width");
    }

    [Fact]
    public void Assigning_Style_String_Reports_The_Drop()
    {
        var rejected = RunWithDiagnostics(
            "document.getElementById('t').style = 'position: sideways';");

        Assert.Contains(("position", "sideways"), rejected);
    }

    [Fact]
    public void Dropped_Inline_Declaration_Is_Silent_In_Serialized_Output()
    {
        // End-to-end: the dropped declaration must be absent from the serialized
        // HTML (the gap #1b closes), while the valid one survives.
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Html, "file:///inline-drop.html");
        context.Eval(
            "document.getElementById('t').setAttribute('style', 'position: wobble; color: red');");

        var serialized = bridge.SerializeToHtml();

        Assert.DoesNotContain("wobble", serialized);
        Assert.Contains("color: red", serialized);
    }

    [Fact]
    public void Modern_WhiteSpace_Shorthand_Is_Not_Dropped()
    {
        // CSS Text 4: white-space is a shorthand for white-space-collapse and
        // text-wrap-mode. The modern longhand-style values must survive the
        // bridge's IsAcceptableCssValue filter (issue #1272), while a truly
        // bogus keyword is still dropped and reported.
        var rejected = RunWithDiagnostics(
            "document.getElementById('t').setAttribute('style', "
            + "'white-space: break-spaces nowrap; --x: 1; white-space: x-bogus');");

        Assert.DoesNotContain(rejected, e => e.Property == "white-space" && e.Value == "break-spaces nowrap");
        Assert.Contains(("white-space", "x-bogus"), rejected);
    }

    [Fact]
    public void Modern_WhiteSpace_Value_Survives_Serialization()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Html, "file:///inline-drop.html");
        context.Eval(
            "document.getElementById('t').setAttribute('style', 'white-space: preserve-breaks');");

        var serialized = bridge.SerializeToHtml();

        Assert.Contains("preserve-breaks", serialized);
    }

    private static List<(string Property, string Value)> RunWithDiagnostics(string script)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Html, "file:///inline-drop.html");

        var rejected = new List<(string Property, string Value)>();
        CssEngineDiagnostics.DeclarationRejected = (p, v) => rejected.Add((p, v));
        try
        {
            context.Eval(script);
        }
        finally
        {
            CssEngineDiagnostics.DeclarationRejected = null;
        }

        return rejected;
    }
}
