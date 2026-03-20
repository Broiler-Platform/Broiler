using Broiler.JavaScript.Ast;

namespace Broiler.JavaScript.Ast.Tests;

/// <summary>
/// Tests for <see cref="StringSpan"/> construction, equality,
/// boundary validation, and value extraction.
/// </summary>
public class StringSpanTests
{
    [Fact]
    public void Constructor_String_SetsProperties()
    {
        var span = new StringSpan("hello");

        Assert.Equal("hello", span.Source);
        Assert.Equal(0, span.Offset);
        Assert.Equal(5, span.Length);
    }

    [Fact]
    public void Constructor_WithOffsetAndLength_SetsProperties()
    {
        var span = new StringSpan("hello world", 6, 5);

        Assert.Equal("hello world", span.Source);
        Assert.Equal(6, span.Offset);
        Assert.Equal(5, span.Length);
        Assert.Equal("world", span.Value);
    }

    [Fact]
    public void Constructor_InvalidOffset_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new StringSpan("hello", 10, 1));
    }

    [Fact]
    public void Constructor_InvalidLength_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new StringSpan("hello", 3, 10));
    }

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new StringSpan(null, 0, 1));
    }

    [Fact]
    public void Empty_HasZeroLength()
    {
        var empty = StringSpan.Empty;

        Assert.Equal(0, empty.Length);
    }

    [Fact]
    public void ImplicitConversion_FromString()
    {
        StringSpan span = "test";

        Assert.Equal("test", span.Value);
        Assert.Equal(4, span.Length);
    }

    [Fact]
    public void Equals_SameContent_ReturnsTrue()
    {
        var a = new StringSpan("hello");
        var b = new StringSpan("hello");

        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentContent_ReturnsFalse()
    {
        var a = new StringSpan("hello");
        var b = new StringSpan("world");

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_String_Overload()
    {
        var span = new StringSpan("test");

        Assert.True(span.Equals("test"));
        Assert.False(span.Equals("other"));
    }

    [Fact]
    public void IsEmpty_ForEmptySpan()
    {
        var span = new StringSpan("");

        Assert.True(span.IsEmpty);
    }

    [Fact]
    public void IsEmpty_ForNonEmptySpan()
    {
        var span = new StringSpan("x");

        Assert.False(span.IsEmpty);
    }

    [Fact]
    public void Value_ExtractsSubstring()
    {
        var span = new StringSpan("hello world", 0, 5);

        Assert.Equal("hello", span.Value);
    }

    [Fact]
    public void GetHashCode_ConsistentForSameContent()
    {
        var a = new StringSpan("test");
        var b = new StringSpan("test");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
