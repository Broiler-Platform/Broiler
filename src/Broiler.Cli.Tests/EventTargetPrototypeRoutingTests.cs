namespace Broiler.Cli.Tests;

/// <summary>
/// <c>EventTarget.prototype</c>'s three methods, and the receivers they have to serve.
/// </summary>
/// <remarks>
/// <para>
/// The realm carries its own <c>EventTarget</c>, a JS-engine class keeping its listeners in fields on
/// the C# instance. A DOM wrapper is a plain object and never one of those, so
/// <c>node instanceof EventTarget</c> answered <see langword="true"/> — the interface graph says so —
/// while <c>EventTarget.prototype.addEventListener.call(node, 'x', fn)</c> was a
/// <c>TypeError: Failed to convert this to EventTarget</c>. Borrowing the prototype method is
/// ordinary defensive code, so the listener was silently never added.
/// </para>
/// <para>
/// The bridge's own methods were separate functions installed on every wrapper, so
/// <c>node.addEventListener === EventTarget.prototype.addEventListener</c> was <c>false</c> and each
/// advertised a <c>length</c> of 3. Chromium answers <c>true</c> and <c>2</c>; every expectation
/// below is its measured answer to the same markup.
/// </para>
/// </remarks>
public class EventTargetPrototypeRoutingTests
{
    private static string Run(string script)
    {
        var html = $@"<!DOCTYPE html><html><body>
<p id=""p"">hello</p>
<div id=""result""></div>
<script>
var p = document.getElementById('p');
var t = p.firstChild;
document.getElementById('result').textContent = String({script});
</script></body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///t.html");
    }

    /// <summary>
    /// There is one function, on the prototype, for every receiver the bridge owns — which is what
    /// <c>node instanceof EventTarget</c> already claimed.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("p.addEventListener === EventTarget.prototype.addEventListener")]
    [InlineData("t.addEventListener === EventTarget.prototype.addEventListener")]
    [InlineData("document.addEventListener === EventTarget.prototype.addEventListener")]
    [InlineData("p.removeEventListener === EventTarget.prototype.removeEventListener")]
    [InlineData("p.dispatchEvent === EventTarget.prototype.dispatchEvent")]
    public void OneFunctionServesEveryReceiver(string expression)
        => Assert.Contains(">true<", Run(expression));

    /// <summary>
    /// The bug this closes: borrowing the prototype method registered nothing and threw. A listener
    /// added that way now fires.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void BorrowingThePrototypeMethodRegistersTheListener()
        => Assert.Contains(">1|1<", Run(
            "(function () { var a = 0, b = 0;"
            + "EventTarget.prototype.addEventListener.call(p, 'x', function () { a++; });"
            + "EventTarget.prototype.addEventListener.call(t, 'x', function () { b++; });"
            + "EventTarget.prototype.dispatchEvent.call(p, new Event('x'));"
            + "EventTarget.prototype.dispatchEvent.call(t, new Event('x'));"
            + "return [a, b].join('|'); })()"));

    /// <summary>Web IDL's argument counts, which the per-wrapper copies advertised as 3, 3, 1.</summary>
    [Fact(Timeout = 600000)]
    public void TheArgumentCountsAreWebIdls()
        => Assert.Contains(">2|2|1<", Run(
            "[EventTarget.prototype.addEventListener.length,"
            + "EventTarget.prototype.removeEventListener.length,"
            + "EventTarget.prototype.dispatchEvent.length].join('|')"));

    /// <summary>
    /// Every receiver the bridge owns still reaches its own listener store: an element, a text node,
    /// the document and the window each register and dispatch, and an event still bubbles to an
    /// ancestor's listener.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void EveryBridgeReceiverStillRegistersAndDispatches()
        => Assert.Contains(">1|1|1|1|1<", Run(
            "(function () { var r = [];"
            + "function count(target, type, dispatchOn) { var n = 0;"
            + "  target.addEventListener(type, function () { n++; });"
            + "  (dispatchOn || target).dispatchEvent(new Event(type, { bubbles: true }));"
            + "  return n; }"
            + "r.push(count(p, 'e1'));"
            + "r.push(count(t, 'e2'));"
            + "r.push(count(document, 'e3'));"
            + "r.push(count(window, 'e4'));"
            + "r.push(count(document.body, 'e5', p));"
            + "return r.join('|'); })()"));

    /// <summary><c>removeEventListener</c> through the same one function still removes.</summary>
    [Fact(Timeout = 600000)]
    public void RemoveEventListenerStillRemoves()
        => Assert.Contains(">0<", Run(
            "(function () { var n = 0; var h = function () { n++; };"
            + "p.addEventListener('r', h); p.removeEventListener('r', h);"
            + "p.dispatchEvent(new Event('r')); return n; })()"));

    /// <summary>
    /// A receiver the bridge does not own is handed back to the function the engine installed, so its
    /// own targets are untouched — and one that is neither is still a <c>TypeError</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AnEngineTargetIsUntouchedAndAForeignReceiverThrows()
        => Assert.Contains(">object|TypeError<", Run(
            "(function () { var r = [];"
            + "r.push(typeof new EventTarget());"
            + "try { EventTarget.prototype.addEventListener.call({}, 'x', function () {}); r.push('no-throw'); }"
            + "catch (e) { r.push(e.constructor.name); }"
            + "return r.join('|'); })()"));

    /// <summary>
    /// With the copies gone, a text node carries no own properties at all — the shape a browser gives
    /// it, and what the character-data interface move left three short of.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ATextNodeCarriesNoOwnPropertiesAtAll()
        => Assert.Contains(">0<", Run("Object.getOwnPropertyNames(t).length"));
}
