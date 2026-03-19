using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for the String built-in object.
/// Covers constructor, prototype methods, and static methods.
/// </summary>
public class StringBuiltInTests : IDisposable
{
    private readonly JSContext _context;

    public StringBuiltInTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ---------------------------------------------------------------
    // Constructor / typeof
    // ---------------------------------------------------------------

    [Fact]
    public void String_TypeofLiteral_IsString()
    {
        var result = _context.Eval("typeof 'hello'");
        Assert.Equal("string", result.ToString());
    }

    [Fact]
    public void String_Constructor_ReturnsObjectWrapper()
    {
        var result = _context.Eval("typeof new String('hello')");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void String_FunctionCall_CoercesToString()
    {
        var result = _context.Eval("String(42)");
        Assert.Equal("42", result.ToString());
    }

    // ---------------------------------------------------------------
    // Length
    // ---------------------------------------------------------------

    [Fact]
    public void String_Length_ReturnsCharCount()
    {
        var result = _context.Eval("'hello'.length");
        Assert.Equal(5d, result.DoubleValue);
    }

    [Fact]
    public void String_Length_EmptyString()
    {
        var result = _context.Eval("''.length");
        Assert.Equal(0d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // charAt / charCodeAt
    // ---------------------------------------------------------------

    [Fact]
    public void String_CharAt_ReturnsCharacter()
    {
        var result = _context.Eval("'abc'.charAt(1)");
        Assert.Equal("b", result.ToString());
    }

    [Fact]
    public void String_CharCodeAt_ReturnsCode()
    {
        var result = _context.Eval("'A'.charCodeAt(0)");
        Assert.Equal(65d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // indexOf / lastIndexOf / includes
    // ---------------------------------------------------------------

    [Fact]
    public void String_IndexOf_FindsSubstring()
    {
        var result = _context.Eval("'hello world'.indexOf('world')");
        Assert.Equal(6d, result.DoubleValue);
    }

    [Fact]
    public void String_IndexOf_ReturnsMinusOneWhenNotFound()
    {
        var result = _context.Eval("'hello'.indexOf('xyz')");
        Assert.Equal(-1d, result.DoubleValue);
    }

    [Fact]
    public void String_LastIndexOf_FindsLastOccurrence()
    {
        var result = _context.Eval("'abcabc'.lastIndexOf('abc')");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void String_Includes_ReturnsTrue()
    {
        var result = _context.Eval("'hello world'.includes('world')");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void String_Includes_ReturnsFalse()
    {
        var result = _context.Eval("'hello'.includes('xyz')");
        Assert.False(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // startsWith / endsWith
    // ---------------------------------------------------------------

    [Fact]
    public void String_StartsWith_True()
    {
        var result = _context.Eval("'hello world'.startsWith('hello')");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void String_StartsWith_False()
    {
        var result = _context.Eval("'hello world'.startsWith('world')");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void String_EndsWith_True()
    {
        var result = _context.Eval("'hello world'.endsWith('world')");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void String_EndsWith_False()
    {
        var result = _context.Eval("'hello world'.endsWith('hello')");
        Assert.False(result.BooleanValue);
    }

    // ---------------------------------------------------------------
    // slice / substring
    // ---------------------------------------------------------------

    [Fact]
    public void String_Slice_ExtractsSubstring()
    {
        var result = _context.Eval("'hello world'.slice(0, 5)");
        Assert.Equal("hello", result.ToString());
    }

    [Fact]
    public void String_Slice_NegativeIndex()
    {
        var result = _context.Eval("'hello'.slice(-3)");
        Assert.Equal("llo", result.ToString());
    }

    [Fact]
    public void String_Substring_ExtractsSubstring()
    {
        var result = _context.Eval("'hello world'.substring(6)");
        Assert.Equal("world", result.ToString());
    }

    // ---------------------------------------------------------------
    // toUpperCase / toLowerCase
    // ---------------------------------------------------------------

    [Fact]
    public void String_ToUpperCase()
    {
        var result = _context.Eval("'hello'.toUpperCase()");
        Assert.Equal("HELLO", result.ToString());
    }

    [Fact]
    public void String_ToLowerCase()
    {
        var result = _context.Eval("'HELLO'.toLowerCase()");
        Assert.Equal("hello", result.ToString());
    }

    // ---------------------------------------------------------------
    // trim / trimStart / trimEnd
    // ---------------------------------------------------------------

    [Fact]
    public void String_Trim_RemovesWhitespace()
    {
        var result = _context.Eval("'  hello  '.trim()");
        Assert.Equal("hello", result.ToString());
    }

    [Fact]
    public void String_TrimStart_RemovesLeading()
    {
        var result = _context.Eval("'  hello  '.trimStart()");
        Assert.Equal("hello  ", result.ToString());
    }

    [Fact]
    public void String_TrimEnd_RemovesTrailing()
    {
        var result = _context.Eval("'  hello  '.trimEnd()");
        Assert.Equal("  hello", result.ToString());
    }

    // ---------------------------------------------------------------
    // split
    // ---------------------------------------------------------------

    [Fact]
    public void String_Split_ByDelimiter()
    {
        var result = _context.Eval("'a,b,c'.split(',').length");
        Assert.Equal(3d, result.DoubleValue);
    }

    [Fact]
    public void String_Split_FirstElement()
    {
        var result = _context.Eval("'a-b-c'.split('-')[1]");
        Assert.Equal("b", result.ToString());
    }

    // ---------------------------------------------------------------
    // replace / replaceAll
    // ---------------------------------------------------------------

    [Fact]
    public void String_Replace_FirstOccurrence()
    {
        var result = _context.Eval("'aabbcc'.replace('bb', 'XX')");
        Assert.Equal("aaXXcc", result.ToString());
    }

    // ---------------------------------------------------------------
    // repeat / padStart / padEnd
    // ---------------------------------------------------------------

    [Fact]
    public void String_Repeat_RepeatsString()
    {
        var result = _context.Eval("'ab'.repeat(3)");
        Assert.Equal("ababab", result.ToString());
    }

    [Fact]
    public void String_PadStart_PadsToLength()
    {
        var result = _context.Eval("'5'.padStart(3, '0')");
        Assert.Equal("005", result.ToString());
    }

    [Fact]
    public void String_PadEnd_PadsToLength()
    {
        var result = _context.Eval("'5'.padEnd(3, '0')");
        Assert.Equal("500", result.ToString());
    }

    // ---------------------------------------------------------------
    // concat
    // ---------------------------------------------------------------

    [Fact]
    public void String_Concat_JoinsStrings()
    {
        var result = _context.Eval("'hello'.concat(' ', 'world')");
        Assert.Equal("hello world", result.ToString());
    }

    // ---------------------------------------------------------------
    // toString / valueOf
    // ---------------------------------------------------------------

    [Fact]
    public void String_ToString_ReturnsItself()
    {
        var result = _context.Eval("'test'.toString()");
        Assert.Equal("test", result.ToString());
    }

    [Fact]
    public void String_ValueOf_ReturnsItself()
    {
        var result = _context.Eval("'test'.valueOf()");
        Assert.Equal("test", result.ToString());
    }

    // ---------------------------------------------------------------
    // Template literals
    // ---------------------------------------------------------------

    [Fact]
    public void String_TemplateLiteral_Interpolation()
    {
        var result = _context.Eval("var x = 42; `value=${x}`");
        Assert.Equal("value=42", result.ToString());
    }

    [Fact]
    public void String_TemplateLiteral_Expression()
    {
        var result = _context.Eval("`sum=${1+2}`");
        Assert.Equal("sum=3", result.ToString());
    }

    // ---------------------------------------------------------------
    // Static methods
    // ---------------------------------------------------------------

    [Fact]
    public void String_FromCharCode_CreatesString()
    {
        var result = _context.Eval("String.fromCharCode(72, 101, 108)");
        Assert.Equal("Hel", result.ToString());
    }

    // ---------------------------------------------------------------
    // Search / Match
    // ---------------------------------------------------------------

    [Fact]
    public void String_Search_FindsPattern()
    {
        var result = _context.Eval("'hello world'.search(/world/)");
        Assert.Equal(6d, result.DoubleValue);
    }

    [Fact]
    public void String_Match_ReturnsMatch()
    {
        var result = _context.Eval("'abc123'.match(/\\d+/)[0]");
        Assert.Equal("123", result.ToString());
    }

}
