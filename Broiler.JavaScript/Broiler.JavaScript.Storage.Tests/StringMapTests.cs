using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Storage.Tests;

/// <summary>
/// Tests for <see cref="StringMap{T}"/>.
/// </summary>
public class StringMapTests
{
    [Fact]
    public void Default_IsNull()
    {
        StringMap<int> map = default;
        Assert.True(map.IsNull);
    }

    [Fact]
    public void Save_And_TryGetValue()
    {
        var map = new StringMap<int>();
        HashedString key = "hello";
        map.Save(in key, 42);

        Assert.True(map.TryGetValue("hello", out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_ReturnsFalseForMissing()
    {
        var map = new StringMap<int>();

        Assert.False(map.TryGetValue("missing", out _));
    }

    [Fact]
    public void HasKey_ReturnsTrueForExisting()
    {
        var map = new StringMap<string>();
        HashedString key = "key1";
        map.Save(in key, "value1");

        Assert.True(map.HasKey("key1"));
        Assert.False(map.HasKey("key2"));
    }

    [Fact]
    public void TryRemove_RemovesExisting()
    {
        var map = new StringMap<int>();
        HashedString key = "remove-me";
        map.Save(in key, 99);

        Assert.True(map.TryRemove("remove-me", out var removed));
        Assert.Equal(99, removed);
        Assert.False(map.HasKey("remove-me"));
    }

    [Fact]
    public void RemoveAt_RemovesKey()
    {
        var map = new StringMap<int>();
        HashedString key = "gone";
        map.Save(in key, 1);

        Assert.True(map.RemoveAt("gone"));
        Assert.False(map.HasKey("gone"));
    }

    [Fact]
    public void Put_CreatesRefToSlot()
    {
        var map = new StringMap<int>();
        HashedString key = "ref-test";
        ref var slot = ref map.Put(in key);
        slot = 777;

        Assert.True(map.TryGetValue("ref-test", out var value));
        Assert.Equal(777, value);
    }

    [Fact]
    public void AllValues_ReturnsAllEntries()
    {
        var map = new StringMap<int>();
        HashedString k1 = "a";
        HashedString k2 = "b";
        HashedString k3 = "c";
        map.Save(in k1, 1);
        map.Save(in k2, 2);
        map.Save(in k3, 3);

        var all = map.AllValues().ToList();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void TryGetValue_HashedStringOverload()
    {
        var map = new StringMap<string>();
        HashedString key = "hashed";
        map.Save(in key, "value");

        Assert.True(map.TryGetValue(in key, out var value));
        Assert.Equal("value", value);
    }
}
