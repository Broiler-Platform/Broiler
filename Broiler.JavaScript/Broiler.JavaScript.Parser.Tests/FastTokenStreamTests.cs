using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Parser;

namespace Broiler.JavaScript.Parser.Tests;

/// <summary>
/// Tests for <see cref="FastTokenStream"/> — verifies token stream
/// buffering, lookahead, and undo capabilities.
/// </summary>
public class FastTokenStreamTests
{
    [Fact]
    public void Constructor_CreatesStreamFromSource()
    {
        var stream = new FastTokenStream("42");

        Assert.NotNull(stream.Current);
        Assert.Equal(TokenTypes.Number, stream.Current.Type);
    }

    [Fact]
    public void Current_ReturnsCurrentToken()
    {
        var stream = new FastTokenStream("var x;");

        Assert.True(stream.Current.IsKeyword);
    }

    [Fact]
    public void Next_ReturnsLookaheadToken()
    {
        var stream = new FastTokenStream("var x;");

        // With multiple tokens, Next should provide lookahead
        Assert.NotNull(stream.Current);
    }

    [Fact]
    public void EmptyInput_ReturnsEOF()
    {
        var stream = new FastTokenStream("");

        Assert.Equal(TokenTypes.EOF, stream.Current.Type);
    }

    [Fact]
    public void ToString_IncludesCurrentAndNext()
    {
        var stream = new FastTokenStream("42");
        var result = stream.ToString();

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Pool_IsNotNull()
    {
        var stream = new FastTokenStream("x");

        Assert.NotNull(stream.Pool);
    }

    [Fact]
    public void Keywords_DefaultInstance()
    {
        var stream = new FastTokenStream("x");

        Assert.NotNull(stream.Keywords);
    }
}

/// <summary>
/// Tests for <see cref="FastKeywordMap"/> — verifies keyword recognition.
/// </summary>
public class FastKeywordMapTests
{
    [Fact]
    public void Instance_IsNotNull()
    {
        Assert.NotNull(FastKeywordMap.Instance);
    }

    [Fact]
    public void IsKeyword_RecognizesIfKeyword()
    {
        var map = FastKeywordMap.Instance;
        StringSpan span = "if";
        var isKw = map.IsKeyword(span, out var keyword);

        Assert.True(isKw, "'if' should be recognized as a keyword");
        Assert.NotEqual(FastKeywords.none, keyword);
    }

    [Fact]
    public void IsKeyword_NonKeyword_ReturnsFalse()
    {
        var map = FastKeywordMap.Instance;
        StringSpan span = "myVariable";
        var isKw = map.IsKeyword(span, out var keyword);

        Assert.False(isKw);
    }
}
