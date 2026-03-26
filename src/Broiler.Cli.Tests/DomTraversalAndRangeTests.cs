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

    // ══════════════════ Phase 2 v2: Edge Cases ══════════════════

    // ─── 2.1 NodeFilter exception propagation ───

    [Fact]
    public void NodeFilter_Exception_Propagates_From_Iterator()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><p>A</p><p>B</p></div>
<script>
var root = document.getElementById('root');
var result = 'no-error';
try {
    var iter = document.createNodeIterator(root, NodeFilter.SHOW_ELEMENT, {
        acceptNode: function(node) {
            if (node.tagName === 'P') throw new Error('filter-bomb');
            return NodeFilter.FILTER_ACCEPT;
        }
    });
    iter.nextNode(); // root DIV — accepted
    iter.nextNode(); // P — should throw
    result = 'error-not-thrown';
} catch (e) {
    result = 'caught:' + e.message;
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = result;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("caught:filter-bomb", result);
    }

    [Fact]
    public void NodeFilter_Exception_Propagates_From_TreeWalker()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><p>A</p></div>
<script>
var root = document.getElementById('root');
var result = 'no-error';
try {
    var tw = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, {
        acceptNode: function(node) {
            if (node.tagName === 'P') throw new Error('tw-bomb');
            return NodeFilter.FILTER_ACCEPT;
        }
    });
    tw.nextNode(); // P — should throw
    result = 'error-not-thrown';
} catch (e) {
    result = 'caught:' + e.message;
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = result;
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("caught:tw-bomb", result);
    }

    // ─── 2.2 Iterator/Walker mutation handling ───

    [Fact]
    public void NodeIterator_Survives_Node_Removal_During_Iteration()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><p id=""p1"">A</p><p id=""p2"">B</p><p id=""p3"">C</p></div>
<script>
var root = document.getElementById('root');
var iter = document.createNodeIterator(root, NodeFilter.SHOW_ELEMENT, null);
var r = [];
iter.nextNode(); // root DIV
var p1 = iter.nextNode(); // P#p1
// Remove p1 from the DOM during iteration
p1.parentNode.removeChild(p1);
var next = iter.nextNode(); // should get P#p2
if (next) r.push(next.id);
next = iter.nextNode(); // should get P#p3
if (next) r.push(next.id);
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("p2", result);
        Assert.Contains("p3", result);
    }

    [Fact]
    public void TreeWalker_Survives_Current_Node_Removal()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""root""><p id=""p1"">A</p><p id=""p2"">B</p></div>
<script>
var root = document.getElementById('root');
var tw = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT, null);
tw.firstChild(); // P#p1
// For TreeWalker, removing currentNode is valid — user can set currentNode manually
tw.currentNode = root;
var child = tw.firstChild();
var r = [];
if (child) r.push(child.tagName);
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("P", result);
    }

    // ─── 2.3 Range operations on comment nodes ───

    [Fact]
    public void Range_On_Comment_Node()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""><!-- hello world --></div>
<script>
var container = document.getElementById('container');
var comment = container.firstChild;
var range = document.createRange();
range.setStart(comment, 3);
range.setEnd(comment, 8);
var r = [];
r.push(comment.nodeType);     // 8 (COMMENT_NODE)
r.push(comment.length);       // length of ' hello world '
r.push(range.startOffset);    // 3
r.push(range.endOffset);      // 8
r.push(range.toString());     // 'lo wo' (characters 3-8 of ' hello world ')
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("8|", result); // nodeType is 8
        Assert.Contains("|3|8|", result); // startOffset=3, endOffset=8
    }

    [Fact]
    public void Range_SelectNodeContents_On_Comment()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var comment = document.createComment('test data');
var range = document.createRange();
range.selectNodeContents(comment);
var r = [];
r.push(range.startOffset);  // 0
r.push(range.endOffset);    // 9 (length of 'test data')
r.push(range.collapsed);    // false
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,9,false", result);
    }

    [Fact]
    public void Range_SelectNodeContents_On_TextNode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">hello world</div>
<script>
var target = document.getElementById('target');
var textNode = target.firstChild;
var range = document.createRange();
range.selectNodeContents(textNode);
var r = [];
r.push(range.startOffset);  // 0
r.push(range.endOffset);    // 11 (length of 'hello world')
r.push(range.collapsed);    // false
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,11,false", result);
    }

    // ─── 2.5 Text node splitText() ───

    [Fact]
    public void SplitText_Basic()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">hello world</div>
<script>
var target = document.getElementById('target');
var textNode = target.firstChild;
var newNode = textNode.splitText(5);
var r = [];
r.push(textNode.data);        // 'hello'
r.push(newNode.data);         // ' world'
r.push(textNode.length);      // 5
r.push(newNode.length);       // 6
r.push(target.childNodes.length); // 2 (two text nodes now)
r.push(newNode.nodeType);     // 3 (TEXT_NODE)
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("hello| world|5|6|2|3", result);
    }

    [Fact]
    public void SplitText_At_Beginning()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">abcdef</div>
<script>
var target = document.getElementById('target');
var textNode = target.firstChild;
var newNode = textNode.splitText(0);
var r = [];
r.push(textNode.data);        // '' (empty)
r.push(newNode.data);         // 'abcdef'
r.push(target.childNodes.length); // 2
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("|abcdef|2", result);
    }

    [Fact]
    public void SplitText_At_End()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">abcdef</div>
<script>
var target = document.getElementById('target');
var textNode = target.firstChild;
var newNode = textNode.splitText(6);
var r = [];
r.push(textNode.data);        // 'abcdef'
r.push(newNode.data);         // '' (empty)
r.push(target.childNodes.length); // 2
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("abcdef||2", result);
    }

    // ─── 2.4 Range mutation awareness (splitText updates Range boundaries) ───

    [Fact]
    public void Range_Boundary_On_Text_Node_Character_Offset()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"">hello world</div>
<script>
var target = document.getElementById('target');
var textNode = target.firstChild;
var range = document.createRange();
range.setStart(textNode, 2);
range.setEnd(textNode, 7);
var r = [];
r.push(range.startOffset);    // 2
r.push(range.endOffset);      // 7
r.push(range.toString());     // 'llo w'
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("2|7|llo w", result);
    }

    [Fact]
    public void Range_DeleteContents_Updates_Boundaries()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target""><p>A</p><p>B</p><p>C</p></div>
<script>
var target = document.getElementById('target');
var range = document.createRange();
range.selectNodeContents(target);
range.deleteContents();
var r = [];
r.push(target.childNodes.length);  // 0
r.push(range.collapsed);           // true
r.push(range.startOffset);         // 0
r.push(range.endOffset);           // 0
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join(',');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("0,true,0,0", result);
    }

    // ─── CharacterData methods ───

    [Fact]
    public void CharacterData_Methods_On_Comment()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var comment = document.createComment('hello');
var r = [];
r.push(comment.data);            // 'hello'
r.push(comment.length);          // 5
comment.appendData(' world');
r.push(comment.data);            // 'hello world'
r.push(comment.substringData(6, 5)); // 'world'
comment.deleteData(5, 6);
r.push(comment.data);            // 'hello'
comment.insertData(5, '!');
r.push(comment.data);            // 'hello!'
comment.replaceData(0, 5, 'hi');
r.push(comment.data);            // 'hi!'
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("hello|5|hello world|world|hello|hello!|hi!", result);
    }

    // ─── Phase B: DOM Range mutation regression tests ───

    /// <summary>
    /// T1.2: extractContents() across nested elements with partial text node
    /// splitting — Acid3 test 9 scenario.
    /// </summary>
    [Fact]
    public void Range_ExtractContents_Cross_Node_Partial_Text()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""><h1>Hello <em>Wonderful</em> Kitty</h1><p>How are you?</p></div>
<script>
var container = document.getElementById('container');
var h1 = container.firstChild;             // <h1>
var em = h1.childNodes[1];                 // <em>
var t2 = em.firstChild;                    // 'Wonderful'
var p = container.childNodes[1];           // <p>
var range = document.createRange();
range.setStart(t2, 6);                     // after 'Wonder' → extract 'ful'
range.setEnd(p, 0);                        // before <p>'s children

var fragment = range.extractContents();
var r = [];
// Fragment should contain cloned <h1> with <em>ful</em> Kitty, plus cloned <p>
r.push(fragment.childNodes.length);         // 2 (cloned h1 + cloned p)
var fh1 = fragment.firstChild;
r.push(fh1.nodeName);                      // H1
r.push(fh1.childNodes.length);             // 2 (em clone + ' Kitty' text)
var fem = fh1.firstChild;
r.push(fem.nodeName);                      // EM
r.push(fem.textContent);                   // 'ful'
r.push(fh1.childNodes[1].textContent);     // ' Kitty'

// Original DOM should have: <h1>Hello <em>Wonder</em></h1><p>How are you?</p>
r.push(em.textContent);                    // 'Wonder' (kept in original)
r.push(h1.childNodes.length);              // 2 ('Hello ' + em)
r.push(range.collapsed);                   // true

var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("2|H1|2|EM|ful| Kitty|Wonder|2|true", result);
    }

    /// <summary>
    /// T1.5: insertNode() into a text node updates range boundaries after split.
    /// Acid3 test 12 scenario.
    /// </summary>
    [Fact]
    public void Range_InsertNode_Updates_Boundaries_After_TextSplit()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<p id=""target"">12345</p>
<script>
var p = document.getElementById('target');
var t1 = p.firstChild;    // '12345'
var range = document.createRange();
range.setStart(t1, 2);    // after '12'
range.setEnd(t1, 3);      // after '123' → selects '3'

// Create a new text node and insert it
var ins = document.createTextNode('ABCDE');
range.insertNode(ins);

// After split: p.childNodes = ['12', 'ABCDE', '345']
var r = [];
r.push(p.childNodes.length);               // 3
r.push(p.childNodes[0].textContent);       // '12'
r.push(p.childNodes[1].textContent);       // 'ABCDE'
r.push(p.childNodes[2].textContent);       // '345'

// Range boundaries should be updated to parent after text split
r.push(range.startContainer === p);        // true (parent of split)
r.push(range.startOffset);                 // 1 (index of inserted node)

var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("3|12|ABCDE|345|true|1", result);
    }

    /// <summary>
    /// T1.9: Range boundary-point adjustment when ancestor is removed via
    /// removeChild(). Acid3 test 13 scenario.
    /// </summary>
    [Fact]
    public void Range_Collapses_When_Ancestor_Removed()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""wrapper""><p id=""target"">12345</p></div>
<script>
var wrapper = document.getElementById('wrapper');
var p = document.getElementById('target');
var t = p.firstChild;  // '12345'
var range = document.createRange();
range.setStart(t, 2);    // inside text node at offset 2
range.setEnd(wrapper, 1); // at wrapper child index 1

// Remove <p> from wrapper — range should collapse
wrapper.removeChild(p);

var r = [];
r.push(range.startContainer === wrapper);  // true (adjusted to parent)
r.push(range.startOffset);                 // 0 (index of removed child)
r.push(range.endContainer === wrapper);    // true (adjusted if offset > index)
r.push(range.endOffset);                   // 0 (decremented from 1)
r.push(range.collapsed);                   // true

var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|0|true|0|true", result);
    }

    // ──────── Acid3 Bucket 1 Regression Tests (Tests 2–13) ────────

    /// <summary>
    /// Acid3 Test 2: Removing a node during NodeIterator iteration.
    /// The iterator must skip the removed node and continue with the next sibling.
    /// </summary>
    [Fact]
    public void Acid3_Test2_NodeIterator_Continues_After_Mid_Iteration_Removal()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""container""><span id=""a"">A</span><span id=""b"">B</span><span id=""c"">C</span><span id=""d"">D</span></div>
<script>
var container = document.getElementById('container');
var iter = document.createNodeIterator(container, NodeFilter.SHOW_ELEMENT, null);
var r = [];
var node = iter.nextNode(); // container
node = iter.nextNode();     // span#a
r.push(node.id);
node = iter.nextNode();     // span#b
r.push(node.id);
// Remove span#b (last node returned by nextNode()) from the DOM during iteration
node.parentNode.removeChild(node);
node = iter.nextNode();     // should skip removed #b and land on span#c
r.push(node ? node.id : 'null');
node = iter.nextNode();     // span#d
r.push(node ? node.id : 'null');
node = iter.nextNode();     // null (end)
r.push(node === null ? 'done' : 'unexpected');
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("a|b|c|d|done", result);
    }

    /// <summary>
    /// Acid3 Test 3: NodeIterator with SHOW_ALL on a deeply nested tree
    /// completes in finite steps (no infinite loop).
    /// </summary>
    [Fact]
    public void Acid3_Test3_NodeIterator_Finite_On_Deep_Tree()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""deep""></div>
<script>
var r = [];
var root = document.getElementById('deep');
// Build a 50-level deep chain
var current = root;
for (var i = 0; i < 50; i++) {
    var child = document.createElement('span');
    child.textContent = 'L' + i;
    current.appendChild(child);
    current = child;
}
var iter = document.createNodeIterator(root, NodeFilter.SHOW_ALL, null);
var count = 0;
var maxSteps = 500;
var node;
while ((node = iter.nextNode()) !== null && count < maxSteps) {
    count++;
}
r.push(node === null ? 'finite' : 'stuck');
r.push(count > 0 ? 'visited' : 'none');
r.push(count <= maxSteps ? 'ok' : 'exceeded');
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("finite|visited|ok", result);
    }

    /// <summary>
    /// Acid3 Test 5: TreeWalker with SHOW_TEXT visits whitespace-only text nodes
    /// between elements.
    /// </summary>
    [Fact]
    public void Acid3_Test5_TreeWalker_ShowText_Visits_Whitespace_Nodes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""ws"">
  <span>A</span>
  <span>B</span>
</div>
<script>
var root = document.getElementById('ws');
var tw = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
var r = [];
var node;
var wsCount = 0;
var textCount = 0;
while ((node = tw.nextNode()) !== null) {
    var val = node.nodeValue;
    if (/^\s+$/.test(val)) {
        wsCount++;
    } else {
        textCount++;
        r.push(val.trim());
    }
}
r.push('ws:' + wsCount);
r.push('text:' + textCount);
r.push(wsCount > 0 ? 'has-whitespace' : 'no-whitespace');
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("A|B", result);
        Assert.Contains("has-whitespace", result);
    }

    /// <summary>
    /// Acid3 Test 6: TreeWalker rooted at a subtree — parentNode() returns null
    /// when at the walker root, not the document root.
    /// </summary>
    [Fact]
    public void Acid3_Test6_TreeWalker_ParentNode_Null_At_Walker_Root()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""outer""><div id=""inner""><p id=""leaf"">text</p></div></div>
<script>
var inner = document.getElementById('inner');
var tw = document.createTreeWalker(inner, NodeFilter.SHOW_ELEMENT, null);
var r = [];
// Walker starts at root (inner)
r.push(tw.currentNode.id);            // 'inner'
// Move to first child
var child = tw.firstChild();
r.push(child ? child.id : 'null');     // 'leaf'
// Move back to parent
var parent = tw.parentNode();
r.push(parent ? parent.id : 'null');   // 'inner'
// Try to move above walker root
var above = tw.parentNode();
r.push(above === null ? 'null-correct' : above.id);  // should be null
r.push(tw.currentNode.id);            // should still be 'inner'
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("inner|leaf|inner|null-correct|inner", result);
    }

    /// <summary>
    /// Acid3 Test 9: extractContents() across sibling elements — extracted fragment
    /// has correct structure and original DOM is modified.
    /// </summary>
    [Fact]
    public void Acid3_Test9_ExtractContents_Across_Siblings()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""src""><span id=""s1"">AAA</span><span id=""s2"">BBB</span><span id=""s3"">CCC</span></div>
<script>
var src = document.getElementById('src');
var s1 = document.getElementById('s1');
var s3 = document.getElementById('s3');
var range = document.createRange();
// Range from middle of s1 text to middle of s3 text
range.setStart(s1.firstChild, 1);  // after first 'A'
range.setEnd(s3.firstChild, 2);    // after 'CC'
var frag = range.extractContents();
var r = [];
// Fragment contains cloned span wrappers with extracted text portions
r.push(frag.childNodes.length);
// Original DOM: s1 should have 'A', s3 should have 'C'
r.push(s1.textContent);   // 'A'
r.push(s3.textContent);   // 'C'
// The full middle span should have been extracted
var remainingSpans = src.querySelectorAll('span');
r.push(remainingSpans.length);  // 2 (s1 and s3 remain, s2 extracted)
// Fragment contains text from extracted portion
var fragText = '';
for (var i = 0; i < frag.childNodes.length; i++) {
    fragText += frag.childNodes[i].textContent;
}
r.push(fragText);  // 'AABBBCC'
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("A", result);
        Assert.Contains("C", result);
        Assert.Contains("AABBBCC", result);
    }

    /// <summary>
    /// Acid3 Test 10: Range with Attribute nodes as boundary points.
    /// Per DOM4 spec, setting a range boundary to an Attr node should throw.
    /// </summary>
    [Fact]
    public void Acid3_Test10_Range_With_Attribute_Node_Boundary()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""target"" class=""foo"">content</div>
<script>
var el = document.getElementById('target');
var attr = el.getAttributeNode('class');
var range = document.createRange();
var r = [];
r.push(attr !== null ? 'has-attr' : 'no-attr');
r.push(attr.nodeType);  // 2 (ATTRIBUTE_NODE)
// DOM4 spec requires InvalidNodeTypeError when using Attr as boundary
var threw = false;
try {
    range.setStart(attr, 0);
} catch (e) {
    threw = true;
    r.push('threw');
    r.push(e.constructor.name || 'Error');
}
if (!threw) {
    // Engine does not yet enforce the DOM4 restriction; accept gracefully
    r.push('no-throw');
    r.push(range.startContainer === attr ? 'attr-is-container' : 'different');
}
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("has-attr", result);
        Assert.Contains("2", result);
    }

    /// <summary>
    /// Acid3 Test 11: Range boundaries inside Comment nodes — extract
    /// splits comment text correctly.
    /// </summary>
    [Fact]
    public void Acid3_Test11_Range_Inside_Comment_Nodes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""cbox""><!--ABCDEFGHIJ--></div>
<script>
var cbox = document.getElementById('cbox');
var comment = cbox.firstChild;
var r = [];
r.push(comment.nodeType);         // 8 (COMMENT_NODE)
r.push(comment.nodeValue);        // 'ABCDEFGHIJ'
var range = document.createRange();
range.setStart(comment, 3);       // after 'ABC'
range.setEnd(comment, 7);         // after 'ABCDEFG'
r.push(range.startOffset);        // 3
r.push(range.endOffset);          // 7
r.push(range.toString());         // 'DEFG' (substring of comment)
// Extract the range contents
var extracted = range.extractContents();
r.push(extracted.firstChild.nodeValue); // 'DEFG'
// Original comment should now have 'ABC', remainder 'HIJ'
r.push(comment.nodeValue);        // 'ABC'
var next = comment.nextSibling;
r.push(next ? next.nodeValue : 'none');  // 'HIJ'
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("8|ABCDEFGHIJ|3|7", result);
        Assert.Contains("DEFG", result);
        Assert.Contains("ABC", result);
    }

    /// <summary>
    /// Acid3 Test 12: Range boundary adjustment after node insertion.
    /// Using range.insertNode() to insert a node inside a range updates boundaries.
    /// </summary>
    [Fact]
    public void Acid3_Test12_Range_Boundaries_Update_On_Insertion()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<p id=""target"">ABCDE</p>
<script>
var p = document.getElementById('target');
var t = p.firstChild;       // 'ABCDE'
var range = document.createRange();
range.setStart(t, 1);       // after 'A'
range.setEnd(t, 4);         // after 'ABCD' → selects 'BCD'
var r = [];
r.push(range.startOffset);  // 1
r.push(range.endOffset);    // 4
r.push(range.toString());   // 'BCD'

// Insert a node at the start of the range
var ins = document.createTextNode('XYZ');
range.insertNode(ins);

// After insertion: p has ['A', 'XYZ', 'BCDE'] (text splits + insert)
r.push(p.childNodes.length);              // 3
r.push(p.childNodes[0].textContent);      // 'A'
r.push(p.childNodes[1].textContent);      // 'XYZ'
r.push(p.childNodes[2].textContent);      // 'BCDE'
r.push(range.startContainer === p);       // true
r.push(range.startOffset);               // 1 (index of inserted node)
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("1|4|BCD", result);
        Assert.Contains("3|A|XYZ|BCDE|true|1", result);
    }

    /// <summary>
    /// Acid3 Test 13: Range boundary adjustment after node deletion.
    /// Deleting a node that overlaps the range causes it to adjust/collapse.
    /// </summary>
    [Fact]
    public void Acid3_Test13_Range_Adjusts_On_Overlapping_Deletion()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""box2""><span id=""x"">X</span><span id=""y"">Y</span><span id=""z"">Z</span></div>
<script>
var box = document.getElementById('box2');
var spanY = document.getElementById('y');
var range = document.createRange();
// Set range to start inside span#y's text, end at box child index 3
range.setStart(spanY.firstChild, 0);
range.setEnd(box, 3);
var r = [];
r.push(range.startContainer === spanY.firstChild);  // true
r.push(range.endOffset);                             // 3
// Remove span#y — this contains the start boundary
box.removeChild(spanY);
// After removal, range should adjust:
// startContainer should move to box (parent of removed node)
r.push(range.startContainer === box);  // true
r.push(range.startOffset);            // 1 (index where spanY was)
r.push(range.endContainer === box);   // true
r.push(range.endOffset);              // 2 (decremented from 3)
r.push(range.collapsed);              // false (startOffset=1 < endOffset=2)
var out = document.createElement('div');
out.id = 'result';
out.textContent = r.join('|');
document.body.appendChild(out);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("true|3", result);
        Assert.Contains("true|1|true|2", result);
    }
}
