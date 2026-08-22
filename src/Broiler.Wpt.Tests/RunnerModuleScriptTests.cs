using System.IO;
using Broiler.HTML.Image;

namespace Broiler.Wpt.Tests;

/// <summary>
/// A <c>&lt;script type="module"&gt;</c> in a WPT test runs, and its static imports bind.
/// </summary>
/// <remarks>
/// <para>
/// The runner scans a document's scripts itself — it has its own stub injection, its own testharness
/// handling and its own ordering — and that scan knew only how to hand a string to <c>Eval</c>. A
/// module cannot be run that way: <c>import</c> is a syntax error to a classic evaluation, so every
/// module in the corpus threw on its first line, the error was logged and swallowed, and the document
/// the module existed to build was never built. The 32 css-grid/abspos tests that generate their
/// grids from a shared support module rendered a blank page against a reference full of them; with
/// the modules running, <c>positioned-grid-descendants-001</c> reports 800 of 800 layout assertions
/// passing, having reported none at all before.
/// </para>
/// <para>
/// The import is the assertion that moves: an inline module with no import is valid classic
/// JavaScript, so <c>Eval</c> ran one perfectly well before and the first test below passes either
/// way — it is here as the control that the new path did not cost anything. Only the second fails
/// without the fix, and it is the shape every css-grid/abspos module test is built on.
/// </para>
/// </remarks>
public class RunnerModuleScriptTests : IDisposable
{
    private readonly string _tempDir;

    public RunnerModuleScriptTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-module-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>The widest extent of green pixels, or (-1, -1) when the page painted none.</summary>
    private static (int Width, int Height) GreenBox(BBitmap bmp)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var p = bmp.GetPixel(x, y);
            if (p.G <= 100 || p.R >= 100 || p.B >= 100)
                continue;

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        return maxX < 0 ? (-1, -1) : (maxX - minX + 1, maxY - minY + 1);
    }

    private BBitmap Render(string html)
    {
        var file = Path.Combine(_tempDir, "page.html");
        File.WriteAllText(file, html);

        var runner = new WptTestRunner(320, 240);
        return runner.RenderHtmlFileBitmapPublic(file, _tempDir);
    }

    /// <summary>A control rather than a regression guard — see the class remarks.</summary>
    [Fact(Timeout = 600000)]
    public void An_Inline_Module_Runs()
    {
        using var bmp = Render(
            "<!DOCTYPE html><body><script type=\"module\">\n"
            + "const d = document.createElement('div');\n"
            + "d.style.cssText = 'width:100px;height:50px;background:green';\n"
            + "document.body.appendChild(d);\n"
            + "</script></body>");

        Assert.Equal((100, 50), GreenBox(bmp));
    }

    /// <summary>
    /// The half the scan could never provide: the engine resolves the specifier against the module's
    /// own URL, fetches it, and binds the export. This is the shape every css-grid/abspos module test
    /// uses (<c>import {runTests} from "./support/…js"</c>).
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Static_Import_From_A_Sibling_File_Binds()
    {
        File.WriteAllText(Path.Combine(_tempDir, "paint.js"),
            "export function paint(w, h) {\n"
            + "  const d = document.createElement('div');\n"
            + "  d.style.cssText = `width:${w}px;height:${h}px;background:green`;\n"
            + "  document.body.appendChild(d);\n"
            + "}\n");

        using var bmp = Render(
            "<!DOCTYPE html><body><script type=\"module\">\n"
            + "import { paint } from './paint.js';\n"
            + "paint(80, 40);\n"
            + "</script></body>");

        Assert.Equal((80, 40), GreenBox(bmp));
    }

    /// <summary>The other control: a classic script is unaffected by any of this and still runs.</summary>
    [Fact(Timeout = 600000)]
    public void A_Classic_Script_Still_Runs()
    {
        using var bmp = Render(
            "<!DOCTYPE html><body><script>\n"
            + "const d = document.createElement('div');\n"
            + "d.style.cssText = 'width:60px;height:30px;background:green';\n"
            + "document.body.appendChild(d);\n"
            + "</script></body>");

        Assert.Equal((60, 30), GreenBox(bmp));
    }
}
