using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>Selection</c> and the <c>window.getSelection()</c> / <c>document.getSelection()</c> pair, plus
/// <c>StaticRange</c> — the second <c>AbstractRange</c> subclass.
/// </summary>
/// <remarks>
/// <para>
/// None of the three existed. <c>window.getSelection</c> was <c>undefined</c> and both <c>Selection</c>
/// and <c>StaticRange</c> were <c>ReferenceError</c>s, which abort the script rather than the
/// statement. That matters for this API beyond feature detection: the copy-to-clipboard idiom every
/// page shares is <c>sel.removeAllRanges(); sel.addRange(range)</c>, so the name is reached by
/// ordinary pages, not only by editors.
/// </para>
/// <para>
/// Broiler has no user input and therefore no <em>user</em> selection — which is exactly the state a
/// browser is in on a freshly loaded page, and every expectation here is Chromium's measured answer
/// from that state. Two are not what the specification's wording suggests: a node or range in another
/// tree is silently <b>ignored</b> rather than rejected (and "another tree" includes a detached one),
/// while an out-of-range offset or a doctype in that same argument still throws — so the argument is
/// validated before the tree is consulted, not after.
/// </para>
/// </remarks>
public sealed class SelectionInterfaceTests
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

    /// <summary>Evaluates <paramref name="body"/> with the fixture's nodes and an emptied selection in
    /// scope, reporting either the value or the thrown error's name.</summary>
    private static string Outcome(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                var p = document.getElementById('p');
                var b = document.getElementById('b');
                var s = document.getElementById('s');
                var t = p.firstChild;
                var x = window.getSelection();
                if (x) x.removeAllRanges();
                try { return String({{body}}); }
                catch (e) { return 'THREW ' + (e.name || '?'); }
            })()
            """).ToString();

    // ---------------- The interface ----------------

    [Fact(Timeout = 600000)]
    public void GetSelection_Answers_One_Selection_Per_Document()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function", Outcome(context, "typeof window.getSelection"));
        Assert.Equal("function", Outcome(context, "typeof document.getSelection"));
        // One object: a page that stashes the selection and comes back to it must find the same one.
        Assert.Equal("true", Outcome(context, "window.getSelection() === window.getSelection()"));
        Assert.Equal("true", Outcome(context, "window.getSelection() === document.getSelection()"));
        Assert.Equal("Selection", Outcome(context, "x.constructor.name"));
        Assert.Equal("[object Selection]", Outcome(context, "Object.prototype.toString.call(x)"));
        Assert.Equal("true", Outcome(context, "x instanceof Selection"));
        Assert.Equal("THREW TypeError", Outcome(context, "new Selection()"));
        // Members on the prototype, nothing on the instance.
        Assert.Equal("", Outcome(context, "Object.getOwnPropertyNames(x).join(',')"));
        Assert.Equal("THREW TypeError", Outcome(context, "Selection.prototype.addRange.call({}, document.createRange())"));
    }

    /// <summary>
    /// The state a browser reports before anything is selected, which is the state Broiler is always
    /// in until a script says otherwise.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Empty_Selection_Reports_Nothing_Selected()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("0", Outcome(context, "x.rangeCount"));
        Assert.Equal("null", Outcome(context, "String(x.anchorNode)"));
        Assert.Equal("null", Outcome(context, "String(x.focusNode)"));
        Assert.Equal("0,0", Outcome(context, "[x.anchorOffset, x.focusOffset].join(',')"));
        Assert.Equal("true", Outcome(context, "x.isCollapsed"));
        Assert.Equal("None,none", Outcome(context, "[x.type, x.direction].join(',')"));
        Assert.Equal("", Outcome(context, "x.toString()"));
        Assert.Equal("THREW IndexSizeError", Outcome(context, "x.getRangeAt(0)"));
    }

    /// <summary>
    /// The selection holds the page's <em>own</em> range object, which is what makes
    /// <c>getRangeAt(0) === r</c> true and what makes the selection follow a later edit of that range.
    /// A second range is dropped rather than added.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void AddRange_Holds_The_Range_Itself()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("1", Outcome(context, "(x.addRange(R(p)), x.rangeCount)".Replace("R(p)", Range("p"))));
        Assert.Equal("true", Outcome(context, $"(x.addRange(r = {Range("p")}), x.getRangeAt(0) === r)"));
        Assert.Equal("P,0,P,3", Outcome(context,
            $"(x.addRange({Range("p")}), [x.anchorNode.nodeName, x.anchorOffset, x.focusNode.nodeName, x.focusOffset].join(','))"));
        Assert.Equal("Hello brave world", Outcome(context, $"(x.addRange({Range("p")}), x.toString())"));
        Assert.Equal("Range,false,forward", Outcome(context,
            $"(x.addRange({Range("p")}), [x.type, x.isCollapsed, x.direction].join(','))"));
        // The selection is the range, so editing the range moves the selection.
        Assert.Equal("tail", Outcome(context, $"(x.addRange(r = {Range("b")}), r.selectNodeContents(s), x.toString())"));
        // A collapsed range makes the selection a caret rather than a range.
        Assert.Equal("Caret,true,1", Outcome(context,
            "(r = document.createRange(), r.setStart(t, 2), r.setEnd(t, 2), x.addRange(r), [x.type, x.isCollapsed, x.rangeCount].join(','))"));
        // A second addRange is ignored — a selection carries one range.
        Assert.Equal("1,brave", Outcome(context,
            $"(x.addRange({Range("b")}), x.addRange({Range("s")}), [x.rangeCount, x.toString()].join(','))"));
    }

    [Fact(Timeout = 600000)]
    public void Removing_Ranges_Empties_The_Selection()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("0,null", Outcome(context,
            $"(x.addRange({Range("p")}), x.removeAllRanges(), [x.rangeCount, String(x.anchorNode)].join(','))"));
        Assert.Equal("0", Outcome(context, $"(x.addRange({Range("p")}), x.empty(), x.rangeCount)"));
        Assert.Equal("0", Outcome(context, $"(x.addRange(r = {Range("p")}), x.removeRange(r), x.rangeCount)"));
        // Another range with the same boundaries is a different range, and removing it removes nothing.
        Assert.Equal("1", Outcome(context,
            $"(x.addRange({Range("p")}), x.removeRange(document.createRange()), x.rangeCount)"));
    }

    // ---------------- Moving the selection ----------------

    [Fact(Timeout = 600000)]
    public void Collapse_Puts_A_Caret_At_A_Point()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("#text,2,1,true,Caret", Outcome(context,
            "(x.collapse(t, 2), [x.anchorNode.nodeName, x.anchorOffset, x.rangeCount, x.isCollapsed, x.type].join(','))"));
        // setPosition is the same operation under its newer name.
        Assert.Equal("3,Caret", Outcome(context, "(x.setPosition(t, 3), [x.anchorOffset, x.type].join(','))"));
        // A null node empties the selection — the one argument here that is not a TypeError.
        Assert.Equal("0,null", Outcome(context,
            $"(x.addRange({Range("p")}), x.collapse(null), [x.rangeCount, String(x.anchorNode)].join(','))"));
        Assert.Equal("P,0,true", Outcome(context,
            $"(x.addRange({Range("p")}), x.collapseToStart(), [x.anchorNode.nodeName, x.anchorOffset, x.isCollapsed].join(','))"));
        Assert.Equal("P,3,true", Outcome(context,
            $"(x.addRange({Range("p")}), x.collapseToEnd(), [x.anchorNode.nodeName, x.anchorOffset, x.isCollapsed].join(','))"));
        Assert.Equal("THREW InvalidStateError", Outcome(context, "x.collapseToStart()"));
    }

    /// <summary>
    /// <c>extend</c> is the one operation a bare <c>Range</c> cannot express: the focus can end up
    /// <em>before</em> the anchor, and the selection remembers that while its range still runs
    /// low-to-high.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Extend_Moves_The_Focus_And_Can_Run_Backwards()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("1,4,ell,forward", Outcome(context,
            "(x.collapse(t, 1), x.extend(t, 4), [x.anchorOffset, x.focusOffset, x.toString(), x.direction].join(','))"));
        Assert.Equal("4,1,ell,backward", Outcome(context,
            "(x.collapse(t, 4), x.extend(t, 1), [x.anchorOffset, x.focusOffset, x.toString(), x.direction].join(','))"));
        Assert.Equal("THREW InvalidStateError", Outcome(context, "x.extend(t, 2)"));

        Assert.Equal("1,4,ell,forward", Outcome(context,
            "(x.setBaseAndExtent(t, 1, t, 4), [x.anchorOffset, x.focusOffset, x.toString(), x.direction].join(','))"));
        Assert.Equal("4,1,ell,backward", Outcome(context,
            "(x.setBaseAndExtent(t, 4, t, 1), [x.anchorOffset, x.focusOffset, x.toString(), x.direction].join(','))"));
        // The legacy aliases a browser still carries.
        Assert.Equal("#text,1,#text,4", Outcome(context,
            "(x.setBaseAndExtent(t, 1, t, 4), [x.baseNode.nodeName, x.baseOffset, x.extentNode.nodeName, x.extentOffset].join(','))"));
    }

    [Fact(Timeout = 600000)]
    public void SelectAllChildren_Selects_A_Nodes_Contents()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("P,0,3,Hello brave world", Outcome(context,
            "(x.selectAllChildren(p), [x.anchorNode.nodeName, x.anchorOffset, x.focusOffset, x.toString()].join(','))"));
        Assert.Equal("THREW InvalidNodeTypeError", Outcome(context, "x.selectAllChildren(document.doctype)"));
    }

    /// <summary>"Contains" means the whole node unless the caller allows a partial one — the default is
    /// the strict reading.</summary>
    [Fact(Timeout = 600000)]
    public void ContainsNode_Distinguishes_Whole_From_Partial()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true,false,false", Outcome(context,
            $"(x.addRange({Range("p")}), [x.containsNode(b, false), x.containsNode(s, false), x.containsNode(s, true)].join(','))"));
        Assert.Equal("true", Outcome(context, $"(x.addRange({Range("p")}), x.containsNode(b))"));
        Assert.Equal("false,true", Outcome(context,
            "(r = document.createRange(), r.setStart(t, 2), r.setEnd(b.firstChild, 2), x.addRange(r)," +
            " [x.containsNode(b, false), x.containsNode(b, true)].join(','))"));
        Assert.Equal("THREW TypeError", Outcome(context, "x.containsNode(null)"));
    }

    [Fact(Timeout = 600000)]
    public void DeleteFromDocument_Removes_The_Selected_Content()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("aef", Outcome(context,
            "(d = document.createElement('div'), d.textContent = 'abcdef', document.body.appendChild(d)," +
            " r = document.createRange(), r.setStart(d.firstChild, 1), r.setEnd(d.firstChild, 4)," +
            " x.addRange(r), x.deleteFromDocument(), d.textContent)"));
        // Nothing selected is nothing to delete, not a failure.
        Assert.Equal("undefined", Outcome(context, "x.deleteFromDocument()"));
    }

    // ---------------- Argument rules ----------------

    /// <summary>
    /// A node outside this selection's tree is <em>ignored</em>, not rejected — and a detached node is
    /// outside it, which is the case reasoning gets wrong: a page that collapses into an element it
    /// has built but not yet inserted gets no selection rather than an error.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("(d = document.createElement('div'), d.textContent = 'xyz', r = document.createRange(), r.selectNodeContents(d), x.addRange(r), x.rangeCount)")]
    [InlineData("(d = document.createElement('div'), d.textContent = 'xyz', x.collapse(d.firstChild, 1), x.rangeCount)")]
    [InlineData("(d = document.createElement('div'), d.textContent = 'xyz', x.selectAllChildren(d), x.rangeCount)")]
    [InlineData("(d = document.implementation.createHTMLDocument('z'), r = d.createRange(), r.selectNodeContents(d.body), x.addRange(r), x.rangeCount)")]
    [InlineData("(d = document.implementation.createHTMLDocument('z'), x.collapse(d.body, 0), x.rangeCount)")]
    [InlineData("(d = document.implementation.createHTMLDocument('z'), x.setBaseAndExtent(t, 1, d.body, 0), x.rangeCount)")]
    public void A_Node_Outside_The_Selections_Tree_Is_Ignored(string body)
    {
        using var bridge = Attach(out var context);

        Assert.Equal("0", Outcome(context, body));
    }

    /// <summary>The argument checks that do throw, and which run before the tree test above.</summary>
    [Theory(Timeout = 600000)]
    [InlineData("x.collapse(t, 999)", "THREW IndexSizeError")]
    [InlineData("(x.collapse(t, 1), x.extend(t, 999))", "THREW IndexSizeError")]
    [InlineData("x.setBaseAndExtent(t, 0, t, 999)", "THREW IndexSizeError")]
    [InlineData("x.collapse(document.doctype, 0)", "THREW InvalidNodeTypeError")]
    [InlineData("x.selectAllChildren(null)", "THREW TypeError")]
    [InlineData("(x.collapse(t, 1), x.extend(null, 0))", "THREW TypeError")]
    [InlineData("x.addRange({})", "THREW TypeError")]
    [InlineData("x.addRange()", "THREW TypeError")]
    public void An_Invalid_Argument_Throws(string body, string expected)
    {
        using var bridge = Attach(out var context);

        Assert.Equal(expected, Outcome(context, body));
    }

    /// <summary>
    /// <c>modify()</c> and <c>getComposedRanges()</c> are deliberately absent rather than stubbed:
    /// the first moves the selection by character/word/line and needs a text-segmentation model this
    /// engine does not have, the second is shadow-tree composition. A page feature-detecting either
    /// takes its fallback, where a stub would claim a movement that silently does nothing. Pinned so
    /// that implementing one is a decision rather than a drift.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Two_Unimplementable_Operations_Are_Absent()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("undefined", Outcome(context, "typeof Selection.prototype.modify"));
        Assert.Equal("undefined", Outcome(context, "typeof Selection.prototype.getComposedRanges"));
    }

    // ---------------- StaticRange ----------------

    /// <summary>
    /// The other <c>AbstractRange</c> subclass: four values captured at construction that never move,
    /// which is the whole difference from a <c>Range</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void StaticRange_Is_A_Frozen_AbstractRange()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function", Outcome(context, "typeof StaticRange"));
        Assert.Equal("1,4,false", Outcome(context,
            "(r = new StaticRange({startContainer: t, startOffset: 1, endContainer: t, endOffset: 4})," +
            " [r.startOffset, r.endOffset, r.collapsed].join(','))"));
        Assert.Equal("true", Outcome(context,
            "new StaticRange({startContainer: t, startOffset: 0, endContainer: t, endOffset: 0}) instanceof AbstractRange"));
        // It carries no members of its own: the boundary getters are AbstractRange's.
        Assert.Equal("constructor", Outcome(context, "Object.getOwnPropertyNames(StaticRange.prototype).sort().join(',')"));
        // Static means static: editing the text underneath leaves the offsets where they were, where a
        // live Range would have been adjusted.
        Assert.Equal("1,4", Outcome(context,
            "(d = document.createElement('div'), d.textContent = 'abcdef'," +
            " r = new StaticRange({startContainer: d.firstChild, startOffset: 1, endContainer: d.firstChild, endOffset: 4})," +
            " d.firstChild.data = 'xy', [r.startOffset, r.endOffset].join(','))"));
    }

    [Fact(Timeout = 600000)]
    public void StaticRange_Rejects_A_Bad_Init()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW InvalidNodeTypeError", Outcome(context,
            "new StaticRange({startContainer: document.doctype, startOffset: 0, endContainer: t, endOffset: 0})"));
        Assert.Equal("THREW TypeError", Outcome(context, "new StaticRange({})"));
        Assert.Equal("THREW TypeError", Outcome(context,
            "new StaticRange({startContainer: t, startOffset: 0, endContainer: t})"));
    }

    /// <summary>A static range has no tree to mutate, so borrowing a <c>Range</c> operation for one is
    /// the same illegal invocation as calling it on any other foreign receiver.</summary>
    [Fact(Timeout = 600000)]
    public void A_Range_Operation_Called_On_A_StaticRange_Is_A_TypeError()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("THREW TypeError", Outcome(context,
            "(r = new StaticRange({startContainer: t, startOffset: 0, endContainer: t, endOffset: 0})," +
            " Range.prototype.setStart.call(r, t, 1))"));
        Assert.Equal("THREW TypeError", Outcome(context,
            "(r = new StaticRange({startContainer: t, startOffset: 0, endContainer: t, endOffset: 0})," +
            " document.createRange().compareBoundaryPoints(0, r))"));
    }

    private static string Range(string id) =>
        $"(function () {{ var q = document.createRange(); q.selectNodeContents(document.getElementById('{id}')); return q; }})()";
}
