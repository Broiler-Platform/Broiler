using Broiler.JavaScript.Ast;

namespace Broiler.JavaScript.Ast.Tests;

/// <summary>
/// Tests for <see cref="FastToken"/> construction, property access,
/// and linked-list navigation.
/// </summary>
public class FastTokenTests
{
    [Fact]
    public void Constructor_SetsTypeAndSpan()
    {
        var token = new FastToken(TokenTypes.String, "hello", start: 0, length: 5);

        Assert.Equal(TokenTypes.String, token.Type);
        Assert.Equal("hello", token.Span.Value);
    }

    [Fact]
    public void Constructor_SetsNumber()
    {
        var token = new FastToken(TokenTypes.Number, "42", start: 0, length: 2, number: 42.0);

        Assert.Equal(TokenTypes.Number, token.Type);
        Assert.Equal(42.0, token.Number);
    }

    [Fact]
    public void Constructor_SetsKeyword()
    {
        var token = new FastToken(TokenTypes.Identifier, "if", start: 0, length: 2,
            isKeyword: true, keyword: FastKeywords.@if);

        Assert.True(token.IsKeyword);
        Assert.Equal(FastKeywords.@if, token.Keyword);
    }

    [Fact]
    public void Constructor_SetsContextualKeyword()
    {
        var token = new FastToken(TokenTypes.Identifier, "async", start: 0, length: 5,
            contextualKeyword: FastKeywords.async);

        Assert.Equal(FastKeywords.async, token.ContextualKeyword);
    }

    [Fact]
    public void Constructor_SetsStartAndEndLocations()
    {
        var start = new SpanLocation(1, 5);
        var end = new SpanLocation(1, 10);
        var token = new FastToken(TokenTypes.String, "hello", start: 0, length: 5,
            startLocation: start, endLocation: end);

        Assert.Equal(1, token.Start.Line);
        Assert.Equal(5, token.Start.Column);
        Assert.Equal(1, token.End.Line);
        Assert.Equal(10, token.End.Column);
    }

    [Fact]
    public void Constructor_SetsCookedTextAndFlags()
    {
        var token = new FastToken(TokenTypes.RegExLiteral, "/abc/gi", cooked: "abc", flags: "gi",
            start: 0, length: 7);

        Assert.Equal("abc", token.CookedText);
        Assert.Equal("gi", token.Flags);
    }

    [Fact]
    public void Constructor_DefaultNumber_IsZero()
    {
        var token = new FastToken(TokenTypes.Identifier, "x", start: 0, length: 1);

        Assert.Equal(0.0, token.Number);
    }

    [Fact]
    public void Constructor_DefaultKeyword_IsNone()
    {
        var token = new FastToken(TokenTypes.Identifier, "x", start: 0, length: 1);

        Assert.False(token.IsKeyword);
        Assert.Equal(FastKeywords.none, token.Keyword);
        Assert.Equal(FastKeywords.none, token.ContextualKeyword);
    }

    [Fact]
    public void NextAndPrevious_DefaultToNull()
    {
        var token = new FastToken(TokenTypes.Number, "1", start: 0, length: 1);

        Assert.Null(token.Next);
        Assert.Null(token.Previous);
    }

    [Fact]
    public void NextAndPrevious_CanBeLinked()
    {
        var first = new FastToken(TokenTypes.Number, "1", start: 0, length: 1);
        var second = new FastToken(TokenTypes.Number, "2", start: 0, length: 1);

        first.Next = second;
        second.Previous = first;

        Assert.Same(second, first.Next);
        Assert.Same(first, second.Previous);
    }

    [Fact]
    public void AsString_ReturnsTokenTypeString()
    {
        var token = new FastToken(TokenTypes.Identifier, "myVar", start: 0, length: 5,
            contextualKeyword: FastKeywords.async);
        var strToken = token.AsString();

        Assert.Equal(TokenTypes.String, strToken.Type);
        Assert.Equal(FastKeywords.async, strToken.ContextualKeyword);
    }

    [Fact]
    public void ToString_IncludesTypeAndSpan()
    {
        var token = new FastToken(TokenTypes.Number, "42", start: 0, length: 2);
        var result = token.ToString();

        Assert.Contains("Number", result);
        Assert.Contains("42", result);
    }

    [Fact]
    public void Empty_IsNull()
    {
        Assert.Null(FastToken.Empty);
    }

    [Fact]
    public void Constructor_NullSource_ProducesDefaultSpan()
    {
        var token = new FastToken(TokenTypes.EOF);

        Assert.Equal(TokenTypes.EOF, token.Type);
    }
}
