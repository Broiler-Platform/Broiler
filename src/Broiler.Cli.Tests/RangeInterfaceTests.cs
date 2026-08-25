using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>AbstractRange</c> and <c>Range</c> (DOM §4.5) as interfaces, the five operations that were
/// missing from them, and the exceptions their arguments owe the caller.
/// </summary>
/// <remarks>
/// <para>
/// Three separate gaps met here. <c>Range</c> was not a global at all, so <c>new Range()</c> and
/// <c>r instanceof Range</c> were <c>ReferenceError</c>s and a range's <c>constructor.name</c> was
/// <c>"Object"</c>. <c>comparePoint</c>, <c>isPointInRange</c>, <c>intersectsNode</c>,
/// <c>createContextualFragment</c> and <c>detach</c> did not exist. And every argument check was
/// lenient: an out-of-range offset was clamped into the node, a non-<c>Node</c> argument returned
/// <c>undefined</c>, <c>selectNode</c> on a parentless node was a no-op, and <c>insertNode</c> would
/// put a doctype inside a paragraph.
/// </para>
/// <para>
/// Every expectation below is Chromium's measured answer over the same probe corpus run against both
/// engines, not a reading of the specification. That is what caught the cases where the two part
/// company: a negative offset is an <c>IndexSizeError</c> rather than a <c>TypeError</c>, because Web
/// IDL turns <c>-1</c> into <c>4294967295</c> first; <c>compareBoundaryPoints(3.7, r)</c> is accepted
/// for the same reason while <c>4</c> is a <c>NotSupportedError</c>; and the boundary getters are
/// <c>AbstractRange</c>'s, so a browser's <c>Range.prototype</c> genuinely does not own them.
/// </para>
/// </remarks>
public sealed class RangeInterfaceTests
{
    private const string Markup =
        "<!DOCTYPE html><html><body><div id=host><p id=p>Hello <b id=b>brave</b> world</p>" +
        "<span id=s>tail</span></div></body></html>";

    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, Markup, "https://example.com/index.html");
        return bridge;
    }

    /// <summary>Evaluates <paramref name="body"/> with the fixture's nodes in scope, reporting either
    /// the value or the thrown error's name — so a failure prints what the engine actually did.</summary>
    private static string Outcome(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                var p = document.getElementById('p');
                var b = document.getElementById('b');
                var s = document.getElementById('s');
                var host = document.getElementById('host');
                var t = p.firstChild;
                var r = document.createRange();
                try { return String({{body}}); }
                catch (e) { return 'THREW ' + (e.name || '?'); }
            })()
            """).ToString();

    // ---------------- The interface ----------------

    [Fact(Timeout = 600000)]
    public void Range_Is_A_Constructible_Interface()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function", Outcome(context, "typeof Range"));
        Assert.Equal("Range", Outcome(context, "document.createRange().constructor.name"));
        Assert.Equal("true", Outcome(context, "document.createRange() instanceof Range"));
        Assert.Equal("[object Range]", Outcome(context, "Object.prototype.toString.call(r)"));
        // `new Range()` starts at (document, 0) and collapsed, which is what makes it usable without
        // a document.createRange() in hand.
        Assert.Equal(
            "#document,0,true",
            Outcome(context, "[new Range().startContainer.nodeName, new Range().startOffset, new Range().collapsed].join(',')"));
        // Calling the interface object without `new` is a TypeError in a browser, not a silent range.
        Assert.Equal("THREW TypeError", Outcome(context, "Range()"));
    }

    /// <summary>
    /// The members are on the prototypes, so the instance has none of its own — 29 own properties
    /// before this. That is what lets <c>Range.prototype.setStart</c> exist at all, and what makes
    /// extending the prototype reach existing ranges.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Range_Members_Live_On_The_Prototype()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("", Outcome(context, "Object.getOwnPropertyNames(r).join(',')"));
        Assert.Equal("function", Outcome(context, "typeof Range.prototype.setStart"));
        Assert.Equal("true", Outcome(context, "Object.getPrototypeOf(r) === Range.prototype"));
        // The ordinary polyfill idiom, which needs a prototype something inherits from.
        Assert.Equal(
            "reached",
            Outcome(context, "(Range.prototype.__probe = function () { return 'reached'; }, r.__probe())"));
    }

    /// <summary>
    /// The boundary attributes belong to <c>AbstractRange</c>, the base <c>Range</c> and
    /// <c>StaticRange</c> share. Putting them on <c>Range.prototype</c> instead would be a shape no
    /// browser has.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Boundary_Attributes_Belong_To_AbstractRange()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function", Outcome(context, "typeof AbstractRange"));
        Assert.Equal("true", Outcome(context, "Object.getPrototypeOf(Range.prototype) === AbstractRange.prototype"));
        Assert.Equal("true", Outcome(context, "r instanceof AbstractRange"));
        Assert.Equal(
            "collapsed,constructor,endContainer,endOffset,startContainer,startOffset",
            Outcome(context, "Object.getOwnPropertyNames(AbstractRange.prototype).sort().join(',')"));
        Assert.Equal("undefined", Outcome(context, "typeof Object.getOwnPropertyDescriptor(Range.prototype, 'startContainer')"));
        Assert.Equal("THREW TypeError", Outcome(context, "new AbstractRange()"));
    }

    /// <summary>
    /// Web IDL constants: on the interface object <em>and</em> its prototype, non-writable. They used
    /// to be four ordinary writable properties of every range object and nothing at all on a
    /// <c>Range</c> that did not exist.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Comparison_Constants_Are_Web_IDL_Constants()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("0,1,2,3", Outcome(context,
            "[Range.START_TO_START, Range.START_TO_END, Range.END_TO_END, Range.END_TO_START].join(',')"));
        Assert.Equal("0,1,2,3", Outcome(context,
            "[r.START_TO_START, r.START_TO_END, r.END_TO_END, r.END_TO_START].join(',')"));
        Assert.Equal("false", Outcome(context, "Object.hasOwnProperty.call(r, 'START_TO_START')"));
        Assert.Equal("0,false,true,false", Outcome(context,
            "(d => [d.value, d.writable, d.enumerable, d.configurable].join(','))" +
            "(Object.getOwnPropertyDescriptor(Range, 'START_TO_START'))"));
    }

    /// <summary>A method's <c>length</c> is its required-argument count, so an optional argument does
    /// not count — <c>collapse(toStart)</c> is <c>0</c>, not <c>1</c>.</summary>
    [Fact(Timeout = 600000)]
    public void Operation_Arities_Match_Web_IDL()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("2,2,2,1,1,0,0", Outcome(context,
            "[Range.prototype.setStart.length, Range.prototype.comparePoint.length," +
            " Range.prototype.isPointInRange.length, Range.prototype.intersectsNode.length," +
            " Range.prototype.createContextualFragment.length, Range.prototype.collapse.length," +
            " Range.prototype.toString.length].join(',')"));
    }

    /// <summary>A member held on the prototype can be called on anything; a receiver that is not a
    /// range is an illegal invocation rather than a crash or a wrong answer.</summary>
    [Fact(Timeout = 600000)]
    public void Calling_An_Operation_On_A_Foreign_Receiver_Is_A_TypeError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW TypeError", Outcome(context, "Range.prototype.setStart.call({}, t, 0)"));
        Assert.Equal("THREW TypeError", Outcome(context, "Object.getOwnPropertyDescriptor(AbstractRange.prototype, 'startOffset').get.call({})"));
    }

    // ---------------- The five operations that were missing ----------------

    [Fact(Timeout = 600000)]
    public void ComparePoint_Reports_Before_Within_And_After()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("-1", Outcome(context, "(r.selectNodeContents(b), r.comparePoint(t, 0))"));
        Assert.Equal("0", Outcome(context, "(r.selectNodeContents(p), r.comparePoint(t, 2))"));
        Assert.Equal("1", Outcome(context, "(r.selectNodeContents(b), r.comparePoint(s.firstChild, 1))"));
        // Both boundaries are "within": the range's own endpoints compare equal, not outside.
        Assert.Equal("0,0", Outcome(context,
            "(r.setStart(t, 2), r.setEnd(t, 4), [r.comparePoint(t, 2), r.comparePoint(t, 4)].join(','))"));
    }

    /// <summary>
    /// The three predicates disagree about a node in another tree, and deliberately so:
    /// <c>comparePoint</c> asks "where is it?", which has no answer across trees, while the other two
    /// ask a yes/no question that does.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Point_In_Another_Tree_Throws_Only_For_ComparePoint()
    {
        using var bridge = Attach(out var context);

        const string other = "document.implementation.createHTMLDocument('x')";
        Assert.Equal("THREW WrongDocumentError", Outcome(context, $"(r.selectNodeContents(p), r.comparePoint({other}.body, 0))"));
        Assert.Equal("false", Outcome(context, $"(r.selectNodeContents(p), r.isPointInRange({other}.body, 0))"));
        Assert.Equal("false", Outcome(context, $"(r.selectNodeContents(p), r.intersectsNode({other}.body))"));
    }

    [Fact(Timeout = 600000)]
    public void IsPointInRange_And_IntersectsNode_Answer_From_The_Boundaries()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true", Outcome(context, "(r.selectNodeContents(p), r.isPointInRange(t, 2))"));
        Assert.Equal("false", Outcome(context, "(r.selectNodeContents(b), r.isPointInRange(t, 2))"));
        Assert.Equal("true", Outcome(context, "(r.selectNodeContents(p), r.intersectsNode(b))"));
        Assert.Equal("false", Outcome(context, "(r.selectNodeContents(b), r.intersectsNode(s))"));
        // A node with no parent that still shares the range's root is the root itself, and the range
        // is inside it. A *detached* node fails the root test first, which is why the two differ.
        Assert.Equal("true", Outcome(context, "(r.selectNodeContents(p), r.intersectsNode(document))"));
        Assert.Equal("false", Outcome(context, "(r.selectNodeContents(p), r.intersectsNode(document.createElement('div')))"));
    }

    /// <summary>Both point predicates reject a doctype point and an over-long offset, the same two
    /// checks the boundary setters make.</summary>
    [Fact(Timeout = 600000)]
    public void The_Point_Predicates_Validate_Their_Point()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW InvalidNodeTypeError", Outcome(context, "(r.selectNodeContents(p), r.comparePoint(document.doctype, 0))"));
        Assert.Equal("THREW IndexSizeError", Outcome(context, "(r.selectNodeContents(p), r.comparePoint(t, 999))"));
        Assert.Equal("THREW InvalidNodeTypeError", Outcome(context, "(r.selectNodeContents(p), r.isPointInRange(document.doctype, 0))"));
        Assert.Equal("THREW IndexSizeError", Outcome(context, "(r.selectNodeContents(p), r.isPointInRange(t, 999))"));
    }

    /// <summary><c>createContextualFragment</c> parses in the start node's element context and returns
    /// a real <c>DocumentFragment</c>.</summary>
    [Fact(Timeout = 600000)]
    public void CreateContextualFragment_Parses_Into_A_Fragment()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("2,I,DocumentFragment", Outcome(context,
            "(r.selectNodeContents(p), (f => [f.childNodes.length, f.firstChild.tagName, f.constructor.name].join(','))" +
            "(r.createContextualFragment('<i>x</i>y')))"));
        Assert.Equal("0", Outcome(context, "(r.selectNodeContents(p), r.createContextualFragment('').childNodes.length)"));
        // A range built by `new Range()` starts at the document, which is not an element context, so
        // the parse falls back to the document's body rather than failing.
        Assert.Equal("I", Outcome(context, "new Range().createContextualFragment('<i>x</i>').firstChild.tagName"));
    }

    /// <summary><c>detach()</c> is specified to do nothing; the range stays usable, which is the
    /// observable half.</summary>
    [Fact(Timeout = 600000)]
    public void Detach_Is_A_No_Op_That_Leaves_The_Range_Usable()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("undefined", Outcome(context, "r.detach()"));
        Assert.Equal("Hello brave world", Outcome(context, "(r.detach(), r.selectNodeContents(p), r.toString())"));
    }

    // ---------------- Exception semantics ----------------

    /// <summary>
    /// An offset past the container's length was clamped into it, so a range silently pointed
    /// somewhere else and the wrongness surfaced later as a wrong extraction. Note the two distinct
    /// messages a browser gives — character data reports the length, an element reports "no child at
    /// offset" — and that a negative offset arrives here as a very large one.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("r.setStart(t, 999)")]
    [InlineData("r.setStart(t, -1)")]
    [InlineData("r.setEnd(t, 999)")]
    [InlineData("r.setStart(p, 4)")]
    public void An_Out_Of_Range_Offset_Is_An_IndexSizeError(string call)
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW IndexSizeError", Outcome(context, call));
    }

    /// <summary>The offsets that must keep working, including Web IDL's own coercions — an offset
    /// equal to the length is valid, and <c>NaN</c>/<c>undefined</c> become <c>0</c>.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("(r.setStart(t, 6), r.startOffset)", "6")]
    [InlineData("(r.setStart(p, 3), r.startOffset)", "3")]
    [InlineData("(r.setStart(t, '2'), r.startOffset)", "2")]
    [InlineData("(r.setStart(t, NaN), r.startOffset)", "0")]
    [InlineData("(r.setStart(t, undefined), r.startOffset)", "0")]
    public void A_Valid_Offset_Is_Accepted(string call, string expected)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(expected, Outcome(context, call));
    }

    /// <summary>A missing argument and one that is not a <c>Node</c> are the same failure, and both
    /// used to return <c>undefined</c> and leave the range untouched.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("r.setStart()")]
    [InlineData("r.setStart({}, 0)")]
    [InlineData("r.setStart(null, 0)")]
    [InlineData("r.selectNodeContents(null)")]
    [InlineData("r.insertNode(null)")]
    [InlineData("r.insertNode({})")]
    [InlineData("r.compareBoundaryPoints(0, {})")]
    public void A_Non_Node_Argument_Is_A_TypeError(string call)
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW TypeError", Outcome(context, call));
    }

    /// <summary>A doctype is never a boundary container, and it has no contents to select.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("r.setStart(document.doctype, 0)")]
    [InlineData("r.setEnd(document.doctype, 0)")]
    [InlineData("r.selectNodeContents(document.doctype)")]
    // A node with no parent cannot be positioned relative to its siblings.
    [InlineData("r.selectNode(document.createElement('div'))")]
    [InlineData("r.selectNode(document)")]
    [InlineData("r.setStartBefore(document.createElement('div'))")]
    [InlineData("r.setEndAfter(document.createElement('div'))")]
    public void A_Node_That_Cannot_Be_A_Boundary_Is_An_InvalidNodeTypeError(string call)
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW InvalidNodeTypeError", Outcome(context, call));
    }

    /// <summary>
    /// <c>compareBoundaryPoints</c> answered <c>0</c> for an unknown comparison method and for a
    /// source range in a different tree — the same value a legitimate "equal" comparison gives, so a
    /// caller could not tell the two apart.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void CompareBoundaryPoints_Rejects_A_Bad_Method_And_A_Foreign_Range()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW NotSupportedError", Outcome(context, "r.compareBoundaryPoints(9, document.createRange())"));
        Assert.Equal("THREW NotSupportedError", Outcome(context, "r.compareBoundaryPoints(-1, document.createRange())"));
        Assert.Equal("THREW WrongDocumentError", Outcome(context,
            "(r.selectNodeContents(p), (d => { var o = d.createRange(); o.selectNodeContents(d.body); return r.compareBoundaryPoints(0, o); })" +
            "(document.implementation.createHTMLDocument('x')))"));
        // Web IDL truncates rather than rejecting, so 3.7 is END_TO_START and is accepted.
        Assert.Equal("0", Outcome(context, "r.compareBoundaryPoints(3.7, document.createRange())"));
    }

    /// <summary>
    /// The content operations reject what they cannot insert: a doctype inside a paragraph used to be
    /// accepted silently, and <c>surroundContents</c> splits its rejection two ways.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Content_Operations_Reject_An_Impossible_Node()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW HierarchyRequestError", Outcome(context, "(r.selectNodeContents(p), r.insertNode(document.doctype))"));
        Assert.Equal("THREW InvalidNodeTypeError", Outcome(context, "(r.selectNodeContents(b), r.surroundContents(document.doctype))"));
        Assert.Equal("THREW HierarchyRequestError", Outcome(context, "(r.selectNodeContents(b), r.surroundContents(document.createTextNode('q')))"));
        // The pre-existing partial-selection rule still holds.
        Assert.Equal("THREW InvalidStateError", Outcome(context,
            "(r.setStart(t, 1), r.setEnd(b.firstChild, 2), r.surroundContents(document.createElement('u')))"));
    }

    /// <summary>
    /// A clone keeps the original's root as well as its boundaries. The former implementation minted
    /// the clone against the main document whatever the original's root was, so a range created in a
    /// frame's document cloned into the containing page's.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void CloneRange_Copies_The_Boundaries_And_Is_Independent()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true", Outcome(context, "r.cloneRange() instanceof Range"));
        Assert.Equal("0,1", Outcome(context,
            "(r.selectNodeContents(p), (c => { c.setStart(t, 1); return [r.startOffset, c.startOffset].join(','); })(r.cloneRange()))"));
    }
}
