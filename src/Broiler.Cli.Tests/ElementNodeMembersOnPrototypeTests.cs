namespace Broiler.Cli.Tests;

/// <summary>
/// An element's <c>Node</c> members, now inherited from <c>Node.prototype</c> rather than copied onto
/// every element wrapper.
/// </summary>
/// <remarks>
/// <para>
/// The members were already on <c>Node.prototype</c> — the character-data interface move put them
/// there — but an element shadowed every one with an own copy that was a byte-identical call to the
/// same binding. Deleting the copies is what makes the prototype the place they live: an element
/// carried 166 own properties where a browser gives it none, and this is 23 of them.
/// </para>
/// <para>
/// The assertions are the two halves of moving a member: it answers from the prototype, and the
/// operation still behaves, now reading the node through its receiver rather than one captured when
/// the wrapper was built.
/// </para>
/// </remarks>
public class ElementNodeMembersOnPrototypeTests
{
    private static string Run(string script)
    {
        var html = $@"<!DOCTYPE html><html><body>
<div id=""d""><span id=""s"">hi</span></div>
<div id=""result""></div>
<script>
var d = document.getElementById('d');
var s = document.getElementById('s');
document.getElementById('result').textContent = String({script});
</script></body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");
    }

    /// <summary>The member is inherited, not owned — which is the whole change.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("childNodes")]
    [InlineData("firstChild")]
    [InlineData("parentNode")]
    [InlineData("nodeType")]
    [InlineData("nodeName")]
    [InlineData("ownerDocument")]
    [InlineData("cloneNode")]
    [InlineData("contains")]
    [InlineData("getRootNode")]
    [InlineData("normalize")]
    public void TheMemberIsInheritedFromNodePrototype(string member)
        => Assert.Contains(">false|true<", Run(
            $"[Object.prototype.hasOwnProperty.call(d, '{member}'),"
            + $" Object.prototype.hasOwnProperty.call(Node.prototype, '{member}')].join('|')"));

    /// <summary>The tree accessors still answer for an element receiver.</summary>
    [Fact(Timeout = 600000)]
    public void TheTreeAccessorsStillAnswer()
        => Assert.Contains(">1|1|DIV|null|s|BODY|BODY|true|true<", Run(
            "[d.nodeType, d.childNodes.length, d.nodeName, String(d.nodeValue), d.firstChild.id,"
            + " d.parentNode.tagName, d.parentElement.tagName, d.ownerDocument === document,"
            + " d.isConnected].join('|')"));

    /// <summary>And the node operations do.</summary>
    [Fact(Timeout = 600000)]
    public void TheNodeOperationsStillBehave()
        => Assert.Contains(">true|true|DIV/1|true|true|true|true<", Run(
            "[d.contains(s), d.hasChildNodes(),"
            + " (function () { var c = d.cloneNode(true); return c.tagName + '/' + c.childNodes.length; })(),"
            + " d.isSameNode(d), d.isEqualNode(d.cloneNode(true)), d.getRootNode() === document,"
            + " d.compareDocumentPosition(s) > 0].join('|')"));

    /// <summary>
    /// <c>textContent</c> is deliberately <em>not</em> among them. An element's is a different
    /// operation from a character-data node's — it reads the descendants' text and writing it
    /// replaces every child with one text node — so it stays the element's own and shadows the
    /// <c>Node.prototype</c> one that character data uses.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TextContentStaysTheElementsOwn()
        => Assert.Contains(">true|hi|abc|1<", Run(
            "(function () { var x = document.createElement('p'); x.textContent = 'abc';"
            + "return [Object.prototype.hasOwnProperty.call(d, 'textContent'), d.textContent,"
            + " x.textContent, x.childNodes.length].join('|'); })()"));

    /// <summary>
    /// A <c>&lt;form&gt;</c>'s wrapper resolves an unknown name to the control carrying it, so an
    /// inherited member has to be found before that fallback runs — otherwise a control named
    /// <c>childNodes</c> would answer for the real one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AFormsNamedGetterDoesNotShadowAnInheritedMember()
        => Assert.Contains(">2|INPUT<", Run(
            "(function () { var f = document.createElement('form');"
            + "var a = document.createElement('input'); a.name = 'childNodes';"
            + "var b = document.createElement('input'); b.name = 'zz';"
            + "f.appendChild(a); f.appendChild(b); document.body.appendChild(f);"
            + "return [f.childNodes.length, f.zz.tagName].join('|'); })()"));
}
