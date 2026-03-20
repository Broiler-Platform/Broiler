using Broiler.JavaScript.Ast;

namespace Broiler.JavaScript.Ast.Tests;

/// <summary>
/// Tests for AST node types — construction, type identification,
/// and the <see cref="AstNode.Code"/> property.
/// </summary>
public class AstNodeTests
{
    private static FastToken MakeToken(TokenTypes type, string source, int start, int length)
    {
        return new FastToken(type, source, start: start, length: length,
            startLocation: new SpanLocation(1, start + 1),
            endLocation: new SpanLocation(1, start + length + 1));
    }

    [Fact]
    public void AstLiteral_HasCorrectNodeType()
    {
        var token = MakeToken(TokenTypes.Number, "42", 0, 2);
        var node = new AstLiteral(TokenTypes.Number, token);

        Assert.Equal(FastNodeType.Literal, node.Type);
        Assert.Equal(TokenTypes.Number, node.TokenType);
        Assert.False(node.IsStatement);
    }

    [Fact]
    public void AstLiteral_NumericValue()
    {
        var token = new FastToken(TokenTypes.Number, "42", start: 0, length: 2, number: 42.0);
        var node = new AstLiteral(TokenTypes.Number, token);

        Assert.Equal(42.0, node.NumericValue);
    }

    [Fact]
    public void AstLiteral_StringValue()
    {
        var token = new FastToken(TokenTypes.String, "\"hello\"", cooked: "hello", start: 0, length: 7);
        var node = new AstLiteral(TokenTypes.String, token);

        Assert.Equal("hello", node.StringValue);
    }

    [Fact]
    public void AstIdentifier_HasCorrectNodeType()
    {
        var token = MakeToken(TokenTypes.Identifier, "myVar", 0, 5);
        var node = new AstIdentifier(token);

        Assert.Equal(FastNodeType.Identifier, node.Type);
        Assert.Equal("myVar", node.Name.Value);
    }

    [Fact]
    public void AstIdentifier_WithStringId()
    {
        var token = MakeToken(TokenTypes.Identifier, "x", 0, 1);
        var node = new AstIdentifier(token, "customId");

        Assert.Equal("customId", node.Name.Value);
    }

    [Fact]
    public void AstExpressionStatement_HasCorrectNodeType()
    {
        var token = MakeToken(TokenTypes.Number, "42", 0, 2);
        var literal = new AstLiteral(TokenTypes.Number, token);
        var node = new AstExpressionStatement(literal);

        Assert.Equal(FastNodeType.ExpressionStatement, node.Type);
        Assert.Same(literal, node.Expression);
    }

    [Fact]
    public void AstReturnStatement_HasCorrectNodeType()
    {
        var source = "return 42;";
        var startToken = MakeToken(TokenTypes.Identifier, source, 0, 6);
        var endToken = MakeToken(TokenTypes.SemiColon, source, 9, 1);
        var node = new AstReturnStatement(startToken, endToken);

        Assert.Equal(FastNodeType.ReturnStatement, node.Type);
        Assert.True(node.IsStatement);
        Assert.Null(node.Argument);
    }

    [Fact]
    public void AstReturnStatement_WithArgument()
    {
        var source = "return 42;";
        var startToken = MakeToken(TokenTypes.Identifier, source, 0, 6);
        var endToken = MakeToken(TokenTypes.SemiColon, source, 9, 1);
        var argToken = MakeToken(TokenTypes.Number, source, 7, 2);
        var arg = new AstLiteral(TokenTypes.Number, argToken);
        var node = new AstReturnStatement(startToken, endToken, arg);

        Assert.NotNull(node.Argument);
        Assert.Same(arg, node.Argument);
    }

    [Fact]
    public void AstNode_StartAndEnd_ArePreserved()
    {
        var token = MakeToken(TokenTypes.Number, "42", 0, 2);
        var node = new AstLiteral(TokenTypes.Number, token);

        Assert.Same(token, node.Start);
        Assert.Same(token, node.End);
    }

    [Fact]
    public void AstNode_Code_SpansStartToEnd()
    {
        var source = "return 42;";
        var startToken = MakeToken(TokenTypes.Identifier, source, 0, 6);
        var endToken = MakeToken(TokenTypes.SemiColon, source, 9, 1);
        var node = new AstReturnStatement(startToken, endToken);

        var code = node.Code;
        Assert.Equal(source, code.Value);
    }
}

