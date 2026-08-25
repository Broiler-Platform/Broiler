namespace Broiler.Cli.Tests;

/// <summary>
/// <c>NodeList</c> and <c>HTMLCollection</c> are real interfaces with real prototypes, and the
/// collections that return them are live or static as their specifications say (DOM §4.2.10,
/// §4.2.10.2, §4.2.6, §4.4, §4.5, §4.9).
/// </summary>
/// <remarks>
/// <para>
/// They used to be plain JavaScript arrays, which is wrong three ways. Neither interface was defined
/// at all, so <c>instanceof</c> was a <c>ReferenceError</c> and
/// <c>childNodes.constructor.name</c> answered <c>"Array"</c>. <c>item()</c> and <c>namedItem()</c>
/// did not exist while <c>map</c>, <c>filter</c> and <c>slice</c> did — the opposite of a browser in
/// both directions, so feature detection branched wrongly either way round. And an array is a
/// snapshot, so the <b>live</b> collections were not: <c>var kids = el.childNodes;
/// el.appendChild(x); kids.length</c> grew in a browser and did not here. That third one returns a
/// wrong number rather than an error, which is how it sat under passing tests.
/// </para>
/// <para>
/// <b>Every expectation is a Chromium answer</b>, taken through Playwright against the pinned
/// browser rather than from a reading of the specification — including the three-way liveness split
/// below, which is the assertion that would have been easiest to get subtly wrong.
/// </para>
/// </remarks>
public class DomCollectionInterfaceTests
{
    private static string Run(string body, string script)
    {
        var html = $"<!doctype html><html><body>{body}<div id=\"result\"></div>\n<script>\n{script}\n</script></body></html>";
        var serialized = CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
        const string start = "<div id=\"result\">";
        var open = serialized.IndexOf(start, StringComparison.Ordinal);
        Assert.True(open >= 0, $"probe did not run; document was:\n{serialized}");
        open += start.Length;
        var close = serialized.IndexOf("</div>", open, StringComparison.Ordinal);
        Assert.True(close > open, $"probe wrote nothing; document was:\n{serialized}");
        return serialized[open..close];
    }

    private const string TwoSpans = "<div id=\"box\"><span>a</span><span>b</span></div>";

    /// <summary>
    /// The interfaces exist and instances really are instances of them — through a real prototype
    /// chain, not an <c>@@hasInstance</c> hook over a foreign object, which is what roadmap track 6
    /// action 1 asks for.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Collections_Are_Instances_Of_Their_Interfaces()
    {
        var result = Run(TwoSpans, """
var cn = document.getElementById('box').childNodes;
var qsa = document.querySelectorAll('span');
var gtn = document.getElementsByTagName('span');
document.getElementById('result').textContent = [
  typeof NodeList, typeof HTMLCollection,
  cn instanceof NodeList, qsa instanceof NodeList, gtn instanceof HTMLCollection,
  cn.constructor.name, gtn.constructor.name,
  Object.getPrototypeOf(cn) === NodeList.prototype,
  Array.isArray(cn)
].join('|');
""");
        Assert.Equal("function|function|true|true|true|NodeList|HTMLCollection|true|false", result);
    }

    /// <summary>
    /// The liveness split, which is the part that silently changed results. <c>childNodes</c> and
    /// <c>getElementsByTagName</c> are live; <c>querySelectorAll</c> is static — the one collection
    /// the specification defines as a snapshot.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Live_Collections_Track_The_Tree_And_Static_Ones_Do_Not()
    {
        var result = Run(TwoSpans, """
var box = document.getElementById('box');
var cn = box.childNodes, qsa = document.querySelectorAll('span'), gtn = document.getElementsByTagName('span');
var before = cn.length + ',' + qsa.length + ',' + gtn.length;
box.appendChild(document.createElement('span'));
var after = cn.length + ',' + qsa.length + ',' + gtn.length;
// and back again: a live collection shrinks too, not just grows
box.removeChild(box.lastChild);
document.getElementById('result').textContent =
  before + ' then ' + after + ' then ' + (cn.length + ',' + qsa.length + ',' + gtn.length);
""");
        Assert.Equal("2,2,2 then 3,2,3 then 2,2,2", result);
    }

    /// <summary>
    /// A live collection's indexed access is live too, not merely its <c>length</c> — the element at
    /// an index is whatever is there now.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Indexed_Access_On_A_Live_Collection_Is_Live()
    {
        var result = Run(TwoSpans, """
var box = document.getElementById('box'), cn = box.childNodes;
var first = cn[0].textContent;
box.insertBefore(document.createElement('em'), box.firstChild);
document.getElementById('result').textContent =
  first + '|' + cn[0].tagName + '|' + cn[1].textContent + '|' + (cn[9] === undefined);
""");
        Assert.Equal("a|EM|a|true", result);
    }

    /// <summary>
    /// <c>item()</c> answers <c>null</c> out of range where indexed access answers <c>undefined</c>.
    /// The two are distinguishable and the specification gives each its own value.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Item_Answers_Null_Out_Of_Range()
    {
        var result = Run(TwoSpans, """
var cn = document.getElementById('box').childNodes;
document.getElementById('result').textContent = [
  cn.item(0) === cn[0], String(cn.item(9)), String(cn[9]), String(cn.item(-1))
].join('|');
""");
        Assert.Equal("true|null|undefined|null", result);
    }

    /// <summary>
    /// The Web IDL iteration surface, held on the prototype and shared — a page reading
    /// <c>NodeList.prototype.item</c> finds the same function the instance uses.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_NodeList_Iterates_And_Its_Methods_Live_On_The_Prototype()
    {
        var result = Run(TwoSpans, """
var cn = document.querySelectorAll('span');
var viaForEach = [];
cn.forEach(function (node, i) { viaForEach.push(i + ':' + node.textContent); });
var viaForOf = [];
for (var node of cn) viaForOf.push(node.textContent);
var viaSpread = [...cn].length;
var viaEntries = [];
var it = cn.entries(), step;
while (!(step = it.next()).done) viaEntries.push(step.value[0] + '=' + step.value[1].textContent);
document.getElementById('result').textContent = [
  viaForEach.join(','), viaForOf.join(','), viaSpread, viaEntries.join(','),
  cn.item === NodeList.prototype.item,
  Object.prototype.hasOwnProperty.call(cn, 'item')
].join('|');
""");
        Assert.Equal("0:a,1:b|a,b|2|0=a,1=b|true|false", result);
    }

    /// <summary>
    /// <c>HTMLCollection</c>'s named getter (DOM §4.2.10.2): by <c>id</c>, then by <c>name</c>, both
    /// through property access and through <c>namedItem</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_HtmlCollection_Answers_Its_Named_Getter()
    {
        var result = Run(
            "<div id=\"box\"><span id=\"first\">a</span><span name=\"second\">b</span></div>",
            """
var gtn = document.getElementById('box').getElementsByTagName('span');
document.getElementById('result').textContent = [
  gtn.namedItem('first').textContent,
  gtn.namedItem('second').textContent,
  gtn.first.textContent,
  String(gtn.namedItem('absent')),
  // A name that collides with a method must NOT shadow it: Web IDL consults named properties only
  // when the object and its prototype chain do not already answer.
  typeof gtn.item
].join('|');
""");
        Assert.Equal("a|b|a|null|function", result);
    }

    /// <summary>
    /// The array methods a browser does <em>not</em> put on a collection are absent, which is the
    /// half of this that feature detection reads. They were present because the collection was an
    /// array, so a page testing for them took the array branch against an object that only
    /// accidentally supported it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Array_Methods_A_Collection_Does_Not_Have_Are_Absent()
    {
        var result = Run(TwoSpans, """
var cn = document.getElementById('box').childNodes;
document.getElementById('result').textContent =
  ['map', 'filter', 'slice', 'indexOf', 'push'].map(function (m) { return typeof cn[m]; }).join('|') +
  '|' + Array.prototype.slice.call(cn).length;
""");
        // Still convertible with the idiom every page uses for exactly this reason.
        Assert.Equal("undefined|undefined|undefined|undefined|undefined|2", result);
    }

    /// <summary>Neither interface is constructible, as in a browser.</summary>
    [Fact(Timeout = 600000)]
    public void The_Interfaces_Are_Not_Constructible()
    {
        var result = Run("", """
function attempt(f) { try { f(); return 'no throw'; } catch (e) { return e.name; } }
document.getElementById('result').textContent =
  attempt(function () { new NodeList(); }) + '|' + attempt(function () { new HTMLCollection(); });
""");
        Assert.Equal("TypeError|TypeError", result);
    }
}
