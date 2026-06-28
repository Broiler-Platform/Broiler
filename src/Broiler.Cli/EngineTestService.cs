using Broiler.CSS;
using Broiler.HtmlBridge;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Engine;

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
    /// Tests the renderer CSS dependency through the canonical shared model.
    /// </summary>
    public EngineTestResult TestHtmlRenderer()
    {
        try
        {
            var sheet = new CssParser().ParseStyleSheet(
                "p { color: red; font-size: 14px; color: blue; }");
            if (sheet.Rules.Count != 1 || sheet.Rules[0] is not CssStyleRule rule)
                throw new InvalidOperationException("Shared CSS rule parsing failed.");
            if (rule.Declarations.GetPropertyValue("color") != "blue")
                throw new InvalidOperationException("Shared CSS declaration precedence failed.");

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
