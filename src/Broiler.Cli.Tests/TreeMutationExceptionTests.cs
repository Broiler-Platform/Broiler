using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>insertBefore</c>, <c>removeChild</c> and <c>replaceChild</c> raise a real <c>NotFoundError</c>
/// <c>DOMException</c> when the named child is not a child of the parent (DOM §4.2.3).
/// </summary>
/// <remarks>
/// <c>insertBefore</c> threw a plain error whose message merely began with the name, so
/// <c>e instanceof DOMException</c> was false and <c>e.name</c> was <c>"Error"</c>. The other two were
/// worse: they returned <em>the value a successful call returns</em> — the removed/replaced node — so
/// a caller was told the mutation had happened when nothing had. Code that removes a node and then
/// re-parents the returned value silently operated on a node still attached to its original parent.
/// <para>
/// The <c>HierarchyRequestError</c> guard beside these call sites was already correct, so the
/// exception machinery was present and only the not-found branches were missing it.
/// </para>
/// </remarks>
public class TreeMutationExceptionTests
{
    private static string ExecJs(string jsCode)
    {
        var html = $@"<!doctype html>
<html><head><title>T</title></head>
<body><div id=""p""><span id=""c1"">a</span><span id=""c2"">b</span></div><div id=""other"">x</div>
<div id=""result""></div>
<script>
{jsCode}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
    }

    [Theory(Timeout = 600000)]
    [InlineData("p.removeChild(o)")]
    [InlineData("p.insertBefore(document.createElement('i'), o)")]
    [InlineData("p.replaceChild(document.createElement('i'), o)")]
    public void Operating_On_A_Node_That_Is_Not_A_Child_Throws_NotFoundError(string call)
    {
        var result = ExecJs($@"
            var p = document.getElementById('p'), o = document.getElementById('other');
            var out = 'NOTHROW';
            try {{ {call}; }}
            catch (e) {{ out = (e instanceof DOMException) + '|' + e.name + '|' + e.code; }}
            document.getElementById('result').textContent = 'V:' + out;
        ");
        Assert.Contains("V:true|NotFoundError|8", result);
    }

    [Fact(Timeout = 600000)]
    public void The_Error_Message_Names_The_Method()
    {
        var result = ExecJs(@"
            var p = document.getElementById('p'), o = document.getElementById('other');
            try { p.removeChild(o); }
            catch (e) { document.getElementById('result').textContent = 'V:' + e.message; }
        ");
        Assert.Contains("removeChild", result);
        Assert.Contains("not a child of this node", result);
    }

    // The circular-reference guard sharing these call sites must keep its own, different error.
    [Fact(Timeout = 600000)]
    public void A_Cycle_Is_Still_A_HierarchyRequestError()
    {
        var result = ExecJs(@"
            var p = document.getElementById('p');
            var out = 'NOTHROW';
            try { p.appendChild(document.body); }
            catch (e) { out = (e instanceof DOMException) + '|' + e.name + '|' + e.code; }
            document.getElementById('result').textContent = 'V:' + out;
        ");
        Assert.Contains("V:true|HierarchyRequestError|3", result);
    }

    // Every in-range mutation is unchanged, including removeChild's return value and the
    // insertBefore(node, null) append form.
    [Fact(Timeout = 600000)]
    public void Successful_Mutations_Are_Unchanged()
    {
        var result = ExecJs(@"
            function ids(el) { return Array.prototype.map.call(el.children, function (e) { return e.id; }).join(','); }
            var p = document.getElementById('p');
            var parts = [];

            var n1 = document.createElement('i'); n1.id = 'n1';
            p.insertBefore(n1, document.getElementById('c2'));
            parts.push(ids(p));                                   // c1,n1,c2

            var n2 = document.createElement('i'); n2.id = 'n2';
            p.insertBefore(n2, null);                             // null ref appends
            parts.push(ids(p));                                   // c1,n1,c2,n2

            var n3 = document.createElement('i'); n3.id = 'n3';
            p.replaceChild(n3, document.getElementById('c1'));
            parts.push(ids(p));                                   // n3,n1,c2,n2

            var removed = p.removeChild(document.getElementById('c2'));
            parts.push(removed.id + ':' + ids(p));                // c2:n3,n1,n2

            document.getElementById('result').textContent = 'V:' + parts.join('|');
        ");
        Assert.Contains("V:c1,n1,c2|c1,n1,c2,n2|n3,n1,c2,n2|c2:n3,n1,n2", result);
    }

    // A DOMException is also an Error, so existing message-based handling still works.
    [Fact(Timeout = 600000)]
    public void The_Thrown_Value_Is_Still_An_Error()
    {
        var result = ExecJs(@"
            var p = document.getElementById('p'), o = document.getElementById('other');
            try { p.removeChild(o); }
            catch (e) { document.getElementById('result').textContent = 'V:' + (e instanceof Error) + ',' + (typeof e.message); }
        ");
        Assert.Contains("V:true,string", result);
    }
}
