using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for the render-serialization comment-collapse fix in
/// <c>DomBridge.ApplySerializationTransforms</c> (<c>RemoveRenderCommentNodes</c>).
///
/// An HTML comment between two siblings splits the surrounding text into two DOM
/// text nodes (<c>"\n&lt;!-- c --&gt;\n"</c> → <c>text("\n")</c>, comment,
/// <c>text("\n")</c>). The shared serializer used to re-emit the comment, so when
/// the canonical HTML was re-parsed for layout each whitespace run collapsed
/// independently and produced a spurious extra space between the boxes — plus an
/// uncollapsed leading space at the start of the block. That shifted every
/// following element to the right, the dominant cause of the comment-heavy
/// css-align WPT "MissingContent" pixel mismatches. Dropping comment nodes from
/// the render document lets the surrounding text re-parse as one node so the run
/// collapses correctly.
/// </summary>
public class CommentWhitespaceCollapseTests : IDisposable
{
    private readonly string _tempDir;

    public CommentWhitespaceCollapseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-comment-ws-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // Left/right extent of the blue run on a scan row.
    private static (int left, int right) BlueRun(BBitmap bmp, int y, int width)
    {
        int left = -1, right = -1;
        for (int x = 0; x < width; x++)
        {
            var p = bmp.GetPixel(x, y);
            if (p.B > 100 && p.R < 100 && p.G < 100)
            {
                if (left < 0) left = x;
                right = x;
            }
        }
        return (left, right);
    }

    private (int left, int right) RenderFirstBoxRun(string body)
    {
        var html = $@"<!DOCTYPE html>
<style>
  body {{ margin: 0; }}
  .c {{ display: inline-block; width: 50px; height: 30px; background: blue; }}
</style>
<body>{body}</body>";
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(400, 100);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);
        return BlueRun(bmp, y: 15, width: 400);
    }

    [Fact]
    public void LeadingComment_DoesNotProduceLeadingSpace()
    {
        // A comment (with surrounding whitespace) before the first inline-block
        // must not leave a leading space: the box starts flush at the body
        // content edge (x≈0), exactly as with no comment at all.
        var withComment = RenderFirstBoxRun("\n<!-- leading -->\n<div class=\"c\"></div>");
        var withoutComment = RenderFirstBoxRun("<div class=\"c\"></div>");

        Assert.True(withoutComment.left >= 0, "control: box not painted.");
        Assert.True(withComment.left >= 0, "with-comment: box not painted.");
        Assert.True(withComment.left <= 2,
            $"leading comment introduced a spurious leading space (box left={withComment.left}, expected ~0).");
        Assert.True(System.Math.Abs(withComment.left - withoutComment.left) <= 1,
            $"leading comment shifted the first box: with={withComment.left}, without={withoutComment.left}.");
    }

    [Fact]
    public void CommentBetweenBoxes_KeepsSingleSpace()
    {
        // A comment between two inline-blocks must collapse to the same single
        // inter-element space as plain whitespace — not two stacked spaces.
        var withComment = RenderFirstBoxRun(
            "<div class=\"c\"></div>\n<!-- between -->\n<div class=\"c\"></div>");
        var withoutComment = RenderFirstBoxRun(
            "<div class=\"c\"></div>\n<div class=\"c\"></div>");

        // First box identical in both.
        Assert.Equal(withoutComment.left, withComment.left);
        Assert.True(System.Math.Abs(withComment.right - withoutComment.right) <= 1,
            $"comment between boxes changed the first box width: with={withComment.right}, without={withoutComment.right}.");
    }
}
