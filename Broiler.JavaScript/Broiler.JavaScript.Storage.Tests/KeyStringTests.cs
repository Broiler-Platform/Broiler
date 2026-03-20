using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Storage.Tests;

/// <summary>
/// Tests for <see cref="KeyType"/> enum, <see cref="KeyString"/> struct,
/// and <see cref="KeyStrings"/> static class, which were extracted from
/// Core into the Storage assembly.
/// </summary>
public class KeyStringTests
{
    // ─── KeyType ───────────────────────────────────────────────

    [Fact]
    public void KeyType_Empty_IsZero()
    {
        Assert.Equal(0, (int)KeyType.Empty);
    }

    [Fact]
    public void KeyType_HasExpectedValues()
    {
        Assert.Equal(1, (int)KeyType.UInt);
        Assert.Equal(2, (int)KeyType.String);
        Assert.Equal(3, (int)KeyType.Symbol);
    }

    // ─── KeyString ─────────────────────────────────────────────

    [Fact]
    public void Empty_HasKeyZero()
    {
        Assert.Equal(0u, KeyString.Empty.Key);
    }

    [Fact]
    public void Empty_HasNoValue()
    {
        Assert.False(KeyString.Empty.HasValue);
    }

    [Fact]
    public void ImplicitFromString_CreatesKeyString()
    {
        KeyString ks = "hello";
        Assert.True(ks.HasValue);
        Assert.Equal("hello", ks.Value.Value);
    }

    [Fact]
    public void ImplicitFromString_SameStringReturnsSameKey()
    {
        KeyString a = "test_same";
        KeyString b = "test_same";
        Assert.Equal(a.Key, b.Key);
    }

    [Fact]
    public void DifferentStrings_HaveDifferentKeys()
    {
        KeyString a = "alpha";
        KeyString b = "beta";
        Assert.NotEqual(a.Key, b.Key);
    }

    [Fact]
    public void Equals_SameKey_ReturnsTrue()
    {
        KeyString a = "equal_test";
        KeyString b = "equal_test";
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
    }

    [Fact]
    public void Equals_DifferentKey_ReturnsFalse()
    {
        KeyString a = "x1";
        KeyString b = "x2";
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void GetHashCode_SameKeyString_SameHash()
    {
        KeyString a = "hash_test";
        KeyString b = "hash_test";
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsStringValue()
    {
        KeyString ks = "world";
        Assert.Equal("world", ks.ToString());
    }

    [Fact]
    public void Value_ReturnsStringSpan()
    {
        KeyString ks = "span_test";
        Assert.Equal("span_test", ks.Value.Value);
    }

    // ─── KeyStrings ────────────────────────────────────────────

    [Fact]
    public void WellKnownKeys_HaveValues()
    {
        // A representative sample of well-known key strings
        Assert.True(KeyStrings.length.HasValue);
        Assert.True(KeyStrings.prototype.HasValue);
        Assert.True(KeyStrings.constructor.HasValue);
        Assert.True(KeyStrings.toString.HasValue);
        Assert.True(KeyStrings.valueOf.HasValue);
    }

    [Fact]
    public void WellKnownKeys_HaveCorrectNames()
    {
        Assert.Equal("length", KeyStrings.length.ToString());
        Assert.Equal("prototype", KeyStrings.prototype.ToString());
        Assert.Equal("constructor", KeyStrings.constructor.ToString());
    }

    [Fact]
    public void GetOrCreate_ReturnsConsistentKeys()
    {
        var a = KeyStrings.GetOrCreate("consistency_test");
        var b = KeyStrings.GetOrCreate("consistency_test");
        Assert.Equal(a.Key, b.Key);
    }

    [Fact]
    public void GetOrCreate_NewKey_HasValue()
    {
        var ks = KeyStrings.GetOrCreate("new_key_test");
        Assert.True(ks.HasValue);
    }

    [Fact]
    public void GetNameString_ReturnsCorrectString()
    {
        var ks = KeyStrings.GetOrCreate("name_string_test");
        var span = KeyStrings.GetNameString(ks.Key);
        Assert.Equal("name_string_test", span.Value);
    }

    [Fact]
    public void TryGet_ExistingKey_ReturnsTrue()
    {
        // Force creation
        KeyStrings.GetOrCreate("try_get_test");
        Assert.True(KeyStrings.TryGet("try_get_test", out var ks));
        Assert.True(ks.HasValue);
    }

    [Fact]
    public void TryGet_NonExistingKey_ReturnsFalse()
    {
        Assert.False(KeyStrings.TryGet("nonexistent_key_xyz_12345", out _));
    }

    [Fact]
    public void GetName_ReturnsKeyStringWithId()
    {
        var ks = KeyStrings.GetOrCreate("getname_test");
        var result = KeyStrings.GetName(ks.Key);
        Assert.Equal(ks.Key, result.Key);
    }

    [Fact]
    public void WellKnownKeys_AreDistinct()
    {
        // Verify that different well-known keys have different IDs
        var keys = new[] {
            KeyStrings.length,
            KeyStrings.prototype,
            KeyStrings.constructor,
            KeyStrings.toString,
            KeyStrings.valueOf,
            KeyStrings.name
        };

        var uniqueKeys = keys.Select(k => k.Key).Distinct().Count();
        Assert.Equal(keys.Length, uniqueKeys);
    }

    [Fact]
    public void TypeLivesInStorageAssembly()
    {
        // Verify that KeyString was successfully migrated to Storage
        var assembly = typeof(KeyString).Assembly;
        Assert.Contains("Storage", assembly.GetName().Name);
    }

    [Fact]
    public void KeyStringsClass_LivesInStorageAssembly()
    {
        // Verify that KeyStrings was successfully migrated to Storage
        var assembly = typeof(KeyStrings).Assembly;
        Assert.Contains("Storage", assembly.GetName().Name);
    }

    [Fact]
    public void KeyType_LivesInStorageAssembly()
    {
        // Verify that KeyType was successfully migrated to Storage
        var assembly = typeof(KeyType).Assembly;
        Assert.Contains("Storage", assembly.GetName().Name);
    }
}
