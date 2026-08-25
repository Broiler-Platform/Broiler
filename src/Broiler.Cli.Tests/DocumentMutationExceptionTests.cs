using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

/// <summary>
/// The document-level <c>removeChild</c>/<c>insertBefore</c> raise <c>NotFoundError</c> for a node
/// that is not a child of the document (DOM §4.2.3) — the counterpart to
/// <see cref="TreeMutationExceptionTests"/>, which covers the element-level methods.
/// </summary>
/// <remarks>
/// These are a separate code path (<c>NodeMutationBinding</c> rather than
/// <c>TreeMutationBinding</c>), and <c>document.insertBefore</c> had the worst shape in the whole
/// family: given a reference node that was not a child it fell through to <em>append</em>, so the
/// node was silently mutated into a position the caller never asked for — the end of the document
/// instead of before the reference. <c>document.removeChild</c> returned the node unchanged, which is
/// what a successful call returns.
/// <para>
/// A <em>null</em> reference still appends. That is the specified behaviour of
/// <c>insertBefore(node, null)</c>, not a fallback, and the tests below pin it so the throw added for
/// the not-found case cannot swallow it.
/// </para>
/// </remarks>
public class DocumentMutationExceptionTests
{
    private static string ExecJs(string jsCode)
    {
        var html = $@"<!doctype html>
<html><head><title>T</title></head><body><div id=""o"">x</div><div id=""result""></div>
<script>
{jsCode}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
    }

    [Fact(Timeout = 600000)]
    public void Removing_A_Node_That_Is_Not_A_Document_Child_Throws_NotFoundError()
    {
        var result = ExecJs(@"
            var o = document.getElementById('o');
            var out = 'NOTHROW';
            try { document.removeChild(o); }
            catch (e) { out = (e instanceof DOMException) + '|' + e.name + '|' + e.code; }
            document.getElementById('result').textContent = 'V:' + out;
        ");
        Assert.Contains("V:true|NotFoundError|8", result);
    }

    [Fact(Timeout = 600000)]
    public void Inserting_Before_A_Node_That_Is_Not_A_Document_Child_Throws_NotFoundError()
    {
        var result = ExecJs(@"
            var o = document.getElementById('o');
            var out = 'NOTHROW';
            try { document.insertBefore(document.createComment('c'), o); }
            catch (e) { out = (e instanceof DOMException) + '|' + e.name + '|' + e.code; }
            document.getElementById('result').textContent = 'V:' + out;
        ");
        Assert.Contains("V:true|NotFoundError|8", result);
    }

    // A node that was never in the tree at all takes the same path.
    [Fact(Timeout = 600000)]
    public void Removing_A_Node_That_Was_Never_Inserted_Throws_NotFoundError()
    {
        var result = ExecJs(@"
            var out = 'NOTHROW';
            try { document.removeChild(document.createComment('never')); }
            catch (e) { out = e.name + '|' + e.code; }
            document.getElementById('result').textContent = 'V:' + out;
        ");
        Assert.Contains("V:NotFoundError|8", result);
    }

    [Fact(Timeout = 600000)]
    public void The_Error_Message_Names_The_Method()
    {
        var result = ExecJs(@"
            var o = document.getElementById('o');
            try { document.removeChild(o); }
            catch (e) { document.getElementById('result').textContent = 'V:' + e.message; }
        ");
        Assert.Contains("removeChild", result);
        Assert.Contains("not a child of this node", result);
    }

    // insertBefore(node, null) appends — the specified behaviour — and the appended node is a real
    // document child afterwards, which the round-trip removal proves.
    [Fact(Timeout = 600000)]
    public void A_Null_Reference_Still_Appends_And_Round_Trips()
    {
        var result = ExecJs(@"
            var c = document.createComment('rt');
            document.insertBefore(c, null);
            var removed = document.removeChild(c);
            document.getElementById('result').textContent = 'V:appended|same=' + (removed === c);
        ");
        Assert.Contains("V:appended|same=true", result);
    }

    // Inserting before a genuine document child works, and leaves the document intact.
    [Fact(Timeout = 600000)]
    public void Inserting_Before_A_Real_Document_Child_Succeeds()
    {
        var result = ExecJs(@"
            var de = document.documentElement;
            var c = document.createComment('before');
            document.insertBefore(c, de);
            var removed = document.removeChild(c);
            document.getElementById('result').textContent =
                'V:ok|same=' + (removed === c) + '|intact=' + (document.documentElement === de);
        ");
        Assert.Contains("V:ok|same=true|intact=true", result);
    }
}
