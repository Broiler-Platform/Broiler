using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Storage.Tests;

/// <summary>
/// Tests for <see cref="HashedString"/>.
/// </summary>
public class HashedStringTests
{
    [Fact]
    public void Constructor_String_SetsValueAndHash()
    {
        var hs = new HashedString("hello");

        Assert.Equal("hello", hs.Value.Value);
        Assert.Equal(hs.Value.GetHashCode(), hs.Hash);
    }

    [Fact]
    public void Constructor_StringSpan_SetsValueAndHash()
    {
        StringSpan span = "world";
        var hs = new HashedString(in span);

        Assert.Equal("world", hs.Value.Value);
        Assert.Equal(span.GetHashCode(), hs.Hash);
    }

    [Fact]
    public void Equality_SameValue_Equal()
    {
        HashedString a = "test";
        HashedString b = "test";

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentValue_NotEqual()
    {
        HashedString a = "foo";
        HashedString b = "bar";

        Assert.True(a != b);
        Assert.False(a == b);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void ImplicitConversion_FromString()
    {
        HashedString hs = "implicit";
        Assert.Equal("implicit", hs.ToString());
    }

    [Fact]
    public void ImplicitConversion_FromStringSpan()
    {
        StringSpan span = "span";
        HashedString hs = span;
        Assert.Equal("span", hs.ToString());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        HashedString hs = "display";
        Assert.Equal("display", hs.ToString());
    }

    [Fact]
    public void CompareTo_ReturnsZeroForSame()
    {
        HashedString a = "abc";
        HashedString b = "abc";

        Assert.Equal(0, a.CompareTo(b));
        Assert.Equal(0, a.CompareToRef(in b));
    }

    [Fact]
    public void Equals_ObjectOverload()
    {
        HashedString a = "obj";
        object b = new HashedString("obj");
        object c = "not-a-hashed-string";

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
    }
}
