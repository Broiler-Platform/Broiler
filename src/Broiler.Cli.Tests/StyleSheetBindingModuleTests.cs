using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 slice P3.15: the CSSOM
/// <c>CSSRuleList</c>/<c>CSSRule</c> object model and its <c>JsStyleSheets*Core</c> callbacks — the
/// sibling of P3.14's <see cref="StyleDeclarationBinding"/> — are now a co-located binding module
/// (<see cref="StyleSheetBinding"/>), an internal static class with no host contract. The
/// <c>CSSStyleSheet</c> object itself (identity cache, live <c>cssRules</c>, mutation bookkeeping) stays
/// bridge-owned and calls into the module. The characterizations exercise the extracted rule surface
/// end-to-end through the JS engine.
/// </summary>
public sealed class StyleSheetBindingModuleTests
{
    private static DomBridge Attach(out JSContext context, string html)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        return bridge;
    }

    [Fact]
    public void StyleSheet_Feature_Module_Is_Co_Located_And_Internal_Static()
    {
        var moduleType = typeof(StyleSheetBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);
        Assert.True(moduleType is { IsAbstract: true, IsSealed: true }); // C# static class
    }

    [Fact]
    public void StyleRule_SelectorText_Style_And_CssText_Are_Built_By_The_Module()
    {
        using var bridge = Attach(out var context,
            "<!DOCTYPE html><html><head><style>#a { color: red; margin-top: 4px; }</style></head><body></body></html>");

        var result = context.Eval("""
            (() => {
                var sheet = document.styleSheets[0];
                var rule = sheet.cssRules[0];
                return sheet.cssRules.length + '|' +
                       rule.type + '|' +
                       rule.selectorText + '|' +
                       rule.style.color + '|' +
                       (rule.cssText.indexOf('#a') >= 0 && rule.cssText.indexOf('color: red') >= 0);
            })()
            """);

        // 1 rule; CSSStyleRule type == 1; selectorText "#a"; rule.style.color "red"; cssText well-formed.
        Assert.Equal("1|1|#a|red|true", result.ToString());
    }

    [Fact]
    public void InsertRule_And_DeleteRule_Mutate_The_Live_CssRules_Collection()
    {
        using var bridge = Attach(out var context,
            "<!DOCTYPE html><html><head><style>#a { color: red; }</style></head><body></body></html>");

        var result = context.Eval("""
            (() => {
                var sheet = document.styleSheets[0];
                var start = sheet.cssRules.length;
                var idx = sheet.insertRule('#b { color: blue; }', 1);
                var afterInsert = sheet.cssRules.length;
                var newSelector = sheet.cssRules[1].selectorText;
                sheet.deleteRule(0);
                var afterDelete = sheet.cssRules.length;
                var remaining = sheet.cssRules[0].selectorText;
                return start + '|' + idx + '|' + afterInsert + '|' + newSelector + '|' + afterDelete + '|' + remaining;
            })()
            """);

        // start 1; inserted at index 1; length 2; new rule "#b"; after deleting rule 0 length 1, remaining "#b".
        Assert.Equal("1|1|2|#b|1|#b", result.ToString());
    }

    [Fact]
    public void AtRule_Media_And_Keyframes_CssText_Are_Built_By_The_Module()
    {
        using var bridge = Attach(out var context,
            "<!DOCTYPE html><html><head><style>" +
            "@media screen { #a { color: red; } }" +
            "@keyframes spin { from { opacity: 0; } to { opacity: 1; } }" +
            "</style></head><body></body></html>");

        var result = context.Eval("""
            (() => {
                var rules = document.styleSheets[0].cssRules;
                var media = rules[0];
                var keyframes = rules[1];
                return rules.length + '|' +
                       media.type + '|' +
                       (media.cssText.indexOf('@media') >= 0) + '|' +
                       keyframes.type + '|' +
                       (keyframes.cssText.indexOf('@keyframes') >= 0);
            })()
            """);

        // 2 rules; CSSMediaRule type == 4; CSSKeyframesRule type == 7; each cssText well-formed.
        Assert.Equal("2|4|true|7|true", result.ToString());
    }
}
