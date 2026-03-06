using Broiler.DevSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace Broiler.DevSite.Pages;

public class TestRunnerModel : PageModel
{
    private readonly RenderingService _renderer;
    private readonly IWebHostEnvironment _env;

    public TestRunnerModel(RenderingService renderer, IWebHostEnvironment env)
    {
        _renderer = renderer;
        _env = env;
    }

    public string? TestName { get; set; }
    public double MatchPercent { get; set; }
    public int DiffPixelCount { get; set; }
    public int TotalPixelCount { get; set; }
    public int RenderWidth { get; set; }
    public int RenderHeight { get; set; }
    public string? RenderedImageBase64 { get; set; }
    public string? ReferenceImageBase64 { get; set; }
    public string? DiffImageBase64 { get; set; }

    public void OnGet() { }

    public IActionResult OnPostAcid1()
    {
        RunTest("Acid1",
            Path.Combine(AcidDirectory, "acid1", "acid1.html"),
            Path.Combine(AcidDirectory, "acid1", "acid1.png"),
            anchor: null);
        return Page();
    }

    public IActionResult OnPostAcid2()
    {
        RunTest("Acid2",
            Path.Combine(AcidDirectory, "acid2", "acid2.html"),
            Path.Combine(AcidDirectory, "acid2", "acid2.png"),
            anchor: "top");
        return Page();
    }

    private void RunTest(string name, string htmlPath, string referencePath, string? anchor)
    {
        TestName = name;

        if (!System.IO.File.Exists(htmlPath))
        {
            TestName = $"{name} — HTML file not found: {htmlPath}";
            return;
        }

        string html = System.IO.File.ReadAllText(htmlPath);
        int width = 1024;
        int height = 768;

        // Render via Broiler
        SKBitmap rendered;
        if (anchor != null)
        {
            byte[]? pngData = _renderer.RenderAtAnchor(html, anchor, width, height);
            if (pngData == null)
            {
                TestName = $"{name} — Anchor '#{anchor}' not found";
                return;
            }
            rendered = SKBitmap.Decode(pngData);
        }
        else
        {
            rendered = _renderer.RenderHtmlToImage(html, width, height);
        }

        using (rendered)
        {
            RenderWidth = rendered.Width;
            RenderHeight = rendered.Height;
            RenderedImageBase64 = BitmapToBase64(rendered);

            // Load reference image
            if (System.IO.File.Exists(referencePath))
            {
                using var reference = SKBitmap.Decode(referencePath);
                ReferenceImageBase64 = BitmapToBase64(reference);

                // Compute pixel diff
                using var result = PixelDiffRunner.Compare(rendered, reference);
                MatchPercent = (1.0 - result.DiffRatio) * 100;
                DiffPixelCount = result.DiffPixelCount;
                TotalPixelCount = result.TotalPixelCount;

                if (result.DiffImage != null)
                {
                    DiffImageBase64 = BitmapToBase64(result.DiffImage);
                }
            }
            else
            {
                ReferenceImageBase64 = RenderedImageBase64;
                MatchPercent = 100;
                TotalPixelCount = rendered.Width * rendered.Height;
            }
        }
    }

    private static string BitmapToBase64(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }

    private string AcidDirectory =>
        Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..", "acid"));
}
