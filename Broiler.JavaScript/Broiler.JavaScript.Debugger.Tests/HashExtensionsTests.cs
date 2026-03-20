using System.Security.Cryptography;
using Broiler.JavaScript.Debugger;

namespace Broiler.JavaScript.Debugger.Tests;

/// <summary>
/// Tests for <see cref="HashExtensions"/>.
/// </summary>
public class HashExtensionsTests
{
    [Fact]
    public void ComputeHash_ReturnsHexString()
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash("hello");

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        // All characters should be valid hex
        Assert.All(hash, c => Assert.True(char.IsAsciiHexDigitLower(c) || char.IsDigit(c)));
    }

    [Fact]
    public void ComputeHash_SameInputSameOutput()
    {
        using var sha1 = SHA256.Create();
        using var sha2 = SHA256.Create();

        var hash1 = sha1.ComputeHash("test");
        var hash2 = sha2.ComputeHash("test");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentInputsDifferentOutput()
    {
        using var sha = SHA256.Create();
        var hash1 = sha.ComputeHash("foo");
        var hash2 = sha.ComputeHash("bar");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_EmptyString()
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash("");

        Assert.NotNull(hash);
        Assert.Empty(hash);
    }
}
