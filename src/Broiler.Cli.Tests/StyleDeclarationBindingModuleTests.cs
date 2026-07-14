using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 slice P3.14: the CSSOM
/// <c>CSSStyleDeclaration</c> surface (the writable <c>element.style</c> and rule <c>style</c>, plus the
/// read-only <c>getComputedStyle</c> result) is now a co-located binding module
/// (<see cref="StyleDeclarationBinding"/>). Like <see cref="ClassListBinding"/> it is an internal static
/// class with no host contract. The characterizations exercise the extracted surface end-to-end.
/// </summary>
public sealed class StyleDeclarationBindingModuleTests
{
    private static DomBridge Attach(out JSContext context, string html = "<!DOCTYPE html><html><body></body></html>")
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        return bridge;
    }

    [Fact]
    public void StyleDeclaration_Feature_Module_Is_Co_Located_And_Internal_Static()
    {
        var moduleType = typeof(StyleDeclarationBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType is { IsAbstract: true, IsSealed: true }); // C# static class
    }

    [Fact]
    public void ElementStyle_CamelCase_And_Kebab_And_CssText_Reflect_One_State()
    {
        using var bridge = Attach(out var context,
            "<!DOCTYPE html><html><body><div id='d'></div></body></html>");

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                d.style.backgroundColor = 'red';                 // camelCase set
                var viaGetProp = d.style.getPropertyValue('background-color');
                d.style.setProperty('margin-top', '4px');
                var cssText = d.style.cssText;
                var attr = d.getAttribute('style');              // P4.7 write-through
                return d.style.backgroundColor + '|' + viaGetProp + '|' + d.style.marginTop + '|' +
                       (cssText.indexOf('background-color: red') >= 0) + '|' +
                       (attr.indexOf('margin-top') >= 0);
            })()
            """);

        Assert.Equal("red|red|4px|true|true", result.ToString());
    }

    [Fact]
    public void ElementStyle_RemoveProperty_And_Length_And_Item_Work_Through_The_Module()
    {
        using var bridge = Attach(out var context,
            "<!DOCTYPE html><html><body><div id='d' style='color:blue;padding:2px'></div></body></html>");

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                var startLen = d.style.length;
                var removed = d.style.removeProperty('color');
                return startLen + '|' + removed + '|' + d.style.length + '|' + (d.style.color === '');
            })()
            """);

        // Two declared props → remove 'color' returns 'blue' → length 1, color now empty.
        Assert.Equal("2|blue|1|true", result.ToString());
    }

    [Fact]
    public void ElementStyle_CssFloat_Maps_To_Float_Property_Through_The_Module()
    {
        using var bridge = Attach(out var context,
            "<!DOCTYPE html><html><body><div id='d'></div></body></html>");

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                d.style.cssFloat = 'left';
                return d.style.cssFloat + '|' + d.style.getPropertyValue('float');
            })()
            """);

        Assert.Equal("left|left", result.ToString());
    }

    [Fact]
    public void GetComputedStyle_Read_Only_Declaration_Is_Built_By_The_Module()
    {
        using var bridge = Attach(out var context,
            "<!DOCTYPE html><html><head><style>#d{color:rgb(1,2,3)}</style></head>" +
            "<body><div id='d'></div></body></html>");

        var result = context.Eval("""
            (() => {
                var d = document.getElementById('d');
                var cs = window.getComputedStyle(d);
                // The module builds the read-only declaration; the dot-access and getPropertyValue
                // paths must agree, be non-empty, and reflect the authored channel values.
                var dot = cs.color;
                var viaGet = cs.getPropertyValue('color');
                return (dot === viaGet) + '|' + (dot.length > 0) + '|' + (dot.indexOf('1') >= 0 && dot.indexOf('2') >= 0 && dot.indexOf('3') >= 0);
            })()
            """);

        Assert.Equal("true|true|true", result.ToString());
    }

    [Fact]
    public void StyleSheet_Rule_Declaration_Is_Built_By_The_Module()
    {
        using var bridge = Attach(out var context,
            "<!DOCTYPE html><html><head><style>#d{color:green;font-size:12px}</style></head>" +
            "<body><div id='d'></div></body></html>");

        var result = context.Eval("""
            (() => {
                var rule = document.styleSheets[0].cssRules[0];
                var before = rule.style.getPropertyValue('color');
                rule.style.setProperty('color', 'purple');
                return before + '|' + rule.style.color + '|' + rule.style.getPropertyValue('font-size');
            })()
            """);

        Assert.Equal("green|purple|12px", result.ToString());
    }
}
