using System.Text.Json;
using Broiler.HTML.Image;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

public class GraphicsBackendCutoverTests
{
    [Fact]
    public void BGraphicsBackend_Defaults_To_Broiler_Raster_Mode()
    {
        Assert.Equal(BGraphicsBackend.BroilerRasterId, BGraphicsBackend.CurrentId);
        Assert.Equal("Broiler raster", BGraphicsBackend.CurrentDisplayName);
        Assert.True(BGraphicsBackend.UseBroilerRasterPipeline);
    }

    [Fact]
    public void BGraphicsBackend_Ignores_The_Legacy_Environment_Variable_Fallback()
    {
        const string legacyVariable = "BROILER_GRAPHICS_BACKEND";
        var previous = Environment.GetEnvironmentVariable(legacyVariable);

        try
        {
            Environment.SetEnvironmentVariable(legacyVariable, BGraphicsBackend.StubFallbackId);

            Assert.Equal(BGraphicsBackend.BroilerRasterId, BGraphicsBackend.CurrentId);
            Assert.Equal("Broiler raster", BGraphicsBackend.CurrentDisplayName);
            Assert.True(BGraphicsBackend.UseBroilerRasterPipeline);
        }
        finally
        {
            Environment.SetEnvironmentVariable(legacyVariable, previous);
        }
    }

    [Theory]
    [InlineData(BGraphicsBackend.BroilerRasterId, "Broiler raster", true)]
    [InlineData(BGraphicsBackend.StubFallbackId, "Stub compatibility fallback (no OS backend)", false)]
    public void BGraphicsBackend_OverrideForCurrentThread_Selects_Requested_Mode(
        string backendId,
        string expectedDisplayName,
        bool expectedRasterPipeline)
    {
        using var _ = BGraphicsBackend.OverrideForCurrentThread(backendId);

        Assert.Equal(backendId, BGraphicsBackend.CurrentId);
        Assert.Equal(expectedDisplayName, BGraphicsBackend.CurrentDisplayName);
        Assert.Equal($"{backendId} ({expectedDisplayName})", BGraphicsBackend.CurrentLabel);
        Assert.Equal(expectedRasterPipeline, BGraphicsBackend.UseBroilerRasterPipeline);
    }

    [Fact]
    public void HtmlRender_Curated_NonText_Fixture_Matches_Explicit_Skia_Fallback()
    {
        const string html = """
            <html><body style='margin:0;background:#ffffff'>
            <div style='margin:8px;width:120px;height:90px;background:linear-gradient(90deg,#0044ff,#55ccff);border:4px solid #112244;border-radius:16px'></div>
            <div style='margin:8px;width:140px;height:16px;background:#ff8844'></div>
            </body></html>
            """;

        using var broiler = RenderWithBackend(BGraphicsBackend.BroilerRasterId, html, 180, 140);
        using var skia = RenderWithBackend(BGraphicsBackend.StubFallbackId, html, 180, 140);
        using var diff = PixelDiffRunner.Compare(broiler, skia);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
    }

    [Fact]
    public void HtmlRender_Curated_Ahem_Text_Fixture_Matches_Explicit_Skia_Fallback()
    {
        EnsureAhemLoaded();

        const string html = """
            <html><body style='margin:0;background:#ffffff'>
            <div style='margin-left:10px;margin-top:12px;font:20px/1 Ahem;color:#000000'>XXXX</div>
            </body></html>
            """;

        using var broiler = RenderWithBackend(BGraphicsBackend.BroilerRasterId, html, 120, 60);
        using var skia = RenderWithBackend(BGraphicsBackend.StubFallbackId, html, 120, 60);
        using var diff = PixelDiffRunner.Compare(broiler, skia);

        Assert.True(diff.IsMatch);
        Assert.Equal(0, diff.DiffPixelCount);
        Assert.Equal(0d, diff.DiffRatio);
    }

    [Fact]
    public async Task CaptureArtifactMetadata_Uses_Explicit_Skia_Fallback_Label()
    {
        var htmlPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        var metadataPath = CaptureArtifactMetadata.GetSidecarPath(outputPath);

        try
        {
            await File.WriteAllTextAsync(htmlPath, "<html><body style='margin:0'>fallback metadata</body></html>");

            using var _ = BGraphicsBackend.OverrideForCurrentThread(BGraphicsBackend.StubFallbackId);
            var exitCode = await Program.Main([
                "--capture-image", htmlPath,
                "--output", outputPath,
                "--width", "32",
                "--height", "32",
            ]);

            Assert.Equal(0, exitCode);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath));
            var renderBackend = document.RootElement.GetProperty("renderBackend");

            Assert.Equal(BGraphicsBackend.StubFallbackId, renderBackend.GetProperty("id").GetString());
            Assert.Equal("Stub compatibility fallback (no OS backend)", renderBackend.GetProperty("displayName").GetString());
            Assert.Equal(BGraphicsBackend.CurrentLabel, renderBackend.GetProperty("label").GetString());
        }
        finally
        {
            if (File.Exists(htmlPath))
                File.Delete(htmlPath);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);
        }
    }

    private static BBitmap RenderWithBackend(string backendId, string html, int width, int height)
    {
        using var _ = BGraphicsBackend.OverrideForCurrentThread(backendId);
        return HtmlRender.RenderToImageWithStyleSet(html, width, height, backgroundColor: BColor.White);
    }

    private static void EnsureAhemLoaded()
    {
        using var _ = BGraphicsBackend.OverrideForCurrentThread(BGraphicsBackend.BroilerRasterId);
        HtmlRender.LoadFontFromFile(Path.Combine(GetRepoRoot(), "tests", "wpt", "fonts", "Ahem.ttf"), "Ahem");
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Broiler.slnx")))
            current = current.Parent;

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
