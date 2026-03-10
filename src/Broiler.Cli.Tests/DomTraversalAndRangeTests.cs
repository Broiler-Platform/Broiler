namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 2 Acid3 compliance: DOM Traversal (TreeWalker, NodeIterator,
/// NodeFilter) and Range APIs.
/// </summary>
public class DomTraversalAndRangeTests
{
    // ──────────────────────── NodeFilter constants ────────────────────────

    [Fact]
    public void NodeFilter_Constants_Are_Available()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var r = [];
r.push(NodeFilter.FILTER_ACCEPT === 1);
r.push(NodeFilter.FILTER_REJECT === 2);
r.push(NodeFilter.FILTER_SKIP === 3);
r.push(NodeFilter.SHOW_ALL === 0xFFFFFFFF);
r.push(NodeFilter.SHOW_ELEMENT === 0x1);
r.push(NodeFilter.SHOW_TEXT === 0x4);
r.push(NodeFilter.SHOW_COMMENT === 0x80);

var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,true,true,true,true,true", result);
    }

    // ──────────────────────── Basic DOM properties ────────────────────────

    [Fact]
    public void NodeType_And_NodeName_Are_Correct()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">hello</div>
<script>
var div = document.getElementById('target');
var text = div.firstChild;
var r = [];
r.push(div.nodeType);      // 1 (ELEMENT_NODE)
r.push(div.nodeName);      // DIV
r.push(text.nodeType);     // 3 (TEXT_NODE)
r.push(text.nodeName);     // #text

var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("1,DIV,3,#text", result);
    }

    [Fact]
    public void PreviousSibling_Works()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""a"">A</div>
<div id=""b"">B</div>
<script>
var b = document.getElementById('b');
var prev = b.previousSibling;
var out = document.createElement('div');
out.id = 'result';
// prev could be a text node (whitespace) or the div, check nodeName
out.textContent = prev !== null ? 'has-prev' : 'no-prev';
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("has-prev", result);
    }

    [Fact]
    public void HasChildNodes_And_Contains_Work()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><span id=""child"">text</span></div>
<script>
var parent = document.getElementById('parent');
var child = document.getElementById('child');
var r = [];
r.push(parent.hasChildNodes());  // true
r.push(parent.contains(child));  // true
r.push(child.contains(parent));  // false

var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,true,false", result);
    }

    [Fact]
    public void CloneNode_Deep_Works()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""original""><span>inside</span></div>
<script>
var orig = document.getElementById('original');
var clone = orig.cloneNode(true);
clone.id = 'cloned';
document.body.appendChild(clone);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("id=\"cloned\"", result);
        Assert.Contains("<span>inside</span>", result);
    }

    [Fact]
    public void InsertBefore_Works()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""><span id=""existing"">old</span></div>
<script>
var container = document.getElementById('container');
var existing = document.getElementById('existing');
var newEl = document.createElement('p');
newEl.textContent = 'inserted';
container.insertBefore(newEl, existing);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // The <p> should appear before the <span>
        var pIdx = result.IndexOf("<p>inserted</p>");
        var spanIdx = result.IndexOf("<span");
        Assert.True(pIdx >= 0, "Inserted element not found");
        Assert.True(pIdx < spanIdx, "Inserted element should appear before existing element");
    }

    [Fact]
    public void HasAttribute_And_RemoveAttribute_Work()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"" data-foo=""bar"">test</div>
<script>
var el = document.getElementById('target');
var r = [];
r.push(el.hasAttribute('data-foo'));   // true
r.push(el.hasAttribute('data-baz'));   // false
el.removeAttribute('data-foo');
r.push(el.hasAttribute('data-foo'));   // false

var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,false,false", result);
    }

    // ──────────────────────── TreeWalker ────────────────────────

    [Fact]
    public void TreeWalker_BasicTraversal()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root"">
  <p id=""p1"">para1</p>
  <p id=""p2"">para2</p>
</div>
<script>
var root = document.getElementById('root');
var tw = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null);
var r = [];
var node;
while (node = tw.nextNode()) {
    r.push(node.tagName);
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("P,P", result);
    }

    [Fact]
    public void TreeWalker_ShowText_Only()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><span>hello</span><span>world</span></div>
<script>
var root = document.getElementById('root');
var tw = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
var r = [];
var node;
while (node = tw.nextNode()) {
    r.push(node.nodeValue);
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("hello|world", result);
    }

    [Fact]
    public void TreeWalker_FirstChild_And_NextSibling()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root"">
  <p id=""p1"">A</p>
  <p id=""p2"">B</p>
  <p id=""p3"">C</p>
</div>
<script>
var root = document.getElementById('root');
var tw = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null);
var r = [];
var child = tw.firstChild();
if (child) r.push(child.id);
var sib = tw.nextSibling();
if (sib) r.push(sib.id);
sib = tw.nextSibling();
if (sib) r.push(sib.id);

var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("p1,p2,p3", result);
    }

    [Fact]
    public void TreeWalker_ParentNode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><p id=""child"">text</p></div>
<script>
var root = document.getElementById('root');
var tw = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null);
tw.firstChild(); // moves to p
var parent = tw.parentNode();
var out = document.createElement('div');
out.id = 'result';
out.textContent = parent ? parent.id : 'none';
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("root", result);
    }

    [Fact]
    public void TreeWalker_WithFilter()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root"">
  <p class=""include"">yes1</p>
  <p class=""exclude"">no</p>
  <p class=""include"">yes2</p>
</div>
<script>
var root = document.getElementById('root');
var tw = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, {
    acceptNode: function(node) {
        return node.className === 'include' ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP;
    }
});
var r = [];
var node;
while (node = tw.nextNode()) {
    r.push(node.textContent);
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("yes1,yes2", result);
    }

    // ──────────────────────── NodeIterator ────────────────────────

    [Fact]
    public void NodeIterator_BasicIteration()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><p>A</p><p>B</p></div>
<script>
var root = document.getElementById('root');
var iter = document.createNodeIterator(root, NodeFilter.SHOW_ELEMENT, null);
var r = [];
var node;
while (node = iter.nextNode()) {
    r.push(node.tagName);
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // Should iterate: DIV, P, P (root is included)
        Assert.Contains("DIV,P,P", result);
    }

    [Fact]
    public void NodeIterator_PreviousNode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><p id=""p1"">A</p><p id=""p2"">B</p></div>
<script>
var root = document.getElementById('root');
var iter = document.createNodeIterator(root, NodeFilter.SHOW_ELEMENT, null);
iter.nextNode(); // DIV (root)
iter.nextNode(); // P#p1
iter.nextNode(); // P#p2
var prev = iter.previousNode(); // back to P#p2
var out = document.createElement('div');
out.id = 'result';
out.textContent = prev ? prev.id : 'none';
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("p2", result);
    }

    [Fact]
    public void NodeIterator_Detach()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><p>A</p></div>
<script>
var root = document.getElementById('root');
var iter = document.createNodeIterator(root, NodeFilter.SHOW_ELEMENT, null);
iter.nextNode();  // root
iter.detach();
var next = iter.nextNode();  // should return null after detach
var out = document.createElement('div');
out.id = 'result';
out.textContent = next === null ? 'detached' : 'not-detached';
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("detached", result);
    }

    [Fact]
    public void NodeIterator_ShowText()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><span>hello</span> <span>world</span></div>
<script>
var root = document.getElementById('root');
var iter = document.createNodeIterator(root, NodeFilter.SHOW_TEXT, null);
var r = [];
var node;
while (node = iter.nextNode()) {
    r.push(node.nodeValue);
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("hello", result);
        Assert.Contains("world", result);
    }

    // ──────────────────────── Range ────────────────────────

    [Fact]
    public void Range_CreateRange_Returns_Collapsed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var range = document.createRange();
var out = document.createElement('div');
out.id = 'result';
out.textContent = range.collapsed ? 'collapsed' : 'not-collapsed';
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("collapsed", result);
    }

    [Fact]
    public void Range_SelectNodeContents()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""><p>A</p><p>B</p></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.selectNodeContents(target);

var out = document.createElement('div');
out.id = 'result';
var r = [];
r.push(range.startOffset);  // 0
r.push(range.endOffset);    // 2 (two children)
r.push(range.collapsed);    // false
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,2,false", result);
    }

    [Fact]
    public void Range_CloneContents()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""><p id=""p1"">A</p><p id=""p2"">B</p></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.selectNodeContents(target);
var fragment = range.cloneContents();
var out = document.createElement('div');
out.id = 'result';
out.textContent = fragment.childNodes.length;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">2<", result);
    }

    [Fact]
    public void Range_ExtractContents()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""><p id=""p1"">A</p><p id=""p2"">B</p></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.selectNodeContents(target);
var fragment = range.extractContents();
var r = [];
r.push(fragment.childNodes.length);   // 2 (extracted nodes)
r.push(target.childNodes.length);     // 0 (original emptied)
r.push(range.collapsed);              // true (collapsed after extract)
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("2,0,true", result);
    }

    [Fact]
    public void Range_Collapse()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""><p>A</p><p>B</p></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.selectNodeContents(target);
range.collapse(true);  // collapse to start
var r = [];
r.push(range.collapsed);    // true
r.push(range.startOffset);  // 0
r.push(range.endOffset);    // 0
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true,0,0", result);
    }

    [Fact]
    public void Range_InsertNode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""><p>A</p><p>B</p></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.setStart(target, 1);
range.setEnd(target, 1);
var inserted = document.createElement('span');
inserted.textContent = 'INSERTED';
range.insertNode(inserted);
var out = document.createElement('div');
out.id = 'result';
out.textContent = target.childNodes.length; // 3 (p, span, p)
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">3<", result);
        Assert.Contains("INSERTED", result);
    }

    [Fact]
    public void Range_DeleteContents()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""><p>A</p><p>B</p><p>C</p></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.selectNodeContents(target);
range.deleteContents();
var out = document.createElement('div');
out.id = 'result';
out.textContent = target.childNodes.length; // 0
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains(">0<", result);
    }

    [Fact]
    public void Range_SelectNode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent""><p id=""target"">text</p></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.selectNode(target);
var r = [];
r.push(range.startOffset);  // index of target in parent
r.push(range.endOffset);     // startOffset + 1
r.push(range.collapsed);    // false
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // The <p> is the first child of <div>, so startOffset=0, endOffset=1
        Assert.Contains("0,1,false", result);
    }

    [Fact]
    public void Range_CloneRange()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""><p>A</p><p>B</p></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.selectNodeContents(target);
var clone = range.cloneRange();
var r = [];
r.push(clone.startOffset);   // 0
r.push(clone.endOffset);     // 2
r.push(clone.collapsed);     // false
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,2,false", result);
    }

    [Fact]
    public void Range_ToString()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""><span>hello</span><span>world</span></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.selectNodeContents(target);
var out = document.createElement('div');
out.id = 'result';
out.textContent = range.toString();
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("helloworld", result);
    }

    // ──────────────────────── Document API additions ────────────────────────

    [Fact]
    public void Document_CreateComment_Works()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var comment = document.createComment('test comment');
var out = document.createElement('div');
out.id = 'result';
out.textContent = comment.nodeType + ',' + comment.nodeValue;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("8,test comment", result);
    }

    [Fact]
    public void Element_Navigation_Properties_Work()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""parent"">
  <p id=""first"">A</p>
  <p id=""second"">B</p>
  <p id=""third"">C</p>
</div>
<script>
var parent = document.getElementById('parent');
var r = [];
r.push(parent.childElementCount);
var fe = parent.firstElementChild;
r.push(fe ? fe.id : 'null');
var le = parent.lastElementChild;
r.push(le ? le.id : 'null');
var ne = fe.nextElementSibling;
r.push(ne ? ne.id : 'null');
var pe = le.previousElementSibling;
r.push(pe ? pe.id : 'null');

var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("3,first,third,second,second", result);
    }
}
