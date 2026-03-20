using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Storage.Tests;

/// <summary>
/// Tests for <see cref="ConcurrentStringMap{T}"/>, <see cref="ConcurrentNameMap"/>,
/// and <see cref="ConcurrentUInt32Map{T}"/>.
/// </summary>
public class ConcurrentMapTests
{
    // ---------------------------------------------------------------
    // ConcurrentStringMap<T>
    // ---------------------------------------------------------------

    [Fact]
    public void ConcurrentStringMap_SetAndGet()
    {
        var map = ConcurrentStringMap<int>.Create();
        StringSpan key = "testKey";
        map[key] = 42;

        Assert.Equal(42, map[key]);
    }

    [Fact]
    public void ConcurrentStringMap_TryGetValue_Existing()
    {
        var map = ConcurrentStringMap<string>.Create();
        StringSpan key = "hello";
        map[key] = "world";

        Assert.True(map.TryGetValue(key, out var value));
        Assert.Equal("world", value);
    }

    [Fact]
    public void ConcurrentStringMap_TryGetValue_Missing()
    {
        var map = ConcurrentStringMap<int>.Create();
        StringSpan key = "missing";

        Assert.False(map.TryGetValue(key, out _));
    }

    [Fact]
    public void ConcurrentStringMap_GetOrCreate_CreatesOnMiss()
    {
        var map = ConcurrentStringMap<int>.Create();
        StringSpan key = "lazy";

        var value = map.GetOrCreate(key, _ => 99);
        Assert.Equal(99, value);

        // Second call returns cached value
        var value2 = map.GetOrCreate(key, _ => 100);
        Assert.Equal(99, value2);
    }

    // ---------------------------------------------------------------
    // ConcurrentNameMap
    // ---------------------------------------------------------------

    [Fact]
    public void ConcurrentNameMap_Get_AssignsUniqueIds()
    {
        var map = new ConcurrentNameMap();
        StringSpan name1 = "alpha";
        StringSpan name2 = "beta";

        var (key1, _) = map.Get(name1);
        var (key2, _) = map.Get(name2);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void ConcurrentNameMap_Get_SameNameReturnsSameId()
    {
        var map = new ConcurrentNameMap();
        StringSpan name = "same";

        var (key1, _) = map.Get(name);
        var (key2, _) = map.Get(name);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void ConcurrentNameMap_TryGetValue_Existing()
    {
        var map = new ConcurrentNameMap();
        StringSpan name = "exists";
        map.Get(name);

        Assert.True(map.TryGetValue(name, out var result));
        Assert.Equal("exists", result.Name.Value);
    }

    [Fact]
    public void ConcurrentNameMap_TryGetValue_Missing()
    {
        var map = new ConcurrentNameMap();
        StringSpan name = "missing";

        Assert.False(map.TryGetValue(name, out _));
    }

    // ---------------------------------------------------------------
    // ConcurrentUInt32Map<T>
    // ---------------------------------------------------------------

    [Fact]
    public void ConcurrentUInt32Map_SetAndGet()
    {
        var map = ConcurrentUInt32Map<string>.Create();
        map[10u] = "ten";

        Assert.Equal("ten", map[10u]);
    }

    [Fact]
    public void ConcurrentUInt32Map_TryGetValue()
    {
        var map = ConcurrentUInt32Map<int>.Create();
        map[5u] = 50;

        Assert.True(map.TryGetValue(5u, out var value));
        Assert.Equal(50, value);

        Assert.False(map.TryGetValue(99u, out _));
    }

    [Fact]
    public void ConcurrentUInt32Map_GetOrCreate()
    {
        var map = ConcurrentUInt32Map<string>.Create();

        var value = map.GetOrCreate(1u, () => "created");
        Assert.Equal("created", value);

        var value2 = map.GetOrCreate(1u, () => "ignored");
        Assert.Equal("created", value2);
    }
}
