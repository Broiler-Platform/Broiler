using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// The CSSOM <c>CSS</c> namespace object — <c>CSS.supports()</c> and <c>CSS.escape()</c>.
/// <para>
/// The object did not exist, and its absence is not the mild kind: an unqualified
/// <c>CSS.supports(…)</c> is a <c>ReferenceError</c>, which aborts the whole calling script rather
/// than the one line. google.com's main bundle calls it unguarded, and it is where that bundle
/// stopped once <c>AbortSignal</c> let it get that far.
/// </para>
/// </summary>
/// <remarks>
/// <c>supports()</c> answers from the CSS engine's own <c>@supports</c> evaluator, which lives in
/// the <c>Broiler.CSS</c> submodule and arrives as a patch a maintainer applies (this session
/// cannot push there). So these tests run against two supported states and say which is which:
/// the negative answers and all of <c>escape()</c> hold either way, while the truthful positive
/// answers are asserted only where the evaluator is actually present. <see cref="EvaluatorPresent"/>
/// probes for it the same way the binding does.
/// </remarks>
public sealed class CssBindingTests
{
    /// <summary>
    /// Whether the <c>Broiler.CSS</c> evaluator this binding defers to is in the build — that is,
    /// whether the pending patch has been applied (or landed and had its pointer bumped).
    /// </summary>
    private static bool EvaluatorPresent { get; } =
        typeof(CSS.Dom.CssStyleEngine).GetMethod(
            "EvaluatesSupportsCondition",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null) is not null;

    private static (JSContext Context, DomBridge Bridge) Attach()
    {
        var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, "<!doctype html><html><body></body></html>", "https://example.com/");
        return (context, bridge);
    }

    private static string Eval(JSContext context, string expression) =>
        context.Eval($"String({expression})").ToString();

    /// <summary>The reported failure: the name has to resolve, on the global and on window.</summary>
    [Fact]
    public void TheNamespaceObjectExists()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("object", Eval(context, "typeof CSS"));
        Assert.Equal("object", Eval(context, "typeof window.CSS"));
        Assert.Equal("function", Eval(context, "typeof CSS.supports"));
        Assert.Equal("function", Eval(context, "typeof CSS.escape"));
    }

    /// <summary>
    /// A query the engine can refute is false however the answer is reached, so these hold with or
    /// without the evaluator. They are also the cases that rule out the tempting implementation:
    /// round-tripping through a detached element's <c>style</c> would answer true to both, because
    /// Broiler's CSSOM stores declarations without validating them.
    /// </summary>
    [Theory]
    [InlineData("CSS.supports('totally-bogus-prop', '1')")]
    [InlineData("CSS.supports('color', 'rainbow')")]
    [InlineData("CSS.supports('(totally-bogus-prop: 1)')")]
    public void UnsupportedQueriesAreFalse(string expression)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("false", Eval(context, expression));
    }

    /// <summary><c>CSS.supports()</c> never throws — a malformed or missing condition is just false.</summary>
    [Theory]
    [InlineData("CSS.supports('malformed')")]
    [InlineData("CSS.supports()")]
    [InlineData("CSS.supports('')")]
    [InlineData("CSS.supports('(display: grid)) and')")]
    public void MalformedConditionsAreFalseRatherThanAnError(string expression)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("false", Eval(context, expression));
    }

    /// <summary>
    /// A feature the engine recognises. With the evaluator this is true; without it the binding
    /// reports false — the conservative direction, where a page uses the fallback it already
    /// carries rather than committing to something nothing will render.
    /// </summary>
    [Theory]
    [InlineData("CSS.supports('display', 'grid')")]
    [InlineData("CSS.supports('color', 'red')")]
    [InlineData("CSS.supports('(display: grid)')")]
    [InlineData("CSS.supports('(display: grid) and (color: red)')")]
    [InlineData("CSS.supports('not (totally-bogus-prop: 1)')")]
    public void SupportedQueriesAreTrueOnceTheEvaluatorIsPresent(string expression)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(EvaluatorPresent ? "true" : "false", Eval(context, expression));
    }

    /// <summary>
    /// The two-argument form must not re-parse its value as part of the condition: a value carrying
    /// its own parentheses — <c>linear(0, 1)</c>, which is exactly what google.com asks about —
    /// would otherwise close the query early and be answered on a truncated string.
    /// </summary>
    [Fact]
    public void TheTwoArgumentFormAcceptsAValueContainingParentheses()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(
            EvaluatorPresent ? "true" : "false",
            Eval(context, "CSS.supports('animation-timing-function', 'linear(0, 1)')"));
    }

    /// <summary>
    /// <c>CSS.escape()</c> is a pure algorithm (CSSOM §2) with no engine dependency, so it is exact
    /// in either state — including the parts that are easy to get wrong: a leading digit becomes a
    /// hexadecimal escape *with* its trailing space, a leading digit after a hyphen does too, a lone
    /// hyphen is escaped while <c>--</c> is not, and NULL becomes the replacement character rather
    /// than being escaped or dropped.
    /// </summary>
    [Theory]
    [InlineData("foo bar", @"foo\ bar")]
    [InlineData(".foo#bar", @"\.foo\#bar")]
    [InlineData("1a", @"\31 a")]
    [InlineData("-1a", @"-\31 a")]
    [InlineData("--x", "--x")]
    [InlineData("-", @"\-")]
    [InlineData("-a", "-a")]
    [InlineData("a_b-c", "a_b-c")]
    [InlineData("", "")]
    [InlineData("hello", "hello")]
    [InlineData("été", "été")]
    public void EscapeFollowsTheCssomAlgorithm(string input, string expected)
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal(expected, context.Eval($"CSS.escape({Quote(input)})").ToString());
    }

    /// <summary>NULL is replaced, not escaped — the one substitution in the algorithm.</summary>
    [Fact]
    public void EscapeReplacesNull()
    {
        var (context, bridge) = Attach();
        using var _ = context;
        using var __ = bridge;

        Assert.Equal("a�b", context.Eval("CSS.escape('a\\u0000b')").ToString());
    }

    private static string Quote(string value) =>
        "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
}
