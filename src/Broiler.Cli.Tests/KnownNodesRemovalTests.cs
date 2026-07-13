using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 4 first slice (P4.1): the
/// <c>_knownNodes</c> parallel node set is removed. It was a tree-derived <see cref="HashSet{T}"/>
/// populated on every node construction (createElement / cloneNode / createComment / parse /
/// sub-document build) but had no behaviour-affecting reader left — its last real consumer was
/// already replaced by tree-order traversal, and its only surviving lookup was a redundant
/// idempotent-<c>Add</c> guard on a <c>HashSet</c>. Phase 4 makes the canonical Broiler.Dom tree the
/// only authority for tree membership, so the parallel set is deleted. These characterizations
/// exercise every construction path that used to feed the set and assert the DOM still behaves; the
/// guard test asserts the field is gone.
/// </summary>
public sealed class KnownNodesRemovalTests
{
    [Fact]
    public void DomBridge_Has_No_KnownNodes_Parallel_Set()
    {
        // The parallel node-tracking set must not be reintroduced: the canonical tree is the single
        // authority for node membership.
        var field = typeof(DomBridge).GetField("_knownNodes", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Null(field);
    }

    [Fact]
    public void Element_Creation_And_Insertion_Round_Trip()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"host\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var host = document.getElementById('host');
                var span = document.createElement('span');
                span.id = 'child';
                span.textContent = 'hi';
                host.appendChild(span);
                var found = document.getElementById('child');
                return found.textContent + '|' + (found.parentNode === host) + '|' + host.children.length;
            })()
            """);

        Assert.Equal("hi|true|1", result.ToString());
    }

    [Fact]
    public void CloneNode_Comment_And_Fragment_Construction_Round_Trip()
    {
        const string html = "<!DOCTYPE html><html><body><ul id=\"list\"><li class=\"row\">a</li></ul></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var list = document.getElementById('list');
                var row = list.querySelector('.row');
                var clone = row.cloneNode(true);
                clone.textContent = 'b';
                list.appendChild(clone);

                var frag = document.createDocumentFragment();
                var extra = document.createElement('li');
                extra.textContent = 'c';
                frag.appendChild(extra);
                list.appendChild(frag);

                var comment = document.createComment('note');
                list.appendChild(comment);

                var texts = [];
                for (var i = 0; i < list.children.length; i++) texts.push(list.children[i].textContent);
                return texts.join(',') + '|' + list.childNodes.length + '|' + comment.nodeType;
            })()
            """);

        // 3 <li> children (a, b, c); childNodes count includes the trailing comment (4).
        Assert.Equal("a,b,c|4|8", result.ToString());
    }

    [Fact]
    public void InnerHtml_Parse_Replace_Round_Trip()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"box\"></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "https://example.com/index.html");

        var result = context.Eval("""
            (() => {
                var box = document.getElementById('box');
                box.innerHTML = '<p class="p">one</p><p class="p">two</p>';
                var ps = box.querySelectorAll('.p');
                return ps.length + '|' + ps[0].textContent + '|' + ps[1].textContent;
            })()
            """);

        Assert.Equal("2|one|two", result.ToString());
    }
}
