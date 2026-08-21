using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// CSS 2.1 §10.8: the baseline of an inline <b>replaced</b> element is its bottom margin edge, so
/// images of different heights on one line stand on a shared baseline rather than starting at a
/// shared top.
/// </summary>
/// <remarks>
/// <para>
/// Only the <c>inline-block</c> half of that rule was implemented. An <c>&lt;img&gt;</c> was
/// aligned by the ascent of a font it does not draw — a constant ~13px for the default 16px strut
/// — so every image on a line was placed the same distance below the line's top regardless of its
/// height, which reads as top-aligned. The vertical-alignment pass then skipped image words
/// outright, so even that placement never moved them.
/// </para>
/// <para>
/// The line-box <em>height</em> code had always assumed the other rule: it extends a line below a
/// tall image by the strut's descent precisely because the image's bottom is the baseline. The two
/// halves disagreed with each other, not merely with the spec.
/// </para>
/// <para>
/// Expected geometry throughout is the reference browser's, measured on the same markup.
/// </para>
/// </remarks>
public class InlineReplacedBaselineTests
{
    private const int Width = 400;
    private const int Height = 300;

    /// <summary>A 1×1 lime PNG, scaled by the <c>width</c>/<c>height</c> attributes.</summary>
    private const string LimePixel =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNg+M8AAAICAQB7CYF4AAAAAElFTkSuQmCC";

    private static string Page(string body) => $$"""
<!DOCTYPE html>
<body style="margin:0;background:white;font:16px/16px monospace">{{body}}</body>
""";

    /// <summary>
    /// The rows a colour occupies, as (top, bottom-exclusive), scanning the column at
    /// <paramref name="x"/>. Bottom is where the image's baseline sits.
    /// </summary>
    private static (int Top, int Bottom) ColumnExtent(BBitmap bitmap, int x)
    {
        int top = -1, bottom = -1;
        for (int y = 0; y < Height; y++)
        {
            var pixel = bitmap.GetPixel(x, y);
            bool lime = pixel.G > 200 && pixel.R < 100 && pixel.B < 100;
            if (!lime)
                continue;
            if (top < 0)
                top = y;
            bottom = y + 1;
        }
        return (top, bottom);
    }

    /// <summary>
    /// Three images of different heights on one line: their bottoms coincide (the baseline), their
    /// tops do not. Before the fix all three tops coincided and the bottoms did not — the exact
    /// inverse.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Images_Of_Different_Heights_Share_A_Baseline_Not_A_Top()
    {
        var html = Page($"""
<img src="{LimePixel}" width="40" height="80"><img src="{LimePixel}" width="40" height="40"><img src="{LimePixel}" width="40" height="20">
""");
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, Width, Height, backgroundColor: Broiler.Graphics.BColor.White);

        var tall = ColumnExtent(bitmap, 20);
        var medium = ColumnExtent(bitmap, 60);
        var shortest = ColumnExtent(bitmap, 100);

        Assert.True(tall.Bottom > 0 && medium.Bottom > 0 && shortest.Bottom > 0,
            $"all three images must paint (got {tall}, {medium}, {shortest})");

        // The baseline: within a pixel of each other, because that is the one edge they share.
        Assert.InRange(medium.Bottom - tall.Bottom, -1, 1);
        Assert.InRange(shortest.Bottom - tall.Bottom, -1, 1);

        // …and the tops are as far apart as the heights differ.
        Assert.InRange(medium.Top - tall.Top, 39, 41);
        Assert.InRange(shortest.Top - tall.Top, 59, 61);
    }

    /// <summary>
    /// The line box still makes room below the baseline for the strut's descent, so a following
    /// line clears the image rather than overlapping it. This is the half that was already
    /// implemented; it is pinned here because the two halves have to agree.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Tall_Image_Leaves_The_Struts_Descent_Below_Its_Baseline()
    {
        var html = Page($"""
<img src="{LimePixel}" width="40" height="80"><br><img src="{LimePixel}" width="40" height="20">
""");
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, Width, Height, backgroundColor: Broiler.Graphics.BColor.White);

        var first = ColumnExtent(bitmap, 20);

        // The second line's image is in the same column, so the column extent runs from the first
        // image's top to the second's bottom; what matters is that the two do not merge into one
        // run — i.e. there is white between them.
        bool gap = false;
        for (int y = first.Top; y < first.Bottom; y++)
        {
            var pixel = bitmap.GetPixel(20, y);
            if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200)
            {
                gap = true;
                break;
            }
        }

        Assert.True(gap, "the second line must clear the first line's image rather than abut it");
    }

    /// <summary>
    /// Text sharing a line with an image taller than itself sits on the image's bottom edge,
    /// because that edge <em>is</em> the line's baseline. Aligning the image by a font ascent
    /// instead put it at the line's top and left the text up there with it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Text_On_The_Line_Sits_On_The_Images_Bottom_Edge()
    {
        var html = Page($"""
<img src="{LimePixel}" width="40" height="80">Ay
""");
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(html, Width, Height, backgroundColor: Broiler.Graphics.BColor.White);

        var image = ColumnExtent(bitmap, 20);
        Assert.InRange(image.Bottom - image.Top, 79, 81);

        // The glyphs' own extent, anywhere to the right of the image.
        int textTop = -1, textBottom = -1;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 41; x < Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200)
                    continue;
                if (textTop < 0)
                    textTop = y;
                textBottom = y + 1;
                break;
            }
        }

        Assert.True(textTop >= 0, "the text must paint");

        // A capital A's baseline is its bottom; a 'y' descends below it. Both sit on the image's
        // bottom edge to within the descender, which is nowhere near the line's top.
        Assert.InRange(textBottom, image.Bottom - 2, image.Bottom + 8);
    }
}
