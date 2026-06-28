using System;
using Broiler.Dom.Html;
using Broiler.HTML.Dom;
using Broiler.Layout;
using Broiler.HTML.Dom.Parse;
using Broiler.HTML.Core;
using Broiler.HTML.Orchestration.Parse;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 5 renderer cutover. Verifies that the shared Broiler.CSS.Dom engine resolves an
/// element's cascade from the canonical DomDocument and that the renderer projection sets
/// the result on the CssBox — without the initial-value backfill that would clobber the
/// renderer's own defaults and adjustments. This is the renderer's sole cascade path.
/// </summary>
public sealed class Phase5SharedCascadeTests
{
    [Fact]
    public void GetCascadedStyle_Applies_Author_Rule()
    {
        const string html = @"<!DOCTYPE html><html><head>
<style>#t { display: block; }</style>
</head><body><span id=""t"">x</span></body></html>";

        var (engine, root) = Build(html);
        var target = FindById(root, "t")!;

        var cascaded = engine.GetCascadedStyle(target.SourceElement);
        // A <span> is display:inline by default; the author rule makes it block.
        Assert.Equal("block", cascaded["display"]);
    }

    [Fact]
    public void GetCascadedStyle_Uses_UserAgent_Sheet_For_Block_Elements()
    {
        const string html = @"<!DOCTYPE html><html><body><div id=""d"">x</div></body></html>";

        var (engine, root) = Build(html);
        var div = FindById(root, "d")!;

        // No author rule for the <div>; the UA sheet (div { display: block }) must cascade.
        Assert.Equal("block", engine.GetCascadedStyle(div.SourceElement)["display"]);
    }

    [Fact]
    public void GetCascadedStyle_Does_Not_Backfill_Initials_For_Unmatched_Property()
    {
        const string html = @"<!DOCTYPE html><html><head>
<style>#t { color: red; }</style>
</head><body><span id=""t"">x</span></body></html>";

        var (engine, root) = Build(html);
        var target = FindById(root, "t")!;
        var cascaded = engine.GetCascadedStyle(target.SourceElement);

        // color is declared; display is not (no UA rule for <span>, no author rule), so it
        // must be ABSENT rather than backfilled to the "inline" initial — the renderer
        // keeps its own default. This is the non-clobbering contract the cutover relies on.
        Assert.Equal("red", cascaded["color"]);
        Assert.False(cascaded.ContainsKey("display"));
    }

    [Fact]
    public void ProjectCascadedStyle_Sets_Box_Property()
    {
        const string html = @"<!DOCTYPE html><html><head>
<style>#t { display: block; }</style>
</head><body><span id=""t"">x</span></body></html>";

        var (engine, root) = Build(html);
        var target = FindById(root, "t")!;

        SharedRendererCascade.ProjectCascadedStyle(target, engine);
        Assert.Equal("block", target.Display);
    }

    private static (Broiler.CSS.Dom.CssStyleEngine Engine, CssBox Root) Build(string html)
    {
        var document = new HtmlDocumentParser().ParseDocument(html).Document;
        var root = HtmlParser.ParseDocument(document, new Uri("file:///t.html"));
        var styleStart = html.IndexOf("<style>", StringComparison.OrdinalIgnoreCase);
        var styleEnd = html.IndexOf("</style>", StringComparison.OrdinalIgnoreCase);
        var styleSet = styleStart >= 0 && styleEnd > styleStart
            ? HtmlStyleSet.Parse(html.Substring(styleStart + 7, styleEnd - styleStart - 7))
            : HtmlStyleSet.Default;
        var engine = SharedRendererCascade.BuildEngine(document, styleSet, 800, 600)!;
        return (engine, root);
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
