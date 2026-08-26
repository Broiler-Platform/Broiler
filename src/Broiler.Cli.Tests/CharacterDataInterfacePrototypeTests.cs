namespace Broiler.Cli.Tests;

/// <summary>
/// <c>Node</c>, <c>CharacterData</c> and <c>Text</c> as real interfaces for a text or comment node:
/// the members on the interface prototypes, and nothing on the instance.
/// </summary>
/// <remarks>
/// <para>
/// Every DOM wrapper in this bridge installed its interface as own properties of the object, so
/// <c>Text.prototype.splitText</c> was <c>undefined</c> and a text node carried 57 own properties
/// where a browser gives it none. The prototype <em>chain</em> was already real
/// (<c>Text → CharacterData → Node → EventTarget → Object</c>) and the interface objects already
/// existed; what had not happened is the engine putting its members on them.
/// </para>
/// <para>
/// Character data is the first node interface to move, and the assertions below are the two halves
/// of what moving one means: the members answer from the prototype, and the operations still behave
/// — reading through the receiver rather than a captured node. Expectations are Chromium's measured
/// answers to the same markup.
/// </para>
/// </remarks>
public class CharacterDataInterfacePrototypeTests
{
    private static string Run(string script)
    {
        var html = $@"<!DOCTYPE html><html><body>
<p id=""p"">hello world</p>
<div id=""result""></div>
<script>
var p = document.getElementById('p');
var t = p.firstChild;
document.getElementById('result').textContent = String({script});
</script></body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");
    }

    /// <summary>The interface members answer from the prototype, which is where a page reaches for
    /// them — <c>Text.prototype.splitText</c> is the case this item was written from.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("typeof Text.prototype.splitText", "function")]
    [InlineData("typeof CharacterData.prototype.appendData", "function")]
    [InlineData("typeof CharacterData.prototype.substringData", "function")]
    [InlineData("typeof CharacterData.prototype.remove", "function")]
    [InlineData("typeof Node.prototype.cloneNode", "function")]
    [InlineData("typeof Node.prototype.contains", "function")]
    [InlineData("typeof Node.prototype.normalize", "function")]
    [InlineData("typeof Object.getOwnPropertyDescriptor(CharacterData.prototype, 'data').get", "function")]
    [InlineData("typeof Object.getOwnPropertyDescriptor(Node.prototype, 'firstChild').get", "function")]
    public void TheMemberLivesOnTheInterfacePrototype(string expression, string expected)
        => Assert.Contains($">{expected}<", Run(expression));

    /// <summary>
    /// And nothing of the interface is left on the instance. The three that remain are
    /// <c>EventTarget</c>'s, which stay on the object deliberately — the realm's own
    /// <c>EventTarget.prototype</c> stores listeners where this bridge's dispatch would not find
    /// them, so they cannot simply be inherited.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void TheInstanceCarriesNoneOfTheInterface()
        => Assert.Contains(">addEventListener,dispatchEvent,removeEventListener<",
            Run("Object.getOwnPropertyNames(t).sort().join(',')"));

    /// <summary><c>splitText</c> is <c>Text</c>'s alone: a comment inherits <c>CharacterData</c> and
    /// must not answer it.</summary>
    [Fact(Timeout = 600000)]
    public void SplitTextIsTextsAloneAndNotACommentsProto()
        => Assert.Contains(">false|true<",
            Run("[('splitText' in document.createComment('c')), ('appendData' in document.createComment('c'))].join('|')"));

    /// <summary>The node constants are inherited from <c>Node.prototype</c> rather than copied onto
    /// each node — 18 own properties a browser does not give an instance either.</summary>
    [Fact(Timeout = 600000)]
    public void TheNodeConstantsAreInheritedNotCopied()
        => Assert.Contains(">3|1|8|true<",
            Run("[t.TEXT_NODE, t.ELEMENT_NODE, t.COMMENT_NODE, "
                + "Object.prototype.hasOwnProperty.call(Node.prototype, 'TEXT_NODE')].join('|')"));

    /// <summary>The operations still behave, which is the half that reading through the receiver has
    /// to preserve.</summary>
    [Fact(Timeout = 600000)]
    public void TheOperationsStillBehave()
        => Assert.Contains(">hello world|11|3|#text|true|he|hello world!<", Run(
            "(function () {"
            + "var r = [t.data, t.length, t.nodeType, t.nodeName, t.parentNode === p, t.substringData(0, 2)];"
            + "t.appendData('!'); r.push(t.data); return r.join('|'); })()"));

    /// <summary>
    /// DOM §4.11 splits a text node <em>in place</em>, so the node keeps its script identity. It did
    /// not: the wrapper was dropped after a split to invalidate members that captured state, and the
    /// next one minted for the node was a different object. Chromium answers <c>true</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void SplitTextKeepsTheNodesIdentity()
        => Assert.Contains(">hello| world|2|true<", Run(
            "(function () { var n = t.splitText(5);"
            + "return [t.data, n.data, p.childNodes.length, p.firstChild === t].join('|'); })()"));

    /// <summary>
    /// Reaching the node through the receiver is also what makes an illegal invocation a
    /// <c>TypeError</c> rather than a crash or a silent wrong answer.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AForeignReceiverIsATypeError()
        => Assert.Contains(">TypeError|TypeError<", Run(
            "(function () { var r = [];"
            + "try { Text.prototype.splitText.call({}, 1); r.push('no-throw'); } catch (e) { r.push(e.constructor.name); }"
            + "try { Object.getOwnPropertyDescriptor(CharacterData.prototype, 'data').get.call({}); r.push('no-throw'); }"
            + "catch (e) { r.push(e.constructor.name); }"
            + "return r.join('|'); })()"));

    /// <summary>
    /// A page extending the interface prototype reaches instances — the ordinary polyfill idiom, and
    /// the reason the members being on the prototype is not only a matter of shape.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void APageCanExtendTheInterfacePrototype()
        => Assert.Contains(">HELLO WORLD<", Run(
            "(function () { CharacterData.prototype.shout = function () { return this.data.toUpperCase(); };"
            + "return t.shout(); })()"));
}
