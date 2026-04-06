using Broiler.Cli;
using Broiler.HTML.Image;
using Broiler.HtmlBridge;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace Broiler.Cli.Tests;

public class PipelineDebugTest
{
    private readonly ITestOutputHelper _output;
    public PipelineDebugTest(ITestOutputHelper output) { _output = output; }

    [Fact]
    public async System.Threading.Tasks.Task FullPipeline_GoogleDe_Debug()
    {
        // Fetch real Google.de
        using var http = new System.Net.Http.HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        string html;
        try
        {
            html = await http.GetStringAsync("https://www.google.de");
        }
        catch
        {
            _output.WriteLine("Cannot fetch Google.de - skipping");
            return;
        }
        
        _output.WriteLine($"Fetched HTML length: {html.Length}");
        
        // Check for buttons in raw HTML
        bool hasGoogleSuche = html.Contains("Google Suche") || html.Contains("Google Search") || html.Contains("Google-Suche");
        bool hasBtnG = html.Contains("btnG");
        bool hasSubmit = html.Contains("type=\"submit\"") || html.Contains("type='submit'");
        _output.WriteLine($"Raw HTML: hasGoogleSuche={hasGoogleSuche} hasBtnG={hasBtnG} hasSubmit={hasSubmit}");
        
        // Check for key CSS classes
        bool hasDsClass = html.Contains(".ds{") || html.Contains(".ds {");
        bool hasLsbbClass = html.Contains(".lsbb{") || html.Contains(".lsbb {");
        bool hasLsbClass = html.Contains(".lsb{") || html.Contains(".lsb {");
        _output.WriteLine($"Raw CSS: .ds={hasDsClass} .lsbb={hasLsbbClass} .lsb={hasLsbClass}");
        
        // Post-process (this is what Broiler.App does without JS)
        var processed = HtmlPostProcessor.Process(html);
        _output.WriteLine($"\nPost-processed HTML length: {processed.Length}");
        
        // Check buttons still in post-processed HTML
        hasGoogleSuche = processed.Contains("Google Suche") || processed.Contains("Google Search") || processed.Contains("Google-Suche");
        hasBtnG = processed.Contains("btnG");
        hasSubmit = processed.Contains("type=\"submit\"") || processed.Contains("type='submit'");
        _output.WriteLine($"Post-processed: hasGoogleSuche={hasGoogleSuche} hasBtnG={hasBtnG} hasSubmit={hasSubmit}");
        
        // Check form preserved
        bool hasForm = processed.Contains("<form");
        bool hasTable = processed.Contains("<table");
        _output.WriteLine($"Post-processed: hasForm={hasForm} hasTable={hasTable}");
        
        // Extract the button area from HTML for inspection
        int btnIdx = processed.IndexOf("btnG");
        if (btnIdx >= 0)
        {
            int start = Math.Max(0, btnIdx - 200);
            int end = Math.Min(processed.Length, btnIdx + 200);
            _output.WriteLine($"\nHTML around btnG:\n{processed[start..end]}");
        }
        
        // Render the post-processed HTML (without JS pipeline)
        using var bmp = HtmlRender.RenderToImage(processed, 800, 600);
        using (var data = bmp.Encode(SKEncodedImageFormat.Png, 100))
        using (var f = System.IO.File.OpenWrite("/tmp/google_postproc_render.png"))
            data.SaveTo(f);
        
        // Analyze rendering
        int totalTextRows = 0;
        int fullWidthLines = 0;
        int btnAreaDark = 0;
        
        for (int y = 0; y < bmp.Height; y++)
        {
            int left = bmp.Width, right = 0;
            int dark = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                    { if (x < left) left = x; if (x > right) right = x; }
                if (px.Red < 80 && px.Green < 80 && px.Blue < 80) dark++;
            }
            int w = (left <= right) ? right - left + 1 : 0;
            if (w > 700) { fullWidthLines++; _output.WriteLine($"FULLWIDTH y={y}: [{left},{right}] w={w}"); }
            if (dark >= 3) { totalTextRows++; if (totalTextRows <= 30) _output.WriteLine($"TEXT y={y}: [{left},{right}] w={w} dark={dark}"); }
            if (y >= 100 && y <= 400) btnAreaDark += dark;
        }
        
        _output.WriteLine($"\nText rows: {totalTextRows}, Full-width: {fullWidthLines}, Btn area dark: {btnAreaDark}");
        
        Assert.True(btnAreaDark > 10, $"Buttons invisible (dark={btnAreaDark})");
        Assert.True(fullWidthLines < 5, $"Too many full-width lines ({fullWidthLines})");
    }
}
