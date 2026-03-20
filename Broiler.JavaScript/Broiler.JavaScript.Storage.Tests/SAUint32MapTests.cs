using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Storage.Tests;

/// <summary>
/// Tests for <see cref="SAUint32Map{T}"/>.
/// </summary>
public class SAUint32MapTests
{
    [Fact]
    public void Default_IsNull()
    {
        SAUint32Map<int> map = default;
        Assert.True(map.IsNull);
    }

    [Fact]
    public void Save_And_Retrieve()
    {
        var map = new SAUint32Map<string>();
        map.Save(1, "one");
        map.Save(2, "two");

        Assert.Equal("one", map[1]);
        Assert.Equal("two", map[2]);
    }

    [Fact]
    public void HasKey_ReturnsTrueForExisting()
    {
        var map = new SAUint32Map<int>();
        map.Save(42, 100);

        Assert.True(map.HasKey(42));
        Assert.False(map.HasKey(43));
    }

    [Fact]
    public void TryGetValue_ReturnsTrueForExisting()
    {
        var map = new SAUint32Map<string>();
        map.Save(5, "five");

        Assert.True(map.TryGetValue(5, out var value));
        Assert.Equal("five", value);

        Assert.False(map.TryGetValue(99, out _));
    }

    [Fact]
    public void TryRemove_RemovesExisting()
    {
        var map = new SAUint32Map<int>();
        map.Save(10, 100);

        Assert.True(map.TryRemove(10, out var removed));
        Assert.Equal(100, removed);
        Assert.False(map.HasKey(10));
    }

    [Fact]
    public void TryRemove_ReturnsFalseForMissing()
    {
        var map = new SAUint32Map<int>();

        Assert.False(map.TryRemove(99, out _));
    }

    [Fact]
    public void RemoveAt_RemovesKey()
    {
        var map = new SAUint32Map<int>();
        map.Save(7, 70);

        Assert.True(map.RemoveAt(7));
        Assert.False(map.HasKey(7));
    }

    [Fact]
    public void RemoveAt_ReturnsFalseForMissing()
    {
        var map = new SAUint32Map<int>();
        Assert.False(map.RemoveAt(99));
    }

    [Fact]
    public void Put_CreatesRefToSlot()
    {
        var map = new SAUint32Map<int>();
        ref var slot = ref map.Put(3);
        slot = 300;

        Assert.Equal(300, map[3]);
    }

    [Fact]
    public void Save_OverwritesExisting()
    {
        var map = new SAUint32Map<string>();
        map.Save(1, "first");
        map.Save(1, "second");

        Assert.Equal("second", map[1]);
    }

    [Fact]
    public void AllValues_ReturnsAllEntries()
    {
        var map = new SAUint32Map<string>();
        map.Save(1, "a");
        map.Save(5, "b");
        map.Save(10, "c");

        var all = map.AllValues().ToList();
        Assert.Equal(3, all.Count);

        Assert.Contains(all, x => x.Key == 1 && x.Value == "a");
        Assert.Contains(all, x => x.Key == 5 && x.Value == "b");
        Assert.Contains(all, x => x.Key == 10 && x.Value == "c");
    }

    [Fact]
    public void Resize_ExpandsCapacity()
    {
        var map = new SAUint32Map<int>();
        map.Save(0, 1);
        map.Resize(100);

        // Original value should still be accessible
        Assert.Equal(1, map[0]);
    }
}
