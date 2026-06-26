using System;
using Broiler.Dom.Html;
using Broiler.HTML.Dom;
using Broiler.HTML.Dom.Parse;
using Broiler.HTML.Orchestration.Parse;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 5 renderer-cutover scaffold (slice 1). Verifies that the shared
/// Broiler.CSS.Dom engine can compute a style from the canonical DomDocument and
/// project it onto the renderer's CssBox tree via the new
/// <c>SharedRendererCascade</c>. This exercises the dual-run path directly (the
/// pipeline keeps it off by default until pixel parity is verified).
/// </summary>
public sealed class Phase5SharedCascadeTests
{
    [Fact]
    public void SharedCascade_Projects_Engine_Computed_Style_Onto_Box()
    {
        const string html = @"<!DOCTYPE html><html><head>
<style>#t { display: block; }</style>
</head><body><span id=""t"">x</span></body></html>";

        var document = new HtmlDocumentParser().ParseDocument(html).Document;
        var root = HtmlParser.ParseDocument(document, new Uri("file:///t.html"));

        SharedRendererCascade.Apply(root, document, 800, 600);

        var target = FindById(root, "t");
        Assert.NotNull(target);
        // A <span> is display:inline by default; the author rule makes it block,
        // and the shared engine's computed value must land on the box.
        Assert.Equal("block", target!.Display);
    }

    [Fact]
    public void SharedCascade_Backfills_Initial_Display_For_Unmatched_Element()
    {
        const string html = @"<!DOCTYPE html><html><head>
<style>#t { display: block; }</style>
</head><body><span id=""t"">x</span><i id=""u"">y</i></body></html>";

        var document = new HtmlDocumentParser().ParseDocument(html).Document;
        var root = HtmlParser.ParseDocument(document, new Uri("file:///t.html"));

        SharedRendererCascade.Apply(root, document, 800, 600);

        // The unmatched <i> gets the CSS initial value for display (inline) from the
        // engine's backfill, projected onto the box.
        var unmatched = FindById(root, "u");
        Assert.NotNull(unmatched);
        Assert.Equal("inline", unmatched!.Display);
    }

    private static CssBox FindById(CssBox box, string id)
    {
        if (box.HtmlTag != null &&
            string.Equals(box.GetAttribute("id", string.Empty), id, StringComparison.Ordinal))
            return box;

        foreach (var child in box.Boxes)
        {
            var found = FindById(child, id);
            if (found != null)
                return found;
        }

        return null;
    }
}
