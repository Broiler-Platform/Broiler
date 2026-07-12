using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// DOM/CSS promotion §2.3 — the live per-property <c>CSSStyleDeclaration</c> setters
/// (<c>el.style.X = …</c>, <c>setProperty(…)</c>, <c>cssFloat = …</c>) route their value through
/// the shared <c>Broiler.CSS.Dom.CssDeclarationValidator</c>, so an invalid value is ignored
/// rather than stored — matching the inline-style <em>attribute</em> path (which already dropped
/// invalid declarations) and CSSOM error handling. Only closed-keyword properties reject; custom
/// and unknown properties still pass (the validator default).
/// </summary>
public sealed class CssStyleDeclarationValidationTests
{
    private static string Eval(string script)
    {
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx,
            "<!DOCTYPE html><html><body><div id=\"t\"></div></body></html>", "file:///v.html");
        return ctx.Eval("(function(){var s=document.getElementById('t').style;" + script + "})()").ToString();
    }

    [Fact]
    public void Invalid_Keyword_On_Property_Setter_Is_Ignored()
    {
        // position:levitating is not a valid <position> keyword → the setter stores nothing.
        Assert.Equal("", Eval("s.position='levitating';return s.position;"));
    }

    [Fact]
    public void Invalid_Keyword_Via_SetProperty_Is_Ignored()
    {
        Assert.Equal("", Eval("s.setProperty('display','supergrid');return s.getPropertyValue('display');"));
    }

    [Fact]
    public void Valid_Value_On_Property_Setter_Still_Applies()
    {
        Assert.Equal("relative", Eval("s.position='relative';return s.position;"));
    }

    [Fact]
    public void Valid_Value_Via_SetProperty_Still_Applies()
    {
        Assert.Equal("block", Eval("s.setProperty('display','block');return s.getPropertyValue('display');"));
    }

    [Fact]
    public void Invalid_Value_Keeps_The_Existing_Value()
    {
        // A rejected set must not clobber a previously-valid value.
        Assert.Equal("relative", Eval("s.position='relative';s.position='levitating';return s.position;"));
    }

    [Fact]
    public void Custom_Property_Via_SetProperty_Is_Accepted()
    {
        // Custom (--*) properties are not closed-keyword; the validator default accepts them.
        Assert.Equal("anything", Eval("s.setProperty('--foo','anything');return s.getPropertyValue('--foo');"));
    }

    [Fact]
    public void Unknown_Property_Value_Is_Accepted()
    {
        // Unknown properties fall through the validator default (accept), so setProperty stores them.
        Assert.Equal("42", Eval("s.setProperty('--x','42');return s.getPropertyValue('--x');"));
    }

    [Fact]
    public void Important_Value_Is_Validated_On_The_Stripped_Value()
    {
        // "red !important" validates the stripped "red" (valid) and is stored with priority.
        Assert.Equal("important",
            Eval("s.setProperty('color','red','important');return s.getPropertyPriority('color');"));
    }

    [Fact]
    public void Invalid_Float_Via_SetProperty_Is_Ignored()
    {
        // `float` set through the general setProperty path (the reachable one) is gated.
        Assert.Equal("", Eval("s.setProperty('float','banana');return s.getPropertyValue('float');"));
        Assert.Equal("left", Eval("s.setProperty('float','left');return s.getPropertyValue('float');"));
    }

    [Fact]
    public void Property_Path_Now_Matches_Attribute_Path_For_Invalid_Values()
    {
        // Both the attribute assignment and the per-property setter drop an invalid keyword.
        Assert.Equal("", Eval("s.cssText='position: levitating';return s.position;"));
        Assert.Equal("", Eval("s.position='levitating';return s.position;"));
    }
}
