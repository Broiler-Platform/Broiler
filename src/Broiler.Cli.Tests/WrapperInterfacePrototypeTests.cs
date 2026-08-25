namespace Broiler.Cli.Tests;

/// <summary>
/// A DOM wrapper's prototype is its interface's prototype, so <c>constructor.name</c> and
/// <c>Object.getPrototypeOf</c> answer the interface rather than <c>Object</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every wrapper reported <c>constructor.name</c> of <c>"Object"</c>. <c>instanceof</c> already
/// answered correctly — the interface globals carry an <c>@@hasInstance</c> hook reading
/// <c>nodeType</c> — which made the gap narrower than it looks and also more confusing:
/// <c>node instanceof Text</c> was <c>true</c> while <c>node.constructor.name</c> was
/// <c>"Object"</c> and <c>Object.getPrototypeOf(node) === Text.prototype</c> was <c>false</c>.
/// </para>
/// <para>
/// This fixture began as the non-element wrappers alone, with the element and attribute cases
/// asserted as still open: each non-element node kind has exactly one interface fixed by its node
/// type, so the mapping is a fact, while an element's was a tag question over a table whose entries
/// overlapped and which omitted tags a browser still names. Guessing there would have put a wrong
/// name where <c>"Object"</c> is at least not misleading. Both are covered now — the table was
/// rebuilt from Chromium's measured per-tag answers — so the last test is the deliberate update the
/// old assertion asked for rather than a silent flip. <see cref="DomInterfacePrototypeTests"/> owns
/// the element detail. Expectations come from Chromium through Playwright.
/// </para>
/// </remarks>
public class WrapperInterfacePrototypeTests
{
    private static string Run(string script)
    {
        var html = "<!doctype html><html><body><div id=\"box\">text<!--c--></div><div id=\"result\"></div>\n<script>\n"
                   + script + "\n</script></body></html>";
        var serialized = CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
        const string start = "<div id=\"result\">";
        var open = serialized.IndexOf(start, StringComparison.Ordinal);
        Assert.True(open >= 0, $"probe did not run; document was:\n{serialized}");
        open += start.Length;
        var close = serialized.IndexOf("</div>", open, StringComparison.Ordinal);
        Assert.True(close > open, $"probe wrote nothing; document was:\n{serialized}");
        return serialized[open..close];
    }

    private const string Box = "var box = document.getElementById('box');\n";

    /// <summary>Each non-element wrapper names its own interface, and its prototype really is that
    /// interface's.</summary>
    [Fact(Timeout = 600000)]
    public void Each_Non_Element_Wrapper_Names_Its_Interface()
    {
        var result = Run(Box + """
function describe(n) {
  return n.constructor.name + '/' + n.nodeType + '/' + (Object.getPrototypeOf(n) === n.constructor.prototype);
}
document.getElementById('result').textContent = [
  describe(box.firstChild),
  describe(box.lastChild),
  describe(document.createDocumentFragment()),
  describe(document.implementation.createDocumentType('html', '', ''))
].join('|');
""");
        Assert.Equal("Text/3/true|Comment/8/true|DocumentFragment/11/true|DocumentType/10/true", result);
    }

    /// <summary>
    /// The prototype really is in the chain, which is the part that is not cosmetic: extending
    /// <c>Text.prototype</c> — the ordinary polyfill idiom — reaches instances, where before the
    /// assignment went to an object nothing inherited from.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Extending_An_Interface_Prototype_Reaches_Instances()
    {
        var result = Run(Box + """
Text.prototype.describeSelf = function () { return 'text:' + this.nodeType; };
Comment.prototype.describeSelf = function () { return 'comment:' + this.nodeType; };
document.getElementById('result').textContent =
  box.firstChild.describeSelf() + '|' + box.lastChild.describeSelf();
""");
        Assert.Equal("text:3|comment:8", result);
    }

    /// <summary>
    /// The <c>@@hasInstance</c> answers that already worked must keep working — a real prototype
    /// chain alongside a hook is the shape where one could start shadowing the other.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Instanceof_Still_Answers_For_Every_Interface()
    {
        var result = Run(Box + """
document.getElementById('result').textContent = [
  box.firstChild instanceof Text,
  box.lastChild instanceof Comment,
  document.createDocumentFragment() instanceof DocumentFragment,
  box instanceof HTMLElement,
  box instanceof HTMLDivElement,
  box instanceof Node
].join('|');
""");
        Assert.Equal("true|true|true|true|true|true", result);
    }

    /// <summary>
    /// The boundary this fixture used to pin — an element and an attribute reporting <c>Object</c> —
    /// is gone: both now name their interface.
    /// </summary>
    /// <remarks>
    /// This is the deliberate update the old assertion asked for. An element's interface was left
    /// unanswered because it is a tag question the engine's table could not answer; the table was
    /// rebuilt from Chromium's measured answers and made single-valued, and an attribute gained the
    /// explicit link its wrapper needs for not being a canonical node.
    /// <see cref="DomInterfacePrototypeTests"/> owns the detail — the per-tag names, the three-way
    /// fallback and the inheritance chains. What stays here is the pairing with the non-element
    /// wrappers above, so one fixture shows the whole surface answering consistently.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void Element_And_Attribute_Wrappers_Name_Their_Interfaces_Too()
    {
        var result = Run(Box + """
document.getElementById('result').textContent =
  box.constructor.name + '|' + box.getAttributeNode('id').constructor.name;
""");
        Assert.Equal("HTMLDivElement|Attr", result);
    }
}
