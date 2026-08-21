using System.Linq;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// A <c>&lt;script&gt;</c> whose <c>type</c> is neither a JavaScript MIME essence nor <c>module</c>
/// is a data block, and a browser never executes it. Extraction checked only for
/// <c>type="module"</c>, so every JSON-LD block, framework-state blob, speculation-rules list,
/// import map and client-side template in a document was collected and handed to the engine, which
/// reported a syntax error for content that was never meant to compile — a <c>text/template</c>
/// opening with a bullet failed as "Unexpected token Empty:  at 1, 1", naming neither the script nor
/// the character.
/// <para>
/// The rule already existed, in <c>ScriptInsertionRunner</c>, and applied only to scripts a page
/// inserts at runtime; it now lives in <see cref="ScriptMimeType"/> and both paths share it.
/// </para>
/// </summary>
public sealed class ScriptDataBlockTests
{
    [Theory]
    [InlineData("application/ld+json")]
    [InlineData("application/json")]
    [InlineData("application/settings+json")]
    [InlineData("text/template")]
    [InlineData("text/x-handlebars-template")]
    [InlineData("text/x-template")]
    [InlineData("speculationrules")]
    [InlineData("importmap")]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    public void DataBlocks_AreNotExtracted(string type)
    {
        var html = $"<script type=\"{type}\">{{\"a\":1}}</script><script>var ran=1;</script>";

        var result = ScriptExtractionService.ExtractAll(html);

        Assert.Equal(new[] { "var ran=1;" }, result.Scripts.ToArray());
        // Skipped entirely: no descriptor either, so nothing downstream can reach the content.
        Assert.Equal(new[] { "var ran=1;" }, result.Descriptors.Select(d => d.Content).ToArray());
        Assert.Equal(new[] { "var ran=1;" }, ScriptExtractionService.Extract(html).ToArray());
    }

    /// <summary>
    /// The shapes that must keep running: no type at all, an empty type, every JavaScript MIME
    /// essence including the legacy aliases, a type carrying parameters, and one with the
    /// surrounding whitespace an HTML author may leave in the attribute.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" type=\"\"")]
    [InlineData(" type=\"text/javascript\"")]
    [InlineData(" type=\"TEXT/JAVASCRIPT\"")]
    [InlineData(" type=\"application/javascript\"")]
    [InlineData(" type=\"application/ecmascript\"")]
    [InlineData(" type=\"text/ecmascript\"")]
    [InlineData(" type=\"text/jscript\"")]
    [InlineData(" type=\"text/javascript1.5\"")]
    [InlineData(" type=\"text/javascript; charset=utf-8\"")]
    [InlineData(" type=\" text/javascript \"")]
    public void ClassicScripts_StillExtracted(string typeAttribute)
    {
        var html = $"<script{typeAttribute}>var ran=1;</script>";

        Assert.Equal(new[] { "var ran=1;" }, ScriptExtractionService.ExtractAll(html).Scripts.ToArray());
    }

    /// <summary>
    /// Modules keep their own route: they stay out of the classic buckets (they always did) and are
    /// collected as module roots — the executable check must not drop them on the way.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void ModuleScripts_AreStillCollectedAsRoots()
    {
        const string html = "<script type=\"module\">export const a = 1;</script>";

        var result = ScriptExtractionService.ExtractAll(html);

        Assert.Empty(result.Scripts);
        Assert.Single(result.ModuleRoots);
        Assert.Equal("export const a = 1;", result.ModuleRoots[0].Source);
    }

    /// <summary>
    /// A data block's <c>src</c> is never fetched by the extraction walk, so the prefetcher must not
    /// request it either — otherwise the page makes a round trip a browser would not.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void DataBlockSources_AreNotPrefetched()
    {
        const string html =
            "<script type=\"application/ld+json\" src=\"https://example.test/a.json\"></script>" +
            "<script type=\"application/json\" src=\"https://example.test/b.json\"></script>";

        // Fewer than two *script* URLs means no prefetcher at all; before the fix these two data
        // blocks counted and one was created.
        Assert.Null(ScriptExtractionService.CreateScriptPrefetcher(html, pageUrl: null, csp: null));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("text/javascript", true)]
    [InlineData("module", true)]
    [InlineData("application/ld+json", false)]
    [InlineData("text/template", false)]
    [InlineData("speculationrules", false)]
    public void IsExecutable_ClassifiesTheType(string? type, bool executable)
        => Assert.Equal(executable, ScriptMimeType.IsExecutable(type));
}
