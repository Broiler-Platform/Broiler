using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class HtmlTokenizerTests
{
    private readonly HtmlTokenizer _tokenizer = new();

    [Fact]
    public void Tokenize_SimpleDivTag_ReturnsStartAndEndTag()
    {
        var tokens = _tokenizer.Tokenize("<div></div>").ToList();

        Assert.Equal(TokenType.StartTag, tokens[0].Type);
        Assert.Equal("div", tokens[0].Name);
        Assert.Equal(TokenType.EndTag, tokens[1].Type);
        Assert.Equal("div", tokens[1].Name);
        Assert.Equal(TokenType.EndOfFile, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_SelfClosingBr_ReturnsSelfClosingStartTag()
    {
        var tokens = _tokenizer.Tokenize("<br/>").ToList();

        Assert.Equal(TokenType.StartTag, tokens[0].Type);
        Assert.Equal("br", tokens[0].Name);
        Assert.True(tokens[0].SelfClosing);
    }

    [Fact]
    public void Tokenize_DoubleQuotedAttribute_ParsesCorrectly()
    {
        var tokens = _tokenizer.Tokenize("<div id=\"main\"></div>").ToList();

        Assert.Equal("main", tokens[0].Attributes["id"]);
    }

    [Fact]
    public void Tokenize_SingleQuotedAttribute_ParsesCorrectly()
    {
        var tokens = _tokenizer.Tokenize("<div id='main'></div>").ToList();

        Assert.Equal("main", tokens[0].Attributes["id"]);
    }

    [Fact]
    public void Tokenize_UnquotedAttribute_ParsesCorrectly()
    {
        var tokens = _tokenizer.Tokenize("<div id=main></div>").ToList();

        Assert.Equal("main", tokens[0].Attributes["id"]);
    }

    [Fact]
    public void Tokenize_BooleanAttribute_ParsesCorrectly()
    {
        var tokens = _tokenizer.Tokenize("<input disabled>").ToList();

        Assert.True(tokens[0].Attributes.ContainsKey("disabled"));
    }

    [Fact]
    public void Tokenize_CharacterData_ReturnsCharacterToken()
    {
        var tokens = _tokenizer.Tokenize("<p>Hello</p>").ToList();

        Assert.Equal(TokenType.StartTag, tokens[0].Type);
        Assert.Equal(TokenType.Character, tokens[1].Type);
        Assert.Equal("Hello", tokens[1].Data);
        Assert.Equal(TokenType.EndTag, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_Comment_ReturnsCommentToken()
    {
        var tokens = _tokenizer.Tokenize("<!-- a comment -->").ToList();

        Assert.Equal(TokenType.Comment, tokens[0].Type);
        Assert.Equal(" a comment ", tokens[0].Data);
    }

    [Fact]
    public void Tokenize_Doctype_ReturnsDoctypeToken()
    {
        var tokens = _tokenizer.Tokenize("<!DOCTYPE html>").ToList();

        Assert.Equal(TokenType.Doctype, tokens[0].Type);
        Assert.Equal("html", tokens[0].Name);
    }

    [Fact]
    public void Tokenize_VoidElement_NoClosingTagNeeded()
    {
        var tokens = _tokenizer.Tokenize("<img><br>").ToList();

        Assert.Equal(TokenType.StartTag, tokens[0].Type);
        Assert.Equal("img", tokens[0].Name);
        Assert.Equal(TokenType.StartTag, tokens[1].Type);
        Assert.Equal("br", tokens[1].Name);
        Assert.Equal(TokenType.EndOfFile, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_EmptyInput_ReturnsOnlyEndOfFile()
    {
        var tokens = _tokenizer.Tokenize("").ToList();

        Assert.Single(tokens);
        Assert.Equal(TokenType.EndOfFile, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_NestedElements_ReturnsCorrectSequence()
    {
        var tokens = _tokenizer.Tokenize("<div><span>Hi</span></div>").ToList();

        Assert.Equal(TokenType.StartTag, tokens[0].Type);
        Assert.Equal("div", tokens[0].Name);
        Assert.Equal(TokenType.StartTag, tokens[1].Type);
        Assert.Equal("span", tokens[1].Name);
        Assert.Equal(TokenType.Character, tokens[2].Type);
        Assert.Equal("Hi", tokens[2].Data);
        Assert.Equal(TokenType.EndTag, tokens[3].Type);
        Assert.Equal("span", tokens[3].Name);
        Assert.Equal(TokenType.EndTag, tokens[4].Type);
        Assert.Equal("div", tokens[4].Name);
        Assert.Equal(TokenType.EndOfFile, tokens[5].Type);
    }

    [Fact]
    public void Tokenize_MultipleAttributes_AllParsed()
    {
        var tokens = _tokenizer.Tokenize("<div id=\"a\" class=\"b\" style=\"color:red\"></div>").ToList();

        Assert.Equal("a", tokens[0].Attributes["id"]);
        Assert.Equal("b", tokens[0].Attributes["class"]);
        Assert.Equal("color:red", tokens[0].Attributes["style"]);
    }
}
