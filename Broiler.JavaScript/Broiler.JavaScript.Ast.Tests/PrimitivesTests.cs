using Broiler.JavaScript.Ast;

namespace Broiler.JavaScript.Ast.Tests;

/// <summary>
/// Tests for <see cref="SpanLocation"/> construction and formatting.
/// </summary>
public class SpanLocationTests
{
    [Fact]
    public void Constructor_SetsLineAndColumn()
    {
        var loc = new SpanLocation(10, 25);

        Assert.Equal(10, loc.Line);
        Assert.Equal(25, loc.Column);
    }

    [Fact]
    public void Default_HasZeroLineAndColumn()
    {
        var loc = default(SpanLocation);

        Assert.Equal(0, loc.Line);
        Assert.Equal(0, loc.Column);
    }

    [Fact]
    public void ToString_FormatsLineAndColumn()
    {
        var loc = new SpanLocation(3, 7);

        Assert.Equal("3, 7", loc.ToString());
    }
}

/// <summary>
/// Tests for <see cref="FastNodeType"/> enum values.
/// </summary>
public class FastNodeTypeTests
{
    [Theory]
    [InlineData(FastNodeType.Program)]
    [InlineData(FastNodeType.Block)]
    [InlineData(FastNodeType.BinaryExpression)]
    [InlineData(FastNodeType.VariableDeclaration)]
    [InlineData(FastNodeType.FunctionExpression)]
    [InlineData(FastNodeType.Identifier)]
    [InlineData(FastNodeType.Literal)]
    [InlineData(FastNodeType.IfStatement)]
    [InlineData(FastNodeType.ForStatement)]
    [InlineData(FastNodeType.WhileStatement)]
    public void NodeType_IsDefined(FastNodeType nodeType)
    {
        Assert.True(Enum.IsDefined(typeof(FastNodeType), nodeType));
    }

    [Fact]
    public void Node_IsDefaultValue()
    {
        Assert.Equal(FastNodeType.Node, default(FastNodeType));
    }
}

/// <summary>
/// Tests for <see cref="TokenTypes"/> enum values.
/// </summary>
public class TokenTypesTests
{
    [Theory]
    [InlineData(TokenTypes.Number)]
    [InlineData(TokenTypes.String)]
    [InlineData(TokenTypes.Identifier)]
    [InlineData(TokenTypes.RegExLiteral)]
    [InlineData(TokenTypes.EOF)]
    [InlineData(TokenTypes.BracketStart)]
    [InlineData(TokenTypes.BracketEnd)]
    [InlineData(TokenTypes.SemiColon)]
    public void TokenType_IsDefined(TokenTypes tokenType)
    {
        Assert.True(Enum.IsDefined(typeof(TokenTypes), tokenType));
    }
}

/// <summary>
/// Tests for <see cref="FastKeywords"/> enum values.
/// </summary>
public class FastKeywordsTests
{
    [Theory]
    [InlineData(FastKeywords.@if)]
    [InlineData(FastKeywords.@else)]
    [InlineData(FastKeywords.@for)]
    [InlineData(FastKeywords.@while)]
    [InlineData(FastKeywords.@return)]
    [InlineData(FastKeywords.@var)]
    [InlineData(FastKeywords.@const)]
    [InlineData(FastKeywords.let)]
    [InlineData(FastKeywords.async)]
    [InlineData(FastKeywords.await)]
    [InlineData(FastKeywords.@class)]
    [InlineData(FastKeywords.function)]
    public void Keyword_IsDefined(FastKeywords keyword)
    {
        Assert.True(Enum.IsDefined(typeof(FastKeywords), keyword));
    }

    [Fact]
    public void None_IsDefaultValue()
    {
        Assert.Equal(FastKeywords.none, default(FastKeywords));
    }
}
