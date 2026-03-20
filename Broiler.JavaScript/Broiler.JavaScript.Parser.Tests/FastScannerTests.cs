using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Parser;

namespace Broiler.JavaScript.Parser.Tests;

/// <summary>
/// Tests for <see cref="FastScanner"/> — verifies that JavaScript source
/// code is correctly tokenized.
/// </summary>
public class FastScannerTests
{
    private static FastToken[] Tokenize(string code)
    {
        var pool = new FastPool();
        var scanner = new FastScanner(pool, code);
        var tokens = new List<FastToken>();

        while (scanner.Token.Type != TokenTypes.EOF)
        {
            tokens.Add(scanner.Token);
            scanner.ConsumeToken();
        }

        tokens.Add(scanner.Token); // EOF
        return tokens.ToArray();
    }

    // ---------------------------------------------------------------
    // Basic tokens
    // ---------------------------------------------------------------

    [Fact]
    public void Tokenize_EmptyInput_ReturnsEOF()
    {
        var tokens = Tokenize("");
        Assert.Single(tokens);
        Assert.Equal(TokenTypes.EOF, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_Number()
    {
        var tokens = Tokenize("42");
        Assert.Equal(TokenTypes.Number, tokens[0].Type);
        Assert.Equal(42.0, tokens[0].Number);
    }

    [Fact]
    public void Tokenize_DecimalNumber()
    {
        var tokens = Tokenize("3.14");
        Assert.Equal(TokenTypes.Number, tokens[0].Type);
        Assert.Equal(3.14, tokens[0].Number, 2);
    }

    [Fact]
    public void Tokenize_HexNumber()
    {
        var tokens = Tokenize("0xFF");
        Assert.Equal(TokenTypes.Number, tokens[0].Type);
        Assert.Equal(255.0, tokens[0].Number);
    }

    [Fact]
    public void Tokenize_String_DoubleQuotes()
    {
        var tokens = Tokenize("\"hello\"");
        Assert.Equal(TokenTypes.String, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_String_SingleQuotes()
    {
        var tokens = Tokenize("'hello'");
        Assert.Equal(TokenTypes.String, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_Identifier()
    {
        var tokens = Tokenize("myVar");
        Assert.Equal(TokenTypes.Identifier, tokens[0].Type);
        Assert.Equal("myVar", tokens[0].Span.Value);
    }

    // ---------------------------------------------------------------
    // Keywords
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("if")]
    [InlineData("else")]
    [InlineData("for")]
    [InlineData("while")]
    [InlineData("return")]
    [InlineData("function")]
    [InlineData("var")]
    [InlineData("class")]
    [InlineData("new")]
    [InlineData("this")]
    public void Tokenize_Keywords(string keyword)
    {
        var tokens = Tokenize(keyword);
        Assert.True(tokens[0].IsKeyword, $"'{keyword}' should be recognized as a keyword");
    }

    // ---------------------------------------------------------------
    // Operators and punctuation
    // ---------------------------------------------------------------

    [Fact]
    public void Tokenize_Semicolon()
    {
        var tokens = Tokenize(";");
        Assert.Equal(TokenTypes.SemiColon, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_Brackets()
    {
        var tokens = Tokenize("()");
        Assert.Equal(TokenTypes.BracketStart, tokens[0].Type);
        Assert.Equal(TokenTypes.BracketEnd, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_CurlyBrackets()
    {
        var tokens = Tokenize("{}");
        Assert.Equal(TokenTypes.CurlyBracketStart, tokens[0].Type);
        Assert.Equal(TokenTypes.CurlyBracketEnd, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_SquareBrackets()
    {
        var tokens = Tokenize("[]");
        Assert.Equal(TokenTypes.SquareBracketStart, tokens[0].Type);
        Assert.Equal(TokenTypes.SquareBracketEnd, tokens[1].Type);
    }

    // ---------------------------------------------------------------
    // Whitespace and comments
    // ---------------------------------------------------------------

    [Fact]
    public void Tokenize_SkipsWhitespace()
    {
        var tokens = Tokenize("  42  ");
        Assert.Equal(TokenTypes.Number, tokens[0].Type);
        Assert.Equal(TokenTypes.EOF, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_SkipsSingleLineComment()
    {
        var tokens = Tokenize("// comment\n42");
        // After single-line comment, scanner may emit a line terminator token
        // before the actual value — find the number token.
        Assert.Contains(tokens, t => t.Type == TokenTypes.Number);
    }

    [Fact]
    public void Tokenize_SkipsMultiLineComment()
    {
        var tokens = Tokenize("/* comment */42");
        Assert.Equal(TokenTypes.Number, tokens[0].Type);
    }

    // ---------------------------------------------------------------
    // Source location tracking
    // ---------------------------------------------------------------

    [Fact]
    public void Tokenize_TracksLocation()
    {
        var tokens = Tokenize("x");
        Assert.Equal(1, tokens[0].Start.Line);
        Assert.Equal(1, tokens[0].Start.Column);
    }

    // ---------------------------------------------------------------
    // Multiple tokens
    // ---------------------------------------------------------------

    [Fact]
    public void Tokenize_MultipleTokens()
    {
        var tokens = Tokenize("var x = 42;");
        // var, x, =, 42, ;, EOF
        Assert.True(tokens.Length >= 5, $"Expected at least 5 tokens, got {tokens.Length}");
    }

    [Fact]
    public void Tokenize_EndsWithEOF()
    {
        var tokens = Tokenize("1 + 2");
        Assert.Equal(TokenTypes.EOF, tokens[^1].Type);
    }
}
