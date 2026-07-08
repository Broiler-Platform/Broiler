using Broiler.Dom;
using Broiler.Dom.Html;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the iterative (explicit-stack) rewrite of
/// <see cref="HtmlSerializer.Append{TNode}"/> — issue #1302's
/// shadow-dom/build-deep-detached-shadow-then-append-text.html crashed the
/// render with "Maximum HTML serialization depth (1024) exceeded" because the
/// serializer recursed once per DOM level. The rewrite must (a) produce exactly
/// the same output as before for ordinary trees, including correctly ordered
/// close tags, and (b) serialize a very deep chain without throwing or
/// overflowing the stack.
/// </summary>
public sealed class HtmlSerializerIterativeTests
{
    [Fact]
    public void Serialize_ProducesCorrectlyNestedAndOrderedOutput()
    {
        var doc = new DomDocument();
        var root = doc.CreateElement("div");
        root.SetAttribute("id", "a");
        var p = doc.CreateElement("p");
        p.AppendChild(doc.CreateTextNode("Hello "));
        var b = doc.CreateElement("b");
        b.AppendChild(doc.CreateTextNode("bold"));
        p.AppendChild(b);
        root.AppendChild(p);
        var img = doc.CreateElement("img");
        img.SetAttribute("src", "p.png");
        root.AppendChild(img);
        root.AppendChild(doc.CreateElement("span"));

        var html = HtmlSerializer.Serialize(root);

        Assert.Equal(
            "<div id=\"a\"><p>Hello <b>bold</b></p><img src=\"p.png\"><span></span></div>",
            html);
    }

    [Fact]
    public void Serialize_DeepChain_DoesNotThrowOrOverflow()
    {
        var doc = new DomDocument();
        var root = doc.CreateElement("div");
        var current = root;
        // ~2000 levels: past the old 1024 recursion cap and comparable to the WPT
        // shadow-dom deep-detached test that triggered the crash.
        for (var i = 0; i < 2000; i++)
        {
            var child = doc.CreateElement("div");
            current.AppendChild(child);
            current = child;
        }
        current.AppendChild(doc.CreateTextNode("deep"));

        // Explicit high cap so the assertion holds against both the recursive
        // baseline (pre-patch pointer) and the iterative rewrite: the point is
        // that ~2000 levels serialize without throwing or overflowing.
        var html = HtmlSerializer.Serialize(
            root,
            new HtmlSerializationOptions(MaximumDepth: 100_000));

        Assert.StartsWith("<div><div>", html);
        Assert.EndsWith("</div></div>", html);
        Assert.Contains("deep", html);
        // Balanced: one open and one close per level plus the root.
        Assert.Equal(2001, System.Text.RegularExpressions.Regex.Matches(html, "<div>").Count);
        Assert.Equal(2001, System.Text.RegularExpressions.Regex.Matches(html, "</div>").Count);
    }
}
