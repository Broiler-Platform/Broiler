using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The shared geometry snapshot is retained across read passes and reused while nothing it is a
/// function of has changed (<c>DomBridge.AcquireSharedGeometrySnapshot</c>). That is what stops a
/// script that walks a collection from paying a whole-document layout per element per property — the
/// shape behind WPT issue #1682's twenty <c>css/css-overflow/overflow-alignment-*</c> timeouts.
/// <para>
/// Reuse is only correct if every layout input is in the key, so these are the staleness tests: each
/// mutates one input between two geometry reads and asserts the second read moved. They fail on a
/// snapshot that is reused when it should not be, which is the one way this optimisation can be
/// wrong — and the way a pixel suite would report as a wrong bitmap rather than as a cache bug.
/// </para>
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class RetainedLayoutSnapshotTests
{
    private static string Eval(string bodyHtml, string expr)
    {
        var html = "<!DOCTYPE html><html><head><style>html,body{margin:0;padding:0}</style></head><body>" +
                   bodyHtml + "</body></html>";
        using var ctx = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(ctx, html, "file:///retained-snapshot.html");
        return ctx.Eval(expr).ToString();
    }

    [Fact]
    public void Inline_Style_Write_Between_Reads_Is_Observed()
    {
        // element.style writes land in the bridge's own inline-style store and never reach the
        // `style=` attribute at script time, so DomDocument.Version does not move for them — this is
        // the case the BridgeRuntimeStateEpoch half of the key exists for.
        var body = "<div id='a' style='width:100px;height:40px'></div>";
        Assert.Equal("100|250", Eval(body,
            "(function(){var a=document.getElementById('a');var before=a.offsetWidth;" +
            "a.style.width='250px';return before+'|'+a.offsetWidth;})()"));
    }

    [Fact]
    public void SetProperty_Between_Reads_Is_Observed()
    {
        var body = "<div id='a' style='width:100px;height:40px'></div>";
        Assert.Equal("40|90", Eval(body,
            "(function(){var a=document.getElementById('a');var before=a.offsetHeight;" +
            "a.style.setProperty('height','90px');return before+'|'+a.offsetHeight;})()"));
    }

    [Fact]
    public void Style_Attribute_Write_Between_Reads_Is_Observed()
    {
        var body = "<div id='a' style='width:100px;height:40px'></div>";
        Assert.Equal("100|175", Eval(body,
            "(function(){var a=document.getElementById('a');var before=a.offsetWidth;" +
            "a.setAttribute('style','width:175px;height:40px');return before+'|'+a.offsetWidth;})()"));
    }

    [Fact]
    public void Class_Change_Between_Reads_Is_Observed()
    {
        // A cascade change with no inline style involved: the element's own declarations are
        // unchanged and only the selector that matches it moves. The base width has to lose the
        // cascade to `.wide`, so it is a type selector — an inline `style=` outranks a class rule
        // whatever its specificity, and so does an `#id` rule, and either would keep the pre-change
        // width and let the test pass without proving anything.
        var body = "<style>div{width:100px;height:40px} .wide{width:300px}</style><div id='a'></div>";
        Assert.Equal("100|300", Eval(body,
            "(function(){var a=document.getElementById('a');var before=a.offsetWidth;" +
            "a.className='wide';return before+'|'+a.offsetWidth;})()"));
    }

    [Fact]
    public void Dom_Insertion_Between_Reads_Is_Observed()
    {
        // The container is auto-height, so appending a child changes geometry the snapshot holds for
        // an element the script never touched.
        var body = "<div id='box'><div style='height:20px'></div></div>";
        Assert.Equal("20|70", Eval(body,
            "(function(){var box=document.getElementById('box');var before=box.offsetHeight;" +
            "var d=document.createElement('div');d.style.height='50px';box.appendChild(d);" +
            "return before+'|'+box.offsetHeight;})()"));
    }

    [Fact]
    public void Text_Change_Between_Reads_Is_Observed()
    {
        var body = "<style>#a{width:60px;font:16px/20px monospace}</style><div id='a'>one</div>";
        // Three short words wrap in a 60px box where one does not, so the block grows.
        Assert.Equal("true", Eval(body,
            "(function(){var a=document.getElementById('a');var before=a.offsetHeight;" +
            "a.textContent='one two three four five six';return a.offsetHeight>before;})()"));
    }

    [Fact]
    public void Stylesheet_Rule_Insertion_Between_Reads_Is_Observed()
    {
        // A CSSOM edit: the rule model is bridge-side runtime state that the DOM text never sees, so
        // DomDocument.Version does not move for it — the other half of the key's job. The inserted
        // rule is appended so it wins on order at equal specificity.
        var body = "<style>#a{height:40px}</style><div id='a'></div>";
        Assert.Equal("40|140", Eval(body,
            "(function(){var a=document.getElementById('a');var before=a.offsetHeight;" +
            "var s=document.styleSheets[document.styleSheets.length-1];" +
            "s.insertRule('#a{height:140px}', s.cssRules.length);" +
            "return before+'|'+a.offsetHeight;})()"));
    }

    [Fact]
    public void Scroll_Write_Between_Reads_Leaves_Box_Geometry_Alone()
    {
        // The other side of the key: scroll offsets are deliberately excluded from it, because the
        // renderer never sees one. This pins the premise — if a scroll write ever did move box
        // geometry, excluding it would be a staleness bug rather than an optimisation.
        var body = "<div id='s' style='width:50px;height:50px;overflow:auto'>" +
                   "<div id='c' style='width:200px;height:200px'></div></div>";
        Assert.Equal("50,50,200,200|50,50,200,200|30", Eval(body,
            "(function(){var s=document.getElementById('s');var c=document.getElementById('c');" +
            "function m(){return s.offsetWidth+','+s.offsetHeight+','+c.offsetWidth+','+c.offsetHeight;}" +
            "var before=m();s.scrollTop=30;return before+'|'+m()+'|'+s.scrollTop;})()"));
    }

    [Fact]
    public void Repeated_Scroll_Writes_Clamp_To_The_Same_Bounds_As_The_First()
    {
        // Clamping reads four extents through the snapshot, so a scroll loop is exactly the workload
        // that reuses one layout. Every element must still clamp to its own bounds, not to whichever
        // one happened to be measured when the snapshot was built.
        var body =
            "<div class='s' id='s1' style='width:50px;height:50px;overflow:auto'><div style='width:60px;height:150px'></div></div>" +
            "<div class='s' id='s2' style='width:50px;height:50px;overflow:auto'><div style='width:60px;height:450px'></div></div>" +
            "<div class='s' id='s3' style='width:50px;height:50px;overflow:auto'><div style='width:60px;height:250px'></div></div>";
        // Content heights 150/450/250 against a 50px box: 100/400/200 of scrollable range each.
        Assert.Equal("100|400|200", Eval(body,
            "(function(){var out=[];var all=document.getElementsByClassName('s');" +
            "for (var i=0;i<all.length;i++){all[i].scrollTop=10000;out.push(all[i].scrollTop);}" +
            "return out.join('|');})()"));
    }
}
