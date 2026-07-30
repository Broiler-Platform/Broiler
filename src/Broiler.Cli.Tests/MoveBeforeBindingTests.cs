using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The DOM <c>Node.moveBefore()</c> binding: an atomic reposition of an already-attached node,
/// preserving state a remove-then-insert would destroy.
/// <para>
/// REGRESSION GUARD (WPT issue #1491, problem 27):
/// <c>dom/nodes/moveBefore/preserve-render-blocking-style.html</c> moves a render-blocking
/// <c>&lt;style&gt;</c> and asserts the styles survive. With no <c>moveBefore</c> the script threw
/// on an undefined function, the document was never styled, and the test rendered white against
/// Chromium's 100% green.
/// </para>
/// <para>
/// These cover the main-repo binding. The move itself is the canonical
/// <c>DomNode.MoveBefore</c> (<c>patches/0038</c>, applied by the maintainer and now pinned);
/// what the binding still owns is the JavaScript surface, the <c>DOMException</c> marshalling
/// and the style-scope invalidation, so these stay the guard on that seam.
/// </para>
/// </summary>
public sealed class MoveBeforeBindingTests
{
    private static string Eval(string html, string script)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        return context.Eval(script).ToString();
    }

    private const string ThreeDivs =
        "<!DOCTYPE html><html><body><div id=\"x\"></div><div id=\"y\"></div><div id=\"z\"></div></body></html>";

    [Fact]
    public void MoveBefore_Is_Exposed_On_Element()
    {
        var result = Eval(ThreeDivs, "typeof document.body.moveBefore");
        Assert.Equal("function", result);
    }

    [Fact]
    public void Moves_A_Child_Within_Its_Parent()
    {
        var result = Eval(ThreeDivs, """
            (() => {
                var b = document.body;
                b.moveBefore(document.getElementById('z'), document.getElementById('x'));
                return [...b.children].map(e => e.id).join(',');
            })()
            """);

        Assert.Equal("z,x,y", result);
    }

    // Removing first shifts later indices down, so a forward move needs a correction.
    // Without it the node lands one slot too far right.
    [Fact]
    public void Moves_A_Child_Forward_Within_Its_Parent()
    {
        var result = Eval(ThreeDivs, """
            (() => {
                var b = document.body;
                b.moveBefore(document.getElementById('x'), document.getElementById('z'));
                return [...b.children].map(e => e.id).join(',');
            })()
            """);

        Assert.Equal("y,x,z", result);
    }

    [Fact]
    public void Moving_Before_Null_Appends()
    {
        var result = Eval(ThreeDivs, """
            (() => {
                var b = document.body;
                b.moveBefore(document.getElementById('x'), null);
                return [...b.children].map(e => e.id).join(',');
            })()
            """);

        Assert.Equal("y,z,x", result);
    }

    [Fact]
    public void Moves_A_Child_Across_Parents()
    {
        const string html =
            "<!DOCTYPE html><html><body><div id=\"src\"><span id=\"m\"></span></div><div id=\"dst\"></div></body></html>";

        var result = Eval(html, """
            (() => {
                var m = document.getElementById('m');
                var dst = document.getElementById('dst');
                dst.moveBefore(m, null);
                return (m.parentNode.id) + '|' + document.getElementById('src').children.length;
            })()
            """);

        Assert.Equal("dst|0", result);
    }

    // Per spec the reference collapses to the node's next sibling, so this is a no-op.
    [Fact]
    public void Moving_A_Node_Before_Itself_Is_A_No_Op()
    {
        var result = Eval(ThreeDivs, """
            (() => {
                var b = document.body;
                var x = document.getElementById('x');
                b.moveBefore(x, x);
                return [...b.children].map(e => e.id).join(',');
            })()
            """);

        Assert.Equal("x,y,z", result);
    }

    // The case the WPT test is about: a moved <style> keeps its rules and stays connected.
    [Fact]
    public void A_Moved_Style_Element_Keeps_Its_Content_And_Connection()
    {
        const string html =
            "<!DOCTYPE html><html><body><div id=\"src\"><style id=\"s\">body { background-color: green; }</style></div>" +
            "<div id=\"dst\"></div></body></html>";

        var result = Eval(html, """
            (() => {
                var s = document.getElementById('s');
                document.getElementById('dst').moveBefore(s, null);
                return s.parentNode.id + '|' + s.isConnected + '|' + s.textContent.trim();
            })()
            """);

        Assert.Equal("dst|true|body { background-color: green; }", result);
    }

    // A move has nothing to preserve for a node that was never in the tree, so the spec
    // rejects it rather than quietly behaving like insertBefore.
    [Fact]
    public void Rejects_A_Node_That_Is_Not_Already_In_The_Tree()
    {
        var result = Eval(ThreeDivs, """
            (() => {
                try {
                    document.body.moveBefore(document.createElement('span'), null);
                    return 'no-throw';
                } catch (e) { return 'threw'; }
            })()
            """);

        Assert.Equal("threw", result);
    }

    [Fact]
    public void Rejects_A_Reference_That_Is_Not_A_Child()
    {
        const string html =
            "<!DOCTYPE html><html><body><div id=\"dst\"></div><div id=\"m\"></div><div id=\"other\"></div></body></html>";

        var result = Eval(html, """
            (() => {
                try {
                    document.getElementById('dst').moveBefore(
                        document.getElementById('m'), document.getElementById('other'));
                    return 'no-throw';
                } catch (e) { return 'threw'; }
            })()
            """);

        Assert.Equal("threw", result);
    }

    [Fact]
    public void Rejects_Moving_A_Node_Into_Its_Own_Descendant()
    {
        const string html =
            "<!DOCTYPE html><html><body><div id=\"outer\"><div id=\"inner\"></div></div></body></html>";

        var result = Eval(html, """
            (() => {
                try {
                    document.getElementById('inner').moveBefore(document.getElementById('outer'), null);
                    return 'no-throw';
                } catch (e) { return 'threw'; }
            })()
            """);

        Assert.Equal("threw", result);
    }
}
