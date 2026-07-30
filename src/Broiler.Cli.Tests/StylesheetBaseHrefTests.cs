using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// A <c>&lt;link rel="stylesheet"&gt;</c> with a relative <c>href</c> must resolve against the
/// document's <c>&lt;base href&gt;</c>, not the document's own directory — WPT
/// <c>html/semantics/document-metadata/the-link-element/stylesheet-with-base</c>. The linked
/// sheet, reachable only under the base-relative <c>resources/</c> directory, sets the page
/// green; without base resolution the sheet 404s and the unstyled fallback shows through.
/// </summary>
public class StylesheetBaseHrefTests
{
    [Fact]
    public void LinkedStylesheet_Href_ResolvesAgainstBaseHref()
    {
        var baseDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "broiler-basehref-" + System.Guid.NewGuid().ToString("N"));
        var resourcesDir = System.IO.Path.Combine(baseDir, "resources");
        System.IO.Directory.CreateDirectory(resourcesDir);
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(resourcesDir, "stylesheet.css"),
            "body { background-color: green; }");
        // The trap the WPT test plants: a same-named sheet next to the document. Resolving the
        // href against the document's own directory picks THIS one and paints red, which is
        // exactly how the test failed (issue #1491, problem 25).
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(baseDir, "stylesheet.css"),
            "body { background-color: red; }");
        try
        {
            var pageUrl = new System.Uri(System.IO.Path.Combine(baseDir, "test.html")).AbsoluteUri;
            const string html = """
<!DOCTYPE html>
<html>
<head>
<base href="resources/">
<link rel="stylesheet" href="stylesheet.css">
</head>
<body></body>
</html>
""";
            using var context = new JSContext();
            var bridge = new DomBridge();
            bridge.Attach(context, html, pageUrl);
            var serialized = bridge.SerializeToHtml();
            using var bmp = HtmlRender.RenderToImageWithStyleSet(serialized, 200, 200);
            var c = bmp.GetPixel(100, 100);
            Assert.True((c.R, c.G, c.B) == (0, 128, 0), $"expected green canvas, was {c.R},{c.G},{c.B}");
        }
        finally
        {
            try { System.IO.Directory.Delete(baseDir, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
