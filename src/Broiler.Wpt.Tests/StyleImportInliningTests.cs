using System.IO;
using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Regression tests for render-time <c>@import</c> resolution
/// (<c>DomBridge.InlineStyleSheetImports</c>, run from
/// <c>ApplySerializationTransforms</c>). The renderer re-parses the serialized HTML and
/// applies each sheet, but never fetched a sheet's <c>@import</c> rules — the imported
/// CSS never reached the cascade, so tests whose only styling came through an
/// <c>@import</c> (the WPT <c>css/cssom</c> import family, e.g. <c>cssimportrule-parent</c>)
/// rendered blank. Inlining the imported text into the importing <c>&lt;style&gt;</c> at
/// serialization time closes that gap.
/// </summary>
public class StyleImportInliningTests : IDisposable
{
    private readonly string _tempDir;

    public StyleImportInliningTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "broiler-style-import-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Program.ResetTestHooks();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private BColor RenderCenterPixel(string html)
    {
        var file = Path.Combine(_tempDir, "t-" + Guid.NewGuid().ToString("N")[..6] + ".html");
        File.WriteAllText(file, html);
        var runner = new WptTestRunner(200, 200);
        using var bmp = runner.RenderHtmlFileBitmapPublic(file, _tempDir);
        return bmp.GetPixel(100, 100);
    }

    private static bool IsRed(BColor c) => c.R > 200 && c.G < 80 && c.B < 80;
    private static bool IsGreenish(BColor c) => c.G > 100 && c.R < 80 && c.B < 80;
    private static bool IsWhite(BColor c) => c.R > 200 && c.G > 200 && c.B > 200;

    [Fact]
    public void DataUrlImport_AppliesImportedRules()
    {
        // @import of a data: URL must apply its rules — Chromium renders this red.
        var color = RenderCenterPixel(
            "<!doctype html>\n<style>@import \"data:text/css,:root{background:red}\";</style>");
        Assert.True(IsRed(color), $"data: @import should apply a red background; got {color.R},{color.G},{color.B}.");
    }

    [Fact]
    public void RelativeFileImport_AppliesImportedRules()
    {
        File.WriteAllText(Path.Combine(_tempDir, "imported.css"), ":root{background:red}");
        var color = RenderCenterPixel(
            "<!doctype html>\n<style>@import \"imported.css\";</style>");
        Assert.True(IsRed(color), $"relative @import should apply a red background; got {color.R},{color.G},{color.B}.");
    }

    [Fact]
    public void NestedImport_IsResolvedRecursively()
    {
        // a.css imports b.css, which sets the background — the whole chain must resolve.
        File.WriteAllText(Path.Combine(_tempDir, "a.css"), "@import \"b.css\";");
        File.WriteAllText(Path.Combine(_tempDir, "b.css"), ":root{background:red}");
        var color = RenderCenterPixel(
            "<!doctype html>\n<style>@import \"a.css\";</style>");
        Assert.True(IsRed(color), $"nested @import chain should apply a red background; got {color.R},{color.G},{color.B}.");
    }

    [Fact]
    public void ImportCycle_DoesNotHang_AndAppliesReachableRules()
    {
        // a.css imports b.css and sets red; b.css imports a.css (a cycle). Resolution
        // must terminate — the cycle is broken — and the reachable red rule applies.
        File.WriteAllText(Path.Combine(_tempDir, "a.css"), "@import \"b.css\";\n:root{background:red}");
        File.WriteAllText(Path.Combine(_tempDir, "b.css"), "@import \"a.css\";");
        var color = RenderCenterPixel(
            "<!doctype html>\n<style>@import \"a.css\";</style>");
        Assert.True(IsRed(color), $"cyclic @import should terminate and apply red; got {color.R},{color.G},{color.B}.");
    }

    [Fact]
    public void ImportAfterStyleRule_IsIgnored()
    {
        // Per CSS syntax, an @import after a style rule is invalid and ignored. The
        // leading rule (green) wins; the misplaced @import (red) must not apply.
        File.WriteAllText(Path.Combine(_tempDir, "imported.css"), ":root{background:red}");
        var color = RenderCenterPixel(
            "<!doctype html>\n<style>:root{background:green}\n@import \"imported.css\";</style>");
        Assert.True(IsGreenish(color),
            $"an @import after a style rule must be ignored (background stays green); got {color.R},{color.G},{color.B}.");
    }

    [Fact]
    public void NoImport_LeavesRenderingUnchanged()
    {
        // A stylesheet without @import must be untouched — no spurious rewrite.
        var withoutImport = RenderCenterPixel("<!doctype html>\n<style>:root{background:red}</style>");
        Assert.True(IsRed(withoutImport));

        var plain = RenderCenterPixel("<!doctype html>\n<style>body{margin:0}</style>");
        Assert.True(IsWhite(plain), $"a plain sheet should render white; got {plain.R},{plain.G},{plain.B}.");
    }
}
