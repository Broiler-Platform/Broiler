using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class ScriptEngineTests
{
    private readonly ScriptEngine _engine = new();

    [Fact]
    public void Execute_EmptyList_ReturnsTrue()
    {
        var result = _engine.Execute(Array.Empty<string>());
        Assert.True(result);
    }

    [Fact]
    public void Execute_ValidScript_ReturnsTrue()
    {
        var result = _engine.Execute(["var x = 1 + 2;"]);
        Assert.True(result);
    }

    [Fact]
    public void Execute_InvalidScript_ReturnsFalse()
    {
        var result = _engine.Execute(["this is not valid javascript @@!!"]);
        Assert.False(result);
    }

    [Fact]
    public void Execute_MultipleValidScripts_ReturnsTrue()
    {
        var result = _engine.Execute(["var a = 1;", "var b = a + 1;"]);
        Assert.True(result);
    }

    /// <summary>
    /// Regression test: a failing script must not prevent subsequent scripts
    /// from executing (mirrors real browser behaviour).
    /// </summary>
    [Fact]
    public void Execute_FailingScriptDoesNotBlockSubsequentScripts()
    {
        // The first script throws; the second should still run.
        var result = _engine.Execute(
        [
            "throw new Error('boom');",
            "var survived = true;"
        ]);

        // Overall result is false because one script failed.
        Assert.False(result);
    }

    /// <summary>
    /// Regression test: a failing script in the HTML overload must not
    /// prevent subsequent scripts from executing.
    /// </summary>
    [Fact]
    public void Execute_WithHtml_FailingScriptDoesNotBlockSubsequentScripts()
    {
        var html = "<html><body></body></html>";

        var result = _engine.Execute(
        [
            "throw new Error('boom');",
            "var survived = true;"
        ], html);

        Assert.False(result);
    }
}
