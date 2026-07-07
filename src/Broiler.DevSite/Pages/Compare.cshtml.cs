using Broiler.DevSite.Services;
using Broiler.HTML.Image;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        const long maxFileSize = 5 * 1024 * 1024; // 5 MB

        if (htmlFile == null || htmlFile.Length == 0)
            return Page();

        if (htmlFile.Length > maxFileSize)
        {
            ModelState.AddModelError("htmlFile", "HTML file must be under 5 MB.");
            return Page();
        }

        if (referenceImage != null && referenceImage.Length > maxFileSize)
        {
            ModelState.AddModelError("referenceImage", "Reference image must be under 5 MB.");
            return Page();
        }

        width = Math.Clamp(width, 100, 4096);
        height = Math.Clamp(height, 100, 4096);

        using var reader = new StreamReader(htmlFile.OpenReadStream());
        string html = await reader.ReadToEndAsync();

        using var rendered = _renderer.RenderHtmlToBitmap(html, width, height);
        RenderedImageBase64 = BitmapToBase64(rendered);

        if (referenceImage != null && referenceImage.Length > 0)
        {
            using var refStream = referenceImage.OpenReadStream();
            using var reference = BBitmap.Decode(refStream);
            ReferenceImageBase64 = BitmapToBase64(reference);

            using var result = PixelDiffRunner.Compare(rendered, reference);
            MatchPercent = (1.0 - result.DiffRatio) * 100;
            DiffPixelCount = result.DiffPixelCount;
            TotalPixelCount = result.TotalPixelCount;

            if (result.DiffBitmap != null)
                DiffImageBase64 = BitmapToBase64(result.DiffBitmap);
        }

        return Page();
    }

    private static string BitmapToBase64(BBitmap bitmap) =>
        Convert.ToBase64String(bitmap.Encode(Graphics.BImageEncodeFormat.Png, 100));
}
