namespace Broiler.Cli.Tests;

/// <summary>
/// A non-element DOM wrapper's prototype is its interface's prototype, so <c>constructor.name</c>
/// and <c>Object.getPrototypeOf</c> answer the interface rather than <c>Object</c>.
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
/// <b>Non-element wrappers only</b>, and the boundary is deliberate: each of these node kinds has
/// exactly one interface fixed by its node type, so the mapping is a fact. An element's interface is
/// a tag question over a table whose entries overlap and which omits tags a browser still names, so
/// guessing there would put a wrong name where <c>"Object"</c> is at least not misleading. That half
/// is asserted below as still open, so this fixture says what is <em>not</em> covered as well as
/// what is. Expectations come from Chromium through Playwright.
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
    /// The boundary, asserted rather than described. An element and an attribute still report
    /// <c>Object</c>: an element's interface is a tag question this does not answer, and an
    /// attribute is not a canonical node, so its wrapper is not minted where the link is applied.
    /// Both are recorded as open — if either starts naming its interface, this fixture should be
    /// updated deliberately rather than the change landing unnoticed.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Element_And_Attribute_Wrappers_Are_Still_Unlinked()
    {
        var result = Run(Box + """
document.getElementById('result').textContent =
  box.constructor.name + '|' + box.getAttributeNode('id').constructor.name;
""");
        Assert.Equal("Object|Object", result);
    }
}
