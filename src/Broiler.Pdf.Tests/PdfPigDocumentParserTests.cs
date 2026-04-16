using System.Text.Json;

namespace Broiler.Pdf.Tests;

public class PdfPigDocumentParserTests
{
    [Fact]
    public void M0_CorpusManifest_Covers_All_Required_Categories()
    {
        var manifest = LoadManifest();

        Assert.Equal("m0", manifest.Version);
        Assert.Contains(manifest.Entries, entry => entry.Category == "simple-text" && entry.AutomationStatus == "verified");
        Assert.Contains(manifest.Entries, entry => entry.Category == "multi-page-report" && entry.AutomationStatus == "verified");
        Assert.Contains(manifest.Entries, entry => entry.Category == "scanned-image-heavy" && entry.AutomationStatus == "verified");
        Assert.Contains(manifest.Entries, entry => entry.Category == "malformed" && entry.AutomationStatus == "verified");
        Assert.Contains(manifest.Entries, entry => entry.Category == "object-stream" && entry.AutomationStatus == "planned");
        Assert.Contains(manifest.Entries, entry => entry.Category == "encrypted" && entry.AutomationStatus == "planned");
    }

    [Theory]
    [InlineData("simple-text-generated")]
    [InlineData("multi-page-generated")]
    [InlineData("image-heavy-generated")]
    public void Open_GeneratedCorpusFixture_Matches_Baseline(string fixtureId)
    {
        var manifest = LoadManifest();
        var entry = manifest.Entries.Single(manifestEntry => manifestEntry.Id == fixtureId);
        var tempDirectory = CreateTempDirectory();

        try
        {
            var pdfPath = Path.Combine(tempDirectory, fixtureId + ".pdf");
            File.WriteAllBytes(pdfPath, PdfTestCorpus.CreateFixtureBytes(fixtureId));

            var parser = new PdfPigDocumentParser();
            using var document = parser.Open(pdfPath);

            Assert.NotNull(entry.Baseline);
            Assert.False(entry.Baseline!.ExpectsOpenFailure);
            Assert.Equal(entry.Baseline.PageCount, document.Pages.Count);

            for (int i = 0; i < document.Pages.Count; i++)
            {
                var page = document.Pages[i];
                var layout = page.ExtractLayout();

                Assert.Equal(entry.Baseline.TextByPage[i], page.Text.Trim());
                Assert.Equal(entry.Baseline.LayoutTextByPage[i], NormalizeLayoutText(layout));
                Assert.Equal(entry.Baseline.LayoutImageCountByPage[i], layout.Images.Count);
                Assert.Equal(612d, page.MediaBox.Width, 3);
                Assert.Equal(792d, page.MediaBox.Height, 3);
            }
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void Open_MalformedCorpusFixture_Fails()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var pdfPath = Path.Combine(tempDirectory, "malformed.pdf");
            File.WriteAllBytes(pdfPath, PdfTestCorpus.CreateFixtureBytes("malformed-generated"));

            var parser = new PdfPigDocumentParser();

            Assert.ThrowsAny<Exception>(() => parser.Open(pdfPath));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static PdfCorpusManifest LoadManifest()
    {
        var json = File.ReadAllText(PdfTestCorpus.GetManifestPath());
        var manifest = JsonSerializer.Deserialize<PdfCorpusManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        Assert.NotNull(manifest);
        return manifest!;
    }

    private static string NormalizeLayoutText(PdfPageLayout layout)
    {
        return string.Concat(layout.Text.Select(fragment => fragment.Text).Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "broiler-pdf-parser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record PdfCorpusManifest(string Version, IReadOnlyList<PdfCorpusManifestEntry> Entries);

    private sealed record PdfCorpusManifestEntry(
        string Id,
        string Category,
        string AutomationStatus,
        string Generator,
        string License,
        string? Notes,
        PdfCorpusBaseline? Baseline);

    private sealed record PdfCorpusBaseline(
        int PageCount,
        IReadOnlyList<string> TextByPage,
        IReadOnlyList<string> LayoutTextByPage,
        IReadOnlyList<int> LayoutImageCountByPage,
        bool ExpectsOpenFailure);
}
