namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>Node</c> members as a <c>document</c> answers them, and where the three name members
/// actually live.
/// </summary>
/// <remarks>
/// <para>
/// The document was the last node kind still installing <c>Node</c> members of its own, and unlike an
/// element's those were separate implementations rather than copies of the prototype's — a literal
/// <c>9</c> for <c>nodeType</c>, a different <c>childNodes</c> binding — so they were checked against
/// the prototype's answer for a document receiver before being dropped. Checking them is what turned
/// up the four divergences below, three of which are ordinary spec bugs that had nothing to do with
/// the migration.
/// </para>
/// <para>
/// Every expectation is Chromium's measured answer to the same markup.
/// </para>
/// </remarks>
public class DocumentNodeMemberTests
{
    private static string Run(string script)
        => CaptureService.ExecuteScriptsWithDom(
            $@"<!DOCTYPE html><html><body><p id=""p"">hi</p><div id=""result""></div>
<script>
var p = document.getElementById('p');
var t = p.firstChild;
document.getElementById('result').textContent = String({script});
</script></body></html>", "file:///t.html");

    /// <summary>The tree members answer for a document receiver from the prototype.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("document.nodeType", "9")]
    [InlineData("document.nodeName", "#document")]
    [InlineData("String(document.nodeValue)", "null")]
    [InlineData("document.childNodes.length", "2")]
    [InlineData("document.firstChild.nodeName", "html")]
    [InlineData("document.lastChild.nodeName", "HTML")]
    [InlineData("String(document.parentNode)", "null")]
    [InlineData("String(document.parentElement)", "null")]
    [InlineData("String(document.nextSibling)", "null")]
    [InlineData("document.isConnected", "true")]
    [InlineData("document.hasChildNodes()", "true")]
    [InlineData("document.contains(p)", "true")]
    [InlineData("document.getRootNode() === document", "true")]
    [InlineData("document.isSameNode(document)", "true")]
    [InlineData("document.isEqualNode(document)", "true")]
    [InlineData("document.compareDocumentPosition(p) > 0", "true")]
    public void ADocumentAnswersItsNodeMembers(string expression, string expected)
        => Assert.Contains($">{expected}<", Run(expression));

    /// <summary>
    /// And carries none of those five itself any more. The wrapper is built during document
    /// registration, before the interface constructors exist, so it makes its own copies and they are
    /// dropped once there is a prototype to inherit from.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ADocumentNoLongerOwnsThoseMembers()
        => Assert.Contains(">false|false|false|false|false<", Run(
            "['nodeType','nodeName','childNodes','firstChild','lastChild']"
            + ".map(function (k) { return Object.prototype.hasOwnProperty.call(document, k); }).join('|')"));

    /// <summary>
    /// DOM §4.4: a document's <c>ownerDocument</c> is null — it *is* the node document rather than a
    /// node that has one. It answered the document itself.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ADocumentsOwnerDocumentIsNull()
        => Assert.Contains(">null|true<", Run(
            "[String(document.ownerDocument), t.ownerDocument === document].join('|')"));

    /// <summary>
    /// DOM §4.4: <c>textContent</c> is null for a document and for a doctype — the two node kinds the
    /// algorithm has no text for, rather than kinds whose text is empty. Both answered <c>""</c>, so a
    /// page branching on <c>=== null</c> took the wrong path.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TextContentIsNullForADocumentAndADoctype()
        => Assert.Contains(">null|null|hi<", Run(
            "[String(document.textContent), String(document.doctype.textContent), t.textContent].join('|')"));

    /// <summary>
    /// <c>localName</c>, <c>prefix</c> and <c>namespaceURI</c> belong to <c>Element</c>, not to
    /// <c>Node</c>. They were on <c>Node.prototype</c>, so a text node and a document answered
    /// <c>null</c> where a browser answers <c>undefined</c> — neither interface declares them.
    /// Measured: <c>'localName' in Node.prototype</c> is false in Chromium and
    /// <c>Element.prototype</c> owns all three.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheNameMembersBelongToElementNotNode()
        => Assert.Contains(">false|true|undefined|undefined|undefined|undefined<", Run(
            "[('localName' in Node.prototype),"
            + " Object.prototype.hasOwnProperty.call(Element.prototype, 'localName'),"
            + " String(t.localName), String(document.localName),"
            + " String(document.prefix), String(document.namespaceURI)].join('|')"));

    /// <summary>An element still answers all three, from <c>Element.prototype</c>.</summary>
    [Fact(Timeout = 600000)]
    public void AnElementStillAnswersTheNameMembers()
        => Assert.Contains(">p|http://www.w3.org/1999/xhtml|null<", Run(
            "[p.localName, p.namespaceURI, String(p.prefix)].join('|')"));

    /// <summary>
    /// Cloning a document is not supported by the canonical DOM kernel, and a browser does support it
    /// — Chromium answers a fresh node of type 9. Pinned as an explicit, detectable failure rather
    /// than the kernel's own <c>InvalidOperationException</c>, whose message named an internal phase
    /// and reached the page as a plain <c>Error</c> nothing could branch on.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void CloningADocumentFailsAsADomException()
        => Assert.Contains(">DOMException|NotSupportedError<", Run(
            "(function () { try { document.cloneNode(false); return 'no-throw'; }"
            + "catch (e) { return [e.constructor.name, e.name].join('|'); } })()"));
}
