using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 4 third slice (P4.3, part of work item 1):
/// the <c>#document-fragment</c> sentinel element is replaced by the canonical
/// <see cref="Broiler.Dom.DomDocumentFragment"/>. The fragment was a fake-tag <c>DomElement</c>
/// (TagName "#document-fragment"); it now IS a canonical DocumentFragment node with a dedicated
/// container JS wrapper. These characterizations exercise the reachable fragment paths — creation,
/// child manipulation, unpack-on-insert, querying, cloneNode, and Range-produced fragments — and pin
/// the observable behaviour across the migration.
/// </summary>
public sealed class DocumentFragmentSentinelMigrationTests
{
    private static DomBridge Attach(string html, out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");
        return bridge;
    }

    [Fact]
    public void CreateDocumentFragment_Reports_Fragment_Node()
    {
        using var bridge = Attach("<!DOCTYPE html><html><body></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var f = document.createDocumentFragment();
                return f.nodeType + '|' + f.nodeName + '|' + (f.ownerDocument === document) + '|' + f.childNodes.length;
            })()
            """);

        Assert.Equal("11|#document-fragment|true|0", result.ToString());
    }

    [Fact]
    public void Append_Unpacks_Fragment_Children_Into_Host()
    {
        using var bridge = Attach("<!DOCTYPE html><html><body><div id=\"host\"></div></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var host = document.getElementById('host');
                var f = document.createDocumentFragment();
                var a = document.createElement('span'); a.textContent = 'one';
                var b = document.createElement('span'); b.textContent = 'two';
                f.appendChild(a);
                f.appendChild(b);
                host.append(f);
                return host.childNodes.length + '|' + f.childNodes.length + '|' +
                       host.firstChild.textContent + '|' + host.lastChild.textContent;
            })()
            """);

        Assert.Equal("2|0|one|two", result.ToString());
    }

    [Fact]
    public void Fragment_Query_And_Children_Work()
    {
        using var bridge = Attach("<!DOCTYPE html><html><body></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var f = document.createDocumentFragment();
                var p = document.createElement('p'); p.className = 'x'; p.id = 'pp';
                f.appendChild(p);
                f.appendChild(document.createElement('b'));
                var q = f.querySelector('.x');
                // getElementById is intentionally NOT on the bridge's fragment surface (behaviour
                // preserved across the migration) — assert its absence so the guard stays honest.
                return f.children.length + '|' + (q === p) + '|' + (f.getElementById ? 'has-gebid' : 'no-gebid');
            })()
            """);

        Assert.Equal("2|true|no-gebid", result.ToString());
    }

    [Fact]
    public void Fragment_CloneNode_Deep_Copies_Children()
    {
        using var bridge = Attach("<!DOCTYPE html><html><body></body></html>", out var context);

        var result = context.Eval("""
            (() => {
                var f = document.createDocumentFragment();
                var s = document.createElement('span'); s.textContent = 'hi';
                f.appendChild(s);
                var c = f.cloneNode(true);
                return c.nodeType + '|' + c.childNodes.length + '|' + c.firstChild.textContent + '|' + (c.firstChild !== s);
            })()
            """);

        Assert.Equal("11|1|hi|true", result.ToString());
    }

    [Fact]
    public void Range_ExtractContents_Returns_Fragment()
    {
        using var bridge = Attach(
            "<!DOCTYPE html><html><body><div id=\"t\"><span>a</span><span>b</span></div></body></html>",
            out var context);

        var result = context.Eval("""
            (() => {
                var t = document.getElementById('t');
                var range = document.createRange();
                range.selectNodeContents(t);
                var frag = range.extractContents();
                return frag.nodeType + '|' + frag.childNodes.length + '|' + t.childNodes.length +
                       '|' + frag.childNodes[0].textContent + frag.childNodes[1].textContent;
            })()
            """);

        Assert.Equal("11|2|0|ab", result.ToString());
    }
}
