using Broiler.DevSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SkiaSharp;
using TheArtOfDev.HtmlRenderer.Image;

namespace Broiler.DevSite.Pages;

public class CompareModel : PageModel
{
    private readonly RenderingService _renderer;

    public CompareModel(RenderingService renderer) => _renderer = renderer;

    public string? RenderedImageBase64 { get; set; }
    public string? ReferenceImageBase64 { get; set; }
    public string? DiffImageBase64 { get; set; }
    public double MatchPercent { get; set; }
    public int DiffPixelCount { get; set; }
    public int TotalPixelCount { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(IFormFile htmlFile, IFormFile? referenceImage, int width = 1024, int height = 768)
    {
        if (htmlFile == null || htmlFile.Length == 0)
            return Page();

        width = Math.Clamp(width, 100, 4096);
        height = Math.Clamp(height, 100, 4096);

        using var reader = new StreamReader(htmlFile.OpenReadStream());
        string html = await reader.ReadToEndAsync();

        using var rendered = _renderer.RenderHtmlToImage(html, width, height);
        RenderedImageBase64 = BitmapToBase64(rendered);

        if (referenceImage != null && referenceImage.Length > 0)
        {
            using var refStream = referenceImage.OpenReadStream();
            using var reference = SKBitmap.Decode(refStream);
            ReferenceImageBase64 = BitmapToBase64(reference);

            using var result = PixelDiffRunner.Compare(rendered, reference);
            MatchPercent = (1.0 - result.DiffRatio) * 100;
            DiffPixelCount = result.DiffPixelCount;
            TotalPixelCount = result.TotalPixelCount;

            if (result.DiffImage != null)
                DiffImageBase64 = BitmapToBase64(result.DiffImage);
        }

        return Page();
    }

    private static string BitmapToBase64(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }
}
