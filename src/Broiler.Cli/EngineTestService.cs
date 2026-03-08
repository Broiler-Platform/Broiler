using Broiler.App.Rendering;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using YantraJS.Core;

namespace Broiler.Cli;

/// <summary>
/// Runs smoke tests for the embedded rendering engines (HTML-Renderer and YantraJS).
/// </summary>
public sealed class EngineTestService
{
    /// <summary>
    /// Result of an individual engine test.
    /// </summary>
    public sealed class EngineTestResult
    {
        /// <summary>Name of the engine tested.</summary>
        public required string EngineName { get; init; }

        /// <summary>Whether the test passed.</summary>
        public required bool Passed { get; init; }

        /// <summary>Error message if the test failed; <c>null</c> on success.</summary>
        public string? Error { get; init; }
    }

    /// <summary>
    /// Runs smoke tests for all embedded engines and returns results.
    /// </summary>
    public IReadOnlyList<EngineTestResult> RunAll() =>
        [
            TestHtmlRenderer(),
            TestYantraJS(),
        ];

    /// <summary>
    /// Tests the HTML-Renderer core by performing a basic CSS block
    /// parse/merge cycle using the cross-platform core library.
    /// </summary>
    public EngineTestResult TestHtmlRenderer()
    {
        try
        {
            // Create a CSS block with properties — exercises the core data model
            var properties = new Dictionary<string, string>
            {
                { "color", "red" },
                { "font-size", "14px" },
            };
            var block = new CssBlock("p", properties);

            // Verify properties are accessible
            if (block.Class != "p")
                throw new InvalidOperationException("CssBlock class mismatch.");
            if (block.Properties["color"] != "red")
                throw new InvalidOperationException("CssBlock property mismatch.");

            // Test clone and merge operations
            var clone = block.Clone();
            var overrideProps = new Dictionary<string, string> { { "color", "blue" } };
            var overrideBlock = new CssBlock("p", overrideProps);
            clone.Merge(overrideBlock);

            if (clone.Properties["color"] != "blue")
                throw new InvalidOperationException("CssBlock merge did not override property.");

            return new EngineTestResult { EngineName = "HTML-Renderer", Passed = true };
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.HtmlRenderer, "EngineTestService.TestHtmlRenderer", $"Smoke test failed: {ex.Message}", ex);
            return new EngineTestResult { EngineName = "HTML-Renderer", Passed = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Tests the YantraJS engine by executing a simple JavaScript
    /// expression and verifying the result.
    /// </summary>
    public EngineTestResult TestYantraJS()
    {
        try
        {
            using var context = new JSContext();
            var result = context.Eval("1 + 2");

            if (result is not JSNumber num || num.IntValue != 3)
                throw new InvalidOperationException($"Expected 3 but got {result}.");

            return new EngineTestResult { EngineName = "YantraJS", Passed = true };
        }
        catch (Exception ex)
        {
            RenderLogger.LogError(LogCategory.JavaScript, "EngineTestService.TestYantraJS", $"Smoke test failed: {ex.Message}", ex);
            return new EngineTestResult { EngineName = "YantraJS", Passed = false, Error = ex.Message };
        }
    }
}
