using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// <c>element.attributes</c> is a live <c>NamedNodeMap</c> (DOM §4.9.1) whose <c>Attr</c> nodes are
/// one object per attribute with a live value.
/// </summary>
/// <remarks>
/// <para>
/// It was a fresh plain object per read, with the same faults the document collections had before
/// they moved onto the shared collection machinery: no interface — <c>constructor.name</c> was
/// <c>"Object"</c> and the bare name <c>NamedNodeMap</c> a <c>ReferenceError</c>, which aborts the
/// script that names it — no identity, and no named access.
/// </para>
/// <para>
/// The fourth fault was the one that threw rather than answering wrongly: <c>length</c> was live
/// while the indices were materialized once, so a map held across a <c>setAttribute</c> reported the
/// new count with nothing at the new index and the idiomatic
/// <c>for (i = 0; i &lt; m.length; i++) m[i].name</c> read <c>undefined.name</c>.
/// </para>
/// <para>Every expectation is Chromium's measured answer over the same probe run against both.</para>
/// </remarks>
public sealed class NamedNodeMapTests
{
    private static DomBridge Attach(out JSContext context)
    {
        context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(
            context,
            "<!DOCTYPE html><html><body><div id=\"a\" class=\"c\" title=\"t\"></div></body></html>",
            "https://example.com/index.html");
        return bridge;
    }

    private static string Eval(JSContext context, string body) =>
        context.Eval($$"""
            (() => {
                var el = document.getElementById('a');
                {{body}}
            })()
            """).ToString();

    [Fact(Timeout = 600000)]
    public void Attributes_Is_A_NamedNodeMap_With_A_Real_Prototype()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("NamedNodeMap|true|function|false", Eval(context, """
            return el.attributes.constructor.name + '|' +
                   (Object.getPrototypeOf(el.attributes) === NamedNodeMap.prototype) + '|' +
                   (typeof NamedNodeMap.prototype.item) + '|' +
                   Object.prototype.hasOwnProperty.call(el.attributes, 'item');
            """));
    }

    /// <summary>
    /// Every member Web IDL declares is on the prototype and shared — including the five that need
    /// the owning element and so are host functions rather than JavaScript.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Every_Declared_Member_Is_On_The_Prototype()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("function,function,function,function,function,function,function", Eval(context, """
            return ['item', 'getNamedItem', 'getNamedItemNS', 'setNamedItem', 'setNamedItemNS',
                    'removeNamedItem', 'removeNamedItemNS']
                .map(function (m) { return typeof NamedNodeMap.prototype[m]; }).join(',');
            """));
    }

    /// <summary>Calling a host-backed member on something that is not a map is a TypeError, not a
    /// silent wrong answer.</summary>
    [Fact(Timeout = 600000)]
    public void A_Host_Member_Called_On_A_Foreign_Object_Throws()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("threw", Eval(context, """
            try { NamedNodeMap.prototype.removeNamedItem.call({}, 'id'); return 'no throw'; }
            catch (e) { return 'threw'; }
            """));
    }

    [Fact(Timeout = 600000)]
    public void The_Map_Has_Stable_Identity()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true", Eval(context, "return String(el.attributes === el.attributes);"));
    }

    /// <summary>
    /// Indices and <c>length</c> are both live, from one contents function. The second half is what
    /// used to break: an index added after the map was taken did not exist, so the loop threw.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Map_Is_Live_In_Both_Length_And_Indices()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("3->5|id,class,title,data-one,data-two|id,class,title", Eval(context, """
            var m = el.attributes;
            var before = m.length;
            el.setAttribute('data-one', '1');
            el.setAttribute('data-two', '2');
            function names() {
                var n = [];
                for (var i = 0; i < m.length; i++) n.push(m[i] ? m[i].name : 'MISSING');
                return n.join(',');
            }
            var grownLength = m.length;
            var grown = names();
            el.removeAttribute('data-one');
            el.removeAttribute('data-two');
            return before + '->' + grownLength + '|' + grown + '|' + names();
            """));
    }

    /// <summary>
    /// The qualified-name getter DOM §4.9.1 declares, and its <c>getNamedItem</c> spelling. Neither
    /// existed: <c>el.attributes.id</c> was <c>undefined</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Attribute_Is_Reachable_By_Its_Qualified_Name()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("id=a|class|undefined|a|null", Eval(context, """
            var byName = el.attributes.id;
            return byName.name + '=' + byName.value + '|' +
                   el.attributes['class'].name + '|' +
                   String(el.attributes.nope) + '|' +
                   el.attributes.getNamedItem('id').value + '|' +
                   String(el.attributes.getNamedItem('nope'));
            """));
    }

    /// <summary>
    /// An interface member is never shadowed by an attribute of the same name — Web IDL consults
    /// named properties only after the object and its prototype chain.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Attribute_Named_Like_A_Member_Does_Not_Shadow_It()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("number|function|true", Eval(context, """
            el.setAttribute('length', 'x');
            el.setAttribute('item', 'y');
            return (typeof el.attributes.length) + '|' + (typeof el.attributes.item) + '|' +
                   (el.attributes.getNamedItem('length').value === 'x');
            """));
    }

    /// <summary>
    /// Only the indices are own enumerable properties, as on a browser's map — the methods used to
    /// be own properties, so <c>Object.keys</c> listed the whole interface.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Only_The_Indices_Are_Own_Properties()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("0,1,2|id,class,title", Eval(context, """
            return Object.keys(el.attributes).join(',') + '|' +
                   Array.prototype.slice.call(el.attributes).map(function (x) { return x.name; }).join(',');
            """));
    }

    /// <summary>
    /// <c>item()</c> out of range is <c>null</c> while the bare index is <c>undefined</c> — the two
    /// are distinguishable and DOM specifies each separately.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Out_Of_Range_Answers_Null_From_Item_And_Undefined_From_An_Index()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("id|null|undefined", Eval(context, """
            return el.attributes.item(0).name + '|' + String(el.attributes.item(99)) + '|' +
                   String(el.attributes[99]);
            """));
    }

    // ---------------- Attr nodes ----------------

    /// <summary>
    /// One <c>Attr</c> object per attribute, whichever way it is reached. Without this the map's own
    /// identity would mean little: every read of the live contents would mint fresh nodes.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Attribute_Is_One_Node_Across_Every_Access_Path()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true|true|true|true|false", Eval(context, """
            var other = document.createElement('div');
            other.setAttribute('id', 'b');
            return (el.attributes[0] === el.attributes[0]) + '|' +
                   (el.attributes.id === el.attributes[0]) + '|' +
                   (el.attributes.getNamedItem('id') === el.attributes.id) + '|' +
                   (el.getAttributeNode('id') === el.attributes.id) + '|' +
                   (other.attributes.id === el.attributes.id);
            """));
    }

    /// <summary>
    /// An attached <c>Attr</c>'s value tracks the element, and writing it writes back — a browser
    /// treats <c>attr.value = 'x'</c> as another spelling of <c>setAttribute</c>.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Attached_Attr_Value_Is_Live_In_Both_Directions()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("z|z|written|written", Eval(context, """
            var at = el.getAttributeNode('id');
            el.setAttribute('id', 'z');
            var read = at.value + '|' + at.nodeValue;
            at.value = 'written';
            return read + '|' + el.getAttribute('id') + '|' + at.value;
            """));
    }

    /// <summary>
    /// Removing an attribute detaches its node: it keeps the value it had, loses its
    /// <c>ownerElement</c>, and re-adding the attribute mints a new node rather than reviving it.
    /// The difference is observable — the two nodes report the old and the new value.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Removing_An_Attribute_Detaches_Its_Node()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("null|1|false|1|2", Eval(context, """
            el.setAttribute('data-tmp', '1');
            var first = el.getAttributeNode('data-tmp');
            el.removeAttribute('data-tmp');
            var detached = String(first.ownerElement) + '|' + first.value;
            el.setAttribute('data-tmp', '2');
            var second = el.getAttributeNode('data-tmp');
            return detached + '|' + (first === second) + '|' + first.value + '|' + second.value;
            """));
    }

    /// <summary>
    /// <c>setAttributeNode</c> distinguishes re-setting the element's own node from replacing it with
    /// a different one (DOM §4.9.2): the first returns that node live, the second detaches the
    /// displaced one.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void SetAttributeNode_Returns_The_Same_Node_Or_Detaches_The_Displaced_One()
    {
        using var bridge = Attach(out var context);

        Assert.Equal("true|new|new", Eval(context, """
            var at = el.getAttributeNode('class');
            at.value = 'new';
            var old = el.setAttributeNode(at);
            return (old === at) + '|' + old.value + '|' + el.getAttribute('class');
            """));

        Assert.Equal("false|c2|null|brand", Eval(context, """
            var target = document.createElement('div');
            target.setAttribute('class', 'c2');
            var displaced = target.getAttributeNode('class');
            var fresh = document.createAttribute('class');
            fresh.value = 'brand';
            var returned = target.setAttributeNode(fresh);
            return (returned === fresh) + '|' + returned.value + '|' +
                   String(returned.ownerElement) + '|' + target.getAttribute('class');
            """));
    }

    /// <summary>
    /// The interface's members are enumerable, as Web IDL declares and a browser has. They were
    /// non-enumerable on every collection here, so <c>for...in</c> yielded only indices.
    /// </summary>
    /// <remarks>
    /// <c>length</c> is the one member still missing from the enumeration: it is answered by the host
    /// rather than held as a prototype accessor, so there is no property for <c>for...in</c> to find.
    /// Recorded rather than papered over.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void The_Interface_Members_Are_Enumerable()
    {
        using var bridge = Attach(out var context);

        Assert.Equal(
            "0,1,2,item,getNamedItem,getNamedItemNS,setNamedItem,setNamedItemNS,removeNamedItem,removeNamedItemNS",
            Eval(context, """
                var k = [];
                for (var n in el.attributes) k.push(n);
                return k.join(',');
                """));
    }
}
