using System;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Unit tests for <see cref="ReadOnlyMemory{T}"/>-based text handling
/// that replaced the former SubString class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Engine", "HtmlRenderer")]
public class SubStringTests
{
    [Fact]
    public void AsMemory_FullString_CapturesEntireString()
    {
        var mem = "hello world".AsMemory();
        Assert.Equal(11, mem.Length);
        Assert.Equal("hello world", mem.ToString());
    }

    [Fact]
    public void AsMemory_Range_CapturesSubstring()
    {
        var mem = "hello world".AsMemory(6, 5);
        Assert.Equal(5, mem.Length);
        Assert.Equal("world", mem.ToString());
    }

    [Fact]
    public void Span_Indexer_ReturnsCorrectCharacter()
    {
        var mem = "abcdef".AsMemory(2, 3);
        Assert.Equal('c', mem.Span[0]);
        Assert.Equal('d', mem.Span[1]);
        Assert.Equal('e', mem.Span[2]);
    }

    [Fact]
    public void IsEmpty_ZeroLength_ReturnsTrue()
    {
        var mem = "abc".AsMemory(1, 0);
        Assert.True(mem.IsEmpty);
    }

    [Fact]
    public void IsEmpty_NonZeroLength_ReturnsFalse()
    {
        var mem = "abc".AsMemory(0, 3);
        Assert.False(mem.IsEmpty);
    }

    [Fact]
    public void Span_IsWhiteSpace_AllSpaces_ReturnsTrue()
    {
        var mem = "   ".AsMemory(0, 3);
        Assert.True(mem.Span.IsWhiteSpace());
    }

    [Fact]
    public void Span_IsWhiteSpace_Empty_ReturnsTrue()
    {
        var mem = "abc".AsMemory(0, 0);
        Assert.True(mem.Span.IsWhiteSpace());
    }

    [Fact]
    public void Span_IsWhiteSpace_MixedWhitespace_ReturnsTrue()
    {
        var mem = "  \t  ".AsMemory(0, 5);
        Assert.True(mem.Span.IsWhiteSpace());
    }

    [Fact]
    public void Span_IsWhiteSpace_NonWhitespace_ReturnsFalse()
    {
        var mem = "abc".AsMemory(0, 3);
        Assert.False(mem.Span.IsWhiteSpace());
    }

    [Fact]
    public void Slice_ExtractsCorrectly()
    {
        var mem = "hello world".AsMemory(0, 11);
        Assert.Equal("world", mem.Slice(6, 5).ToString());
    }

    [Fact]
    public void Slice_InvalidRange_Throws()
    {
        var mem = "hello".AsMemory(0, 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => mem.Slice(3, 5));
    }

    [Fact]
    public void ToString_EmptyLength_ReturnsEmpty()
    {
        var mem = "abc".AsMemory(1, 0);
        Assert.Equal(string.Empty, mem.ToString());
    }

    [Fact]
    public void AsMemory_InvalidStartIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => "abc".AsMemory(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => "abc".AsMemory(5, 1));
    }

    [Fact]
    public void AsMemory_InvalidLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => "abc".AsMemory(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => "abc".AsMemory(1, 5));
    }

    [Fact]
    public void ToString_IncludesContent()
    {
        var mem = "hello".AsMemory(0, 5);
        string str = mem.ToString();
        Assert.Contains("hello", str);
    }
}
