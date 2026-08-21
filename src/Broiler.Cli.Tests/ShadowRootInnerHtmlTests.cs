using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Setting <c>innerHTML</c> on a shadow root must not inject marker nodes. The fragment parser
/// was given the context element's tag name — <c>#shadow-root</c>, which is not an HTML element —
/// so the round-trip came back with the unparsable open tag as a literal text node
/// ("&lt;#shadow-root&gt;") plus a bogus comment. Both then rendered.
/// </summary>
public class ShadowRootInnerHtmlTests
{
    [Fact(Timeout = 600000)]
    public void ShadowRoot_InnerHtml_ParsesWithoutMarkerNodes()
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><html><body><host-1>L</host-1></body></html>", "file:///t.html");

        var result = context.Eval("""
            var root = document.querySelector('host-1').attachShadow({ mode: 'open' });
            root.innerHTML = '<style>x{}</style>';
            var kinds = [];
            for (var i = 0; i < root.childNodes.length; i++) kinds.push(root.childNodes[i].nodeName);
            root.childNodes.length + ':' + kinds.join(',');
        """).ToString();

        Assert.Equal("1:STYLE", result);
    }
}
