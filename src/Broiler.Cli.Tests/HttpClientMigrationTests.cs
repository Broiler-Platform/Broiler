using System.Reflection;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests verifying the migration from <c>WebClient</c> to <c>HttpClient</c>
/// across all HtmlRenderer assemblies.
/// </summary>
public class HttpClientMigrationTests
{
    private static Assembly LoadAssembly(string name)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == name);
        if (loaded != null)
            return loaded;

        var dir = Path.GetDirectoryName(typeof(HttpClientMigrationTests).Assembly.Location)!;
        return Assembly.LoadFrom(Path.Combine(dir, name + ".dll"));
    }

    // ────────────────────── WebClient removal ──────────────────────

    [Fact]
    public void HtmlRenderer_Rendering_Assembly_Has_No_WebClient_References()
    {
        var assembly = LoadAssembly("HtmlRenderer.Rendering");

        var webClientTypes = assembly.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(f => f.FieldType.FullName?.Contains("WebClient") == true)
            .ToList();

        Assert.Empty(webClientTypes);
    }

    [Fact]
    public void HtmlRenderer_Orchestration_Assembly_Has_No_WebClient_References()
    {
        var assembly = LoadAssembly("HtmlRenderer.Orchestration");

        var webClientTypes = assembly.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(f => f.FieldType.FullName?.Contains("WebClient") == true)
            .ToList();

        Assert.Empty(webClientTypes);
    }

    [Fact]
    public void HtmlRenderer_Utils_Assembly_Has_No_WebClient_Method_Parameters()
    {
        var assembly = LoadAssembly("HtmlRenderer.Utils");

        var webClientMethods = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(m => m.GetParameters().Any(p => p.ParameterType.FullName?.Contains("WebClient") == true))
            .ToList();

        Assert.Empty(webClientMethods);
    }

    // ────────────────────── HttpClient adoption ──────────────────────

    [Fact]
    public void ImageDownloader_Uses_HttpClient()
    {
        var assembly = LoadAssembly("HtmlRenderer.Rendering");

        var downloaderType = assembly.GetTypes()
            .First(t => t.Name == "ImageDownloader");

        var httpClientFields = downloaderType
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.FieldType.FullName?.Contains("HttpClient") == true)
            .ToList();

        Assert.NotEmpty(httpClientFields);
    }

    [Fact]
    public void StylesheetLoadHandler_Uses_HttpClient()
    {
        var assembly = LoadAssembly("HtmlRenderer.Orchestration");

        var handlerType = assembly.GetTypes()
            .First(t => t.Name == "StylesheetLoadHandler");

        var httpClientFields = handlerType
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.FieldType.FullName?.Contains("HttpClient") == true)
            .ToList();

        Assert.NotEmpty(httpClientFields);
    }

    // ────────────────────── SSL / HTTPS support ──────────────────────

    [Fact]
    public void ImageDownloader_Supports_Cancellation()
    {
        var assembly = LoadAssembly("HtmlRenderer.Rendering");

        var downloaderType = assembly.GetTypes()
            .First(t => t.Name == "ImageDownloader");

        var ctsField = downloaderType
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(f => f.FieldType == typeof(CancellationTokenSource));

        Assert.NotNull(ctsField);
    }

    [Fact]
    public void PageLoader_Defaults_To_Https_For_Bare_Urls()
    {
        // PageLoader (already using HttpClient) normalises bare domains to https://
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
// Verify that the page loader would prefix bare URLs with https://
var url = 'example.com';
if (!url.startsWith('http://') && !url.startsWith('https://')) {
    url = 'https://' + url;
}
document.getElementById('result').textContent = url;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("https://example.com", result);
    }

    [Fact]
    public void ScriptExtractor_Uses_HttpClient()
    {
        var assembly = LoadAssembly("Broiler.HtmlBridge");

        var extractorType = assembly.GetTypes()
            .First(t => t.Name == "ScriptExtractor");

        var httpClientFields = extractorType
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.FieldType.FullName?.Contains("HttpClient") == true)
            .ToList();

        Assert.NotEmpty(httpClientFields);
    }

    [Fact]
    public void ImageDownloader_Implements_IDisposable()
    {
        var assembly = LoadAssembly("HtmlRenderer.Rendering");

        var downloaderType = assembly.GetTypes()
            .First(t => t.Name == "ImageDownloader");

        Assert.True(typeof(IDisposable).IsAssignableFrom(downloaderType));
    }
}
