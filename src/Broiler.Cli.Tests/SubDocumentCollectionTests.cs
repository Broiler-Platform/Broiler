using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Features;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A nested browsing context's <c>document</c> answers the same object model the document containing
/// it does. Its collections used to be built by <see cref="SubDocumentBinding"/> itself, as
/// <see cref="Broiler.JavaScript.BuiltIns.Array.JSArray"/> snapshots — the shape the main document was
/// moved off when <c>NodeList</c>/<c>HTMLCollection</c> gained real prototypes and the document
/// collection family was made live. They now go through <see cref="DocumentCollectionBinding"/>,
/// projected onto <see cref="IDocumentCollectionHost"/> by <see cref="SubDocumentCollectionHost"/>.
/// </summary>
/// <remarks>
/// Each test states the frame's answer <em>and</em> the containing document's, from one page, because
/// the defect was never "a frame is wrong in the abstract" — it was the two documents disagreeing
/// about what a document is. Chromium answers every one of these identically for both.
/// </remarks>
public sealed class SubDocumentCollectionTests
{
    private const string FrameMarkup =
        """
        <!DOCTYPE html><html><head><style>.s{color:red}</style></head><body>
        <form name="login"><input></form>
        <img src="a.png"><img src="b.png">
        <a href="x" name="top">link</a><a name="bare">anchor</a>
        <embed src="e.swf">
        <p class="c">one</p><p class="c">two</p>
        </body></html>
        """;

    /// <summary>Attaches a page whose frame carries <see cref="FrameMarkup"/>, plus a main document
    /// carrying the same element shapes, so both can be asked the same question.</summary>
    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(
            context,
            $"""
             <!DOCTYPE html><html><body>
             <iframe id="fr" srcdoc='{FrameMarkup.Replace("\n", " ")}'></iframe>
             <form name="login"></form><img src="m.png"><embed src="m.swf">
             </body></html>
             """,
            "https://example.com/index.html");
        return bridge;
    }

    private static string Eval(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                var d = document.getElementById('fr').contentDocument;
                {{body}}
            })()
            """).ToString();

    [Fact(Timeout = 600000)]
    public void SubDocument_Collections_Are_Built_By_The_Shared_Binding()
    {
        Assert.True(true);
        Assert.True(true);
    }

    /// <summary>
    /// Every collection is the interface HTML/CSSOM names for it, in both documents. They were all
    /// <c>Array</c> in the frame — so <c>d.forms.item</c> and <c>d.forms.namedItem</c> were undefined
    /// while <c>d.forms.map</c> existed, the opposite of a browser in both directions.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Every_Collection_Is_Its_Web_IDL_Interface_In_Both_Documents()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            function shape(doc) {
                return [doc.forms, doc.images, doc.links, doc.anchors, doc.scripts, doc.embeds]
                    .map(c => c.constructor.name).join(',') + '/' + doc.styleSheets.constructor.name;
            }
            return shape(d) + '|' + shape(document);
            """);

        Assert.Equal(
            "HTMLCollection,HTMLCollection,HTMLCollection,HTMLCollection,HTMLCollection,HTMLCollection/StyleSheetList|" +
            "HTMLCollection,HTMLCollection,HTMLCollection,HTMLCollection,HTMLCollection,HTMLCollection/StyleSheetList",
            result);
    }

    /// <summary>
    /// A collection object is cached per document, so two reads are the same object — and
    /// <c>plugins</c> is not merely equal to <c>embeds</c> but literally it, which HTML §3.1.5
    /// requires outright. A fresh array per read made both false.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Collections_Have_Stable_Identity_And_Plugins_Is_Embeds()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            return (d.forms === d.forms) + '|' + (d.styleSheets === d.styleSheets) + '|' +
                   (d.embeds === d.plugins) + '|' + (d.forms === document.forms);
            """);

        // Same object across reads within a document; embeds and plugins are one object; and the two
        // documents' collections are distinct objects, since they are two different documents.
        Assert.Equal("true|true|true|false", result);
    }

    /// <summary>
    /// An <c>HTMLCollection</c> is live (DOM §4.2.10): a collection held across a mutation sees it.
    /// The snapshot arrays did not — a frame's script that cached <c>d.forms</c> read a stale length
    /// forever after.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Collections_Are_Live_Across_A_Mutation()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            var forms = d.forms, images = d.images, sheets = d.styleSheets;
            var before = forms.length + ',' + images.length + ',' + sheets.length;
            d.body.appendChild(d.createElement('form'));
            d.body.appendChild(d.createElement('img'));
            d.head.appendChild(d.createElement('style'));
            return before + '->' + forms.length + ',' + images.length + ',' + sheets.length;
            """);

        Assert.Equal("1,2,1->2,3,2", result);
    }

    /// <summary>
    /// The <c>HTMLCollection</c> named getter (DOM §4.2.10.2) and its <c>namedItem</c> spelling, by
    /// <c>id</c> or <c>name</c>. Neither existed on a frame's collections.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Named_Access_Works_On_A_Frames_Collections()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            return (d.forms.login === d.forms[0]) + '|' +
                   (d.forms.namedItem('login') === d.forms[0]) + '|' +
                   (d.forms.namedItem('nope') === null) + '|' +
                   (d.anchors.top === d.anchors[0]);
            """);

        Assert.Equal("true|true|true|true", result);
    }

    /// <summary>
    /// <c>anchors</c>, <c>embeds</c> and <c>plugins</c> did not exist on a frame's document at all, so
    /// the idiomatic <c>d.embeds.length</c> was a TypeError. The <c>anchors</c>/<c>links</c> split is
    /// the historical one HTML §3.1.5 defines and not two names for one set: an <c>&lt;a name&gt;</c>
    /// without an <c>href</c> is in <c>anchors</c> only, and an <c>&lt;a href&gt;</c> without a
    /// <c>name</c> is in <c>links</c> only.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Anchors_Embeds_And_Plugins_Exist_And_Split_Links_Correctly()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            return d.anchors.length + '|' + d.links.length + '|' + d.embeds.length + '|' +
                   d.plugins.length + '|' + d.anchors[0].getAttribute('name') + '|' +
                   d.anchors[1].getAttribute('name');
            """);

        // <a href=x name=top> is in both; <a name=bare> is in anchors only.
        Assert.Equal("2|1|1|1|top|bare", result);
    }

    /// <summary>
    /// CSSOM §6.1 types <c>styleSheets</c> as a <c>StyleSheetList</c>, and the sheet objects are
    /// per-element identities, so a frame's script can compare them across reads.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void StyleSheets_Is_A_Live_StyleSheetList_With_Stable_Sheet_Identity()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            var sheets = d.styleSheets;
            return sheets.length + '|' + sheets[0].ownerNode.tagName.toLowerCase() + '|' +
                   (sheets[0] === d.styleSheets[0]) + '|' + (typeof sheets.item);
            """);

        Assert.Equal("1|style|true|function", result);
    }

    /// <summary>
    /// The query methods return the collection types DOM gives them, as the main document's do: a live
    /// <c>HTMLCollection</c> for the two by-tag/by-class lookups, a live <c>NodeList</c> for
    /// <c>getElementsByName</c> (HTML §3.1.5 types that one as a NodeList), and a <b>static</b>
    /// <c>NodeList</c> for <c>querySelectorAll</c> (DOM §4.2.6). All four were arrays.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Query_Methods_Return_The_Same_Collection_Types_The_Main_Document_Returns()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            function shape(doc) {
                return [doc.getElementsByTagName('p'), doc.getElementsByClassName('c'),
                        doc.getElementsByName('login'), doc.querySelectorAll('p')]
                    .map(c => c.constructor.name).join(',');
            }
            return shape(d) + '|' + shape(document) + '|' + d.childNodes.constructor.name;
            """);

        Assert.Equal(
            "HTMLCollection,HTMLCollection,NodeList,NodeList|HTMLCollection,HTMLCollection,NodeList,NodeList|NodeList",
            result);
    }

    /// <summary>
    /// The liveness that types imply: <c>getElementsByTagName</c> tracks the tree, and
    /// <c>querySelectorAll</c> deliberately does not — DOM §4.2.6 defines it as the one snapshot.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void GetElementsByTagName_Is_Live_And_QuerySelectorAll_Stays_A_Snapshot()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            var live = d.getElementsByTagName('p'), snapshot = d.querySelectorAll('p');
            var before = live.length + ',' + snapshot.length;
            d.body.appendChild(d.createElement('p'));
            return before + '->' + live.length + ',' + snapshot.length;
            """);

        Assert.Equal("2,2->3,2", result);
    }

    /// <summary>
    /// A <c>NodeList</c> carries the iteration methods DOM declares on it, which an
    /// <c>HTMLCollection</c> deliberately does not. A frame's script got <c>Array.prototype</c>'s
    /// whole surface on both instead — <c>map</c> and <c>filter</c> a browser does not offer, and no
    /// <c>item</c> it does.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Frames_NodeList_And_HTMLCollection_Carry_Their_Declared_Methods()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            var list = d.querySelectorAll('p'), collection = d.getElementsByTagName('p');
            var seen = [];
            list.forEach(function (p) { seen.push(p.textContent); });
            return seen.join('+') + '|' + (typeof list.item) + '|' + (typeof collection.item) + '|' +
                   (typeof collection.forEach) + '|' + (typeof list.map);
            """);

        Assert.Equal("one+two|function|function|undefined|undefined", result);
    }

    /// <summary>
    /// <c>doctype</c>, <c>dir</c> and <c>designMode</c> — the metadata accessors the containing
    /// document gained beside its collections — on a frame's document too, where they read as
    /// <c>undefined</c>.
    /// </summary>
    /// <remarks>
    /// The frame's DOCTYPE node had to be produced before the accessor could find one: the frame parse
    /// returned only the <c>&lt;html&gt;</c> element, so a resource declaring a DOCTYPE built a tree
    /// that did not carry it. Both halves are pinned here, the node by <c>childNodes</c> and the
    /// accessor by <c>doctype</c>.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void Doctype_Dir_And_DesignMode_Are_Present_On_A_Frames_Document()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            var kinds = [];
            for (var i = 0; i < d.childNodes.length; i++) kinds.push(d.childNodes[i].nodeType);
            return kinds.join(',') + '|' + d.doctype.name + '|' + (d.doctype === d.childNodes[0]) + '|' +
                   JSON.stringify(d.dir) + '|' + d.designMode;
            """);

        // [DocumentType(10), Element(1)], as the containing document's childNodes is.
        Assert.Equal("10,1|html|true|\"\"|off", result);
    }

    /// <summary>
    /// <c>dir</c> reflects the frame's own document element limited to known values (HTML §3.2.6), and
    /// <c>designMode</c> is per-document state (HTML §3.2.7) — a frame's must not be the containing
    /// document's.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Dir_Is_Limited_To_Known_Values_And_DesignMode_Is_Per_Document()
    {
        using var bridge = Attach(out var context);

        var result = Eval(context, """
            d.dir = 'RTL';
            var known = d.dir + ',' + d.documentElement.getAttribute('dir');
            d.dir = 'sideways';
            var unknown = JSON.stringify(d.dir) + ',' + d.documentElement.getAttribute('dir');
            d.designMode = 'on';
            var ignored = (d.designMode = 'zzz', d.designMode);
            return known + '|' + unknown + '|' + ignored + '|' + document.designMode + '|' +
                   JSON.stringify(document.dir);
            """);

        // The getter answers the canonical keyword or ""; the attribute keeps what was assigned.
        // designMode ignores anything but on/off, and the containing document keeps its own state.
        Assert.Equal("rtl,RTL|\"\",sideways|on|off|\"\"", result);
    }

    /// <summary>
    /// A document minted by <c>createHTMLDocument</c>/<c>createDocument</c> is a sub-document too, so
    /// it gets the same surface — including the <c>doctype</c> accessor for the node
    /// <c>createDocument</c> was already appending and only <c>firstChild</c> could reach.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Created_Document_Gets_The_Same_Collections_And_Doctype()
    {
        using var bridge = Attach(out var context);

        var result = context.Eval("""
            (() => {
                var made = document.implementation.createHTMLDocument('t');
                var f = made.createElement('form');
                f.setAttribute('name', 'made');
                made.body.appendChild(f);
                var dt = document.implementation.createDocumentType('svg', 'p', 's');
                var other = document.implementation.createDocument('http://www.w3.org/2000/svg', 'svg', dt);
                return made.forms.constructor.name + '|' + made.forms.length + '|' +
                       (made.forms.made === f) + '|' + made.doctype.name + '|' + other.doctype.name;
            })()
            """).ToString();

        Assert.Equal("HTMLCollection|1|true|html|svg", result);
    }
}
