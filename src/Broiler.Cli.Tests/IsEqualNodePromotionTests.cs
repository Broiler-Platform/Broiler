namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 4 items 4/5: node equality (<c>Node.isEqualNode()</c>) is slated to move to the canonical
/// <c>Broiler.Dom.DomNode.IsEqualNode</c> tree algorithm, with the bridge's <c>NodesAreEqual</c> copy
/// deleted and its <c>isEqualNode</c> binding delegating. That promotion is a Broiler.DOM submodule
/// change; the submodule push is outside this session's GitHub scope (403), so it ships as
/// <c>patches/0001-add-domnode-isequalnode.patch</c> and the bridge keeps its <c>NodesAreEqual</c>
/// implementation as the active fallback until the patch lands and the pointer is bumped.
///
/// These end-to-end characterizations pin the observable <c>isEqualNode</c> behaviour so the
/// eventual delegation is provably equivalent (they pass against the bridge copy today and must keep
/// passing after the canonical delegation).
/// </summary>
public sealed class IsEqualNodePromotionTests
{
    private static string Run(string bodyScript)
    {
        var html = $@"<!DOCTYPE html><html><body>
<div id=""result""></div>
<script>
function put(v) {{ document.getElementById('result').textContent = String(v); }}
{bodyScript}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
    }

    [Fact]
    public void Equal_And_Unequal_Element_Trees()
    {
        var result = Run(
            "var a = document.createElement('div'); a.setAttribute('class','c'); a.innerHTML = '<span>x</span>';" +
            "var b = document.createElement('div'); b.setAttribute('class','c'); b.innerHTML = '<span>x</span>';" +
            "var c = document.createElement('div'); c.setAttribute('class','c'); c.innerHTML = '<span>y</span>';" +
            "put(a.isEqualNode(b) + '|' + a.isEqualNode(c) + '|' + a.isEqualNode(null));");
        Assert.Contains("true|false|false", result);
    }

    [Fact]
    public void Attribute_Order_Irrelevant_But_Values_Matter()
    {
        var result = Run(
            "var a = document.createElement('div'); a.setAttribute('id','x'); a.setAttribute('data-k','v');" +
            "var b = document.createElement('div'); b.setAttribute('data-k','v'); b.setAttribute('id','x');" +
            "var c = document.createElement('div'); c.setAttribute('id','x'); c.setAttribute('data-k','w');" +
            "put(a.isEqualNode(b) + '|' + a.isEqualNode(c));");
        Assert.Contains("true|false", result);
    }

    [Fact]
    public void Text_Nodes_Equal_By_Data()
    {
        var result = Run(
            "put(document.createTextNode('t').isEqualNode(document.createTextNode('t')) + '|' +" +
            "    document.createTextNode('t').isEqualNode(document.createTextNode('u')));");
        Assert.Contains("true|false", result);
    }
}
