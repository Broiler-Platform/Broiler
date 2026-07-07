using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for the CSS Text 3 §2.1 <c>text-transform</c> implementation
/// in <c>CssBox.ParseToWords</c> (main-repo <c>Broiler.Layout</c>).
///
/// The computed value already reached each box (the CSS value validator accepts
/// <c>uppercase</c>/<c>lowercase</c>/<c>capitalize</c>/<c>full-width</c>), but the
/// layout engine never applied it, so the whole <c>css-text/text-transform</c>
/// suite rendered untransformed glyphs. These tests pin the transform by rendering
/// transformed text and asserting it is pixel-identical to the same text authored
/// in the target case — a font-independent oracle that fails without the fix
/// (untransformed text does not match the pre-cased reference).
/// </summary>
public class TextTransformTests : IDisposable
{
    private readonly string _tempDir;

    public TextTransformTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-text-transform-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private BBitmap Render(string transform, string text)
    {
        var html = $@"<!DOCTYPE html>
<style>
  body {{ margin: 0; font: 20px monospace; color: black; }}
  #t {{ text-transform: {transform}; }}
</style>
<body><div id='t'>{text}</div></body>";
        var file = Path.Combine(_tempDir, $"tt-{Guid.NewGuid().ToString("N")[..8]}.html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(400, 80);
        return runner.RenderHtmlFileBitmapPublic(file, _tempDir);
    }

    // Number of "inked" (non-near-white) pixels — a cheap glyph presence measure.
    private static int InkCount(BBitmap bmp)
    {
        int n = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var p = bmp.GetPixel(x, y);
            if (p.R < 200 || p.G < 200 || p.B < 200)
                n++;
        }
        return n;
    }

    private static bool PixelIdentical(BBitmap a, BBitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            return false;
        for (int y = 0; y < a.Height; y++)
        for (int x = 0; x < a.Width; x++)
        {
            var pa = a.GetPixel(x, y);
            var pb = b.GetPixel(x, y);
            if (pa.R != pb.R || pa.G != pb.G || pa.B != pb.B || pa.A != pb.A)
                return false;
        }
        return true;
    }

    private void AssertTransformMatchesLiteral(string transform, string source, string expected)
    {
        using var transformed = Render(transform, source);
        using var literal = Render("none", expected);

        Assert.True(InkCount(literal) > 0, "reference text painted nothing.");
        Assert.True(
            PixelIdentical(transformed, literal),
            $"text-transform:{transform} of \"{source}\" did not render identically to \"{expected}\".");
    }

    [Fact]
    public void Uppercase_RendersAsUppercasedText()
        => AssertTransformMatchesLiteral("uppercase", "hello world", "HELLO WORLD");

    [Fact]
    public void Lowercase_RendersAsLowercasedText()
        => AssertTransformMatchesLiteral("lowercase", "HELLO WORLD", "hello world");

    [Fact]
    public void Capitalize_TitlecasesEachWord()
        => AssertTransformMatchesLiteral("capitalize", "hello world", "Hello World");

    [Fact]
    public void Capitalize_KeepsApostropheInsideWord()
        => AssertTransformMatchesLiteral("capitalize", "can't stop", "Can't Stop");

    [Fact]
    public void Capitalize_TreatsHyphenAsWordBoundary()
        => AssertTransformMatchesLiteral("capitalize", "well-being", "Well-Being");

    [Fact]
    public void None_LeavesTextUnchanged()
        => AssertTransformMatchesLiteral("none", "Mixed Case", "Mixed Case");

    [Fact]
    public void Uppercase_ChangesRenderingFromSource()
    {
        // Guards the oracle itself: transformed output must differ from the
        // untransformed source (otherwise the identity test above is vacuous).
        using var transformed = Render("uppercase", "hello world");
        using var untransformed = Render("none", "hello world");
        Assert.False(
            PixelIdentical(transformed, untransformed),
            "text-transform:uppercase did not change the rendering.");
    }
}
