using System.IO;

using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// <c>document.currentScript</c> names the running script under the WPT runner.
/// </summary>
/// <remarks>
/// <para>
/// The runner never recorded which <c>&lt;script&gt;</c> each collected program came from, so the
/// bridge's current-script index stayed at its <c>-1</c> default and <c>document.currentScript</c>
/// was <see langword="null"/> for every script on every test. That is a different failure from a
/// wrong answer: a test whose script reads it took the "not supported" branch and was scored on a
/// page that had never done its work, rather than failing on the feature it was testing.
/// </para>
/// <para>
/// The pages below assert it through geometry, because that is what this suite can observe: the
/// script sizes a box from what <c>currentScript</c> reports, and the render says which branch it
/// took. The data block ahead of the script is the case that separates a recorded ordinal from a
/// re-derived classification — it is a <c>&lt;script&gt;</c> element the runner does not execute, so
/// pairing the collected programs against the document's script elements by position would name it
/// instead of the script that is running.
/// </para>
/// </remarks>
public class CurrentScriptUnderTheRunnerTests
{
    private const int Width = 400;
    private const int Height = 200;

    /// <summary>The widest run of red pixels, which is how each page reports its answer.</summary>
    private static int RedWidth(BBitmap bitmap)
    {
        var widest = 0;
        for (var y = 0; y < Height; y++)
        {
            var run = 0;
            for (var x = 0; x < Width; x++)
            {
                var (r, g, b) = ReadPixel(bitmap, x, y);
                if (r > 200 && g < 80 && b < 80)
                    run++;
                else if (run > widest)
                {
                    widest = run;
                    run = 0;
                }
                else
                {
                    run = 0;
                }
            }

            if (run > widest)
                widest = run;
        }

        return widest;
    }

    private static (int R, int G, int B) ReadPixel(BBitmap bitmap, int x, int y)
    {
        var pixel = bitmap.GetPixel(x, y);
        return (pixel.R, pixel.G, pixel.B);
    }

    private static int RenderAndMeasure(string html)
    {
        var directory = Path.Combine(Path.GetTempPath(), "broiler-currentscript-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "currentscript.html");
            File.WriteAllText(file, html);

            var runner = new WptTestRunner(Width, Height);
            using var bitmap = runner.RenderHtmlFileBitmapPublic(file, directory);
            return RedWidth(bitmap);
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch { /* best-effort */ }
        }
    }

    private const string Preamble =
        "<!DOCTYPE html><meta charset=\"utf-8\">"
        + "<style>body{margin:0} #b{height:40px;background:red;width:0}</style><div id=\"b\"></div>";

    /// <summary>The running script is the one <c>currentScript</c> names.</summary>
    [Fact(Timeout = 600000)]
    public void TheRunningScriptIsNamed()
    {
        var width = RenderAndMeasure(Preamble
            + "<script id=\"s1\">document.getElementById('b').style.width ="
            + " (document.currentScript && document.currentScript.id === 's1') ? '200px' : '20px';</script>");

        Assert.True(width is >= 195 and <= 205, $"red run was {width}px; expected ~200 (20 means currentScript was null).");
    }

    /// <summary>
    /// And a <c>&lt;script&gt;</c> the runner does not execute does not shift the answer. A data
    /// block is an element in the document and not a program in any bucket, so a host that pairs the
    /// two by position attributes the first script that runs to the block ahead of it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ADataBlockAheadOfItDoesNotShiftTheAnswer()
    {
        var width = RenderAndMeasure(Preamble
            + "<script type=\"application/ld+json\">{\"@context\":\"https://schema.org\"}</script>"
            + "<script id=\"s2\">document.getElementById('b').style.width ="
            + " (document.currentScript && document.currentScript.id === 's2') ? '300px' : '30px';</script>");

        Assert.True(width is >= 295 and <= 305, $"red run was {width}px; expected ~300 (30 means the data block was named).");
    }

    /// <summary>
    /// Outside script execution there is no current script, which is what a browser reports — so a
    /// callback that runs later reads <see langword="null"/> rather than whichever script ran last.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ATimerCallbackHasNoCurrentScript()
    {
        var width = RenderAndMeasure(Preamble
            + "<script id=\"s3\">setTimeout(function () {"
            + " document.getElementById('b').style.width = document.currentScript === null ? '100px' : '40px';"
            + "}, 0);</script>");

        Assert.True(width is >= 95 and <= 105, $"red run was {width}px; expected ~100 (40 means a script was still current).");
    }
}
