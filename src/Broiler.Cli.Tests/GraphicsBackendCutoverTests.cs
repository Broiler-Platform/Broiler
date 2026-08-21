using System.Text.Json;
using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

public class GraphicsBackendCutoverTests
{
    [Fact(Timeout = 600000)]
    public void BGraphicsBackend_Defaults_To_Broiler_Raster_Mode()
    {
        Assert.Equal(BGraphicsBackend.BroilerRasterId, BGraphicsBackend.CurrentId);
        Assert.Equal("Broiler raster", BGraphicsBackend.CurrentDisplayName);
        Assert.True(BGraphicsBackend.UseBroilerRasterPipeline);
    }

    [Fact(Timeout = 600000)]
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

    // Two pixel-parity facts used to sit here, rendering each fixture twice — once through
    // the Broiler raster pipeline and once through what was then a real second backend — and
    // asserting the results were identical. The stub is no longer a second implementation, so
    // the comparison measured the raster pipeline against a placeholder and had stopped
    // meaning anything. Pixel fidelity is the WPT suites' job; what is left here is the
    // backend selection surface itself.

    /// <summary>
    /// The capture sidecar records which backend produced the image. This is the only
    /// coverage of the <c>renderBackend</c> metadata anywhere, and it runs the real
    /// <c>--capture-image</c> path end to end.
    /// </summary>
    [Fact(Timeout = 600000)]
    public async Task CaptureArtifactMetadata_Records_The_Active_Backend()
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
}
