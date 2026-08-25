using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>document.adoptNode</c> (DOM §4.5) and the <c>adoptedCallback</c> reaction it drives.
/// </summary>
/// <remarks>
/// <para>
/// <c>adoptNode</c> was the one document method with no bridge implementation at all, and
/// <c>importNode</c> is not a substitute for it: importing <em>copies</em>, so the node a page holds
/// afterwards is a different one. Adoption moves the node itself, which is why it is the operation a
/// custom element can observe — the element that changed document is the element the page still has
/// a reference to.
/// </para>
/// <para>
/// <b>Both directions are heard.</b> Adoption publishes its mutation on the document the node moves
/// <em>to</em>, so listening to the page's own document alone hears an adoption into the page and
/// misses the symmetric one out of it. Every document this bridge mints is subscribed for that
/// reason.
/// </para>
/// <para>Every expectation is Chromium's measured answer over the same probe run against both.</para>
/// </remarks>
public sealed class CustomElementAdoptionTests
{
    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!DOCTYPE html><html><body><div id=\"host\"></div></body></html>",
            "https://example.com/index.html");
        return bridge;
    }

    private static string Eval(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                {{body}}
            })()
            """).ToString();

    /// <summary><c>adoptNode</c> moves the node rather than copying it: same node, new owner.</summary>
    [Fact(Timeout = 600000)]
    public void AdoptNode_Moves_The_Node_Itself()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true/true/true/null", Eval(context, """
            var node = document.createElement('span');
            node.id = 'moved';
            document.body.appendChild(node);
            var other = document.implementation.createHTMLDocument('x');
            var returned = other.adoptNode(node);
            return (returned === node) + '/' + (node.ownerDocument === other) + '/' +
                   (document.getElementById('moved') === null) + '/' + String(node.parentNode);
            """));
    }

    /// <summary>
    /// An upgraded custom element receives <c>adoptedCallback(oldDocument, newDocument)</c>, after
    /// the <c>disconnectedCallback</c> that adoption's removal from the old tree produces.
    /// </summary>
    /// <remarks>
    /// The order is measured rather than assumed: adoption removes the node from its tree first, and
    /// that removal is an ordinary child-list mutation, so the disconnected reaction has already run
    /// by the time the adopted one does.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void AdoptedCallback_Reports_Both_Documents()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("disconnected,adopted:true->false,adopted:false->true", Eval(context, """
            var log = [];
            class A extends HTMLElement {
                disconnectedCallback() { log.push('disconnected'); }
                adoptedCallback(oldDoc, newDoc) {
                    log.push('adopted:' + (oldDoc === document) + '->' + (newDoc === document));
                }
            }
            customElements.define('ad-el', A);
            var a = document.createElement('ad-el');
            document.body.appendChild(a);
            var other = document.implementation.createHTMLDocument('x');
            other.adoptNode(a);
            document.adoptNode(a);
            return log.join(',');
            """));
    }

    /// <summary>
    /// The whole adopted subtree is reported, not only the node named on the record: adoption moves
    /// every descendant's node document too.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Adoption_Reaches_Every_Custom_Element_In_The_Subtree()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("adopted:outer,adopted:inner", Eval(context, """
            var log = [];
            class A extends HTMLElement { adoptedCallback() { log.push('adopted:' + this.id); } }
            customElements.define('ad-el', A);
            var wrapper = document.createElement('div');
            wrapper.innerHTML = '<ad-el id="outer"><span><ad-el id="inner"></ad-el></span></ad-el>';
            document.body.appendChild(wrapper);
            document.implementation.createHTMLDocument('x').adoptNode(wrapper);
            return log.join(',');
            """));
    }

    /// <summary>
    /// Inserting a node from another document adopts it implicitly (DOM §4.2.3), so the reaction
    /// fires for the ordinary <c>appendChild</c> shape too — followed by the connected one, because
    /// the node really did reach a tree.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Cross_Document_Insertion_Adopts_Implicitly()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("connected,disconnected,adopted,connected,true", Eval(context, """
            var log = [];
            class A extends HTMLElement {
                connectedCallback() { log.push('connected'); }
                disconnectedCallback() { log.push('disconnected'); }
                adoptedCallback() { log.push('adopted'); }
            }
            customElements.define('ad-el', A);
            var a = document.createElement('ad-el');
            document.body.appendChild(a);
            var other = document.implementation.createHTMLDocument('x');
            other.body.appendChild(a);
            return log.join(',') + ',' + (a.ownerDocument === other);
            """));
    }

    /// <summary>A document may not be adopted (DOM §4.5), and adopting a node already owned here is
    /// a detach rather than an error.</summary>
    [Fact(Timeout = 600000)]
    public void Adopting_A_Document_Is_A_NotSupportedError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("NotSupportedError/true/null", Eval(context, """
            var thrown;
            try { document.adoptNode(document.implementation.createHTMLDocument('x')); thrown = 'ok'; }
            catch (e) { thrown = e.name; }
            var own = document.getElementById('host');
            var same = document.adoptNode(own) === own;
            return thrown + '/' + same + '/' + String(own.parentNode);
            """));
    }
}
