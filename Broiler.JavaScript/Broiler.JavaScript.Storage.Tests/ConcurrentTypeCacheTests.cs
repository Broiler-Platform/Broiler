using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Storage.Tests;

/// <summary>
/// Tests for <see cref="ConcurrentTypeCache"/> and <see cref="ConcurrentTypeTrie{T}"/>.
/// </summary>
public class ConcurrentTypeCacheTests
{
    // ---------------------------------------------------------------
    // ConcurrentTypeCache
    // ---------------------------------------------------------------

    [Fact]
    public void GetOrCreate_ReturnsConsistentId()
    {
        var id1 = ConcurrentTypeCache.GetOrCreate(typeof(string));
        var id2 = ConcurrentTypeCache.GetOrCreate(typeof(string));

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GetOrCreate_DifferentTypes_DifferentIds()
    {
        var id1 = ConcurrentTypeCache.GetOrCreate(typeof(int));
        var id2 = ConcurrentTypeCache.GetOrCreate(typeof(double));

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GetOrCreate_WithSuffix_DifferentFromWithout()
    {
        var id1 = ConcurrentTypeCache.GetOrCreate(typeof(string));
        var id2 = ConcurrentTypeCache.GetOrCreate(typeof(string), "special");

        Assert.NotEqual(id1, id2);
    }

    // ---------------------------------------------------------------
    // ConcurrentTypeTrie<T>
    // ---------------------------------------------------------------

    [Fact]
    public void ConcurrentTypeTrie_CreatesOnFirstAccess()
    {
        var trie = new ConcurrentTypeTrie<string>(t => t.Name);

        Assert.Equal("String", trie[typeof(string)]);
        Assert.Equal("Int32", trie[typeof(int)]);
    }

    [Fact]
    public void ConcurrentTypeTrie_CachesResult()
    {
        int callCount = 0;
        var trie = new ConcurrentTypeTrie<int>(_ =>
        {
            Interlocked.Increment(ref callCount);
            return callCount;
        });

        var result1 = trie[typeof(string)];
        var result2 = trie[typeof(string)];

        Assert.Equal(result1, result2);
    }
}
