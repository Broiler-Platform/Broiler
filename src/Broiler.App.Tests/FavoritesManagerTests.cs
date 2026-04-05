namespace Broiler.App.Tests;

/// <summary>
/// Tests for <see cref="FavoritesManager"/> covering add, remove, contains,
/// persistence (save/load round-trip), duplicate prevention, and graceful
/// handling of missing or corrupt files.
/// </summary>
public class FavoritesManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public FavoritesManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "BroilerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "favorites.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Add_ReturnsTrue_WhenUrlIsNew()
    {
        var mgr = new FavoritesManager(_filePath);
        Assert.True(mgr.Add("https://example.com"));
        Assert.Single(mgr.Favorites);
        Assert.Equal("https://example.com", mgr.Favorites[0]);
    }

    [Fact]
    public void Add_ReturnsFalse_WhenUrlAlreadyExists()
    {
        var mgr = new FavoritesManager(_filePath);
        mgr.Add("https://example.com");
        Assert.False(mgr.Add("https://example.com"));
        Assert.Single(mgr.Favorites);
    }

    [Fact]
    public void Add_ReturnsFalse_ForNullOrWhitespace()
    {
        var mgr = new FavoritesManager(_filePath);
        Assert.False(mgr.Add(""));
        Assert.False(mgr.Add("   "));
        Assert.Empty(mgr.Favorites);
    }

    [Fact]
    public void Remove_ReturnsTrue_WhenUrlExists()
    {
        var mgr = new FavoritesManager(_filePath);
        mgr.Add("https://example.com");
        Assert.True(mgr.Remove("https://example.com"));
        Assert.Empty(mgr.Favorites);
    }

    [Fact]
    public void Remove_ReturnsFalse_WhenUrlNotFound()
    {
        var mgr = new FavoritesManager(_filePath);
        Assert.False(mgr.Remove("https://example.com"));
    }

    [Fact]
    public void Contains_ReturnsCorrectResult()
    {
        var mgr = new FavoritesManager(_filePath);
        mgr.Add("https://example.com");
        Assert.True(mgr.Contains("https://example.com"));
        Assert.False(mgr.Contains("https://other.com"));
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var mgr1 = new FavoritesManager(_filePath);
        mgr1.Add("https://example.com");
        mgr1.Add("https://other.com");
        mgr1.Save();

        var mgr2 = new FavoritesManager(_filePath);
        mgr2.Load();

        Assert.Equal(2, mgr2.Favorites.Count);
        Assert.Equal("https://example.com", mgr2.Favorites[0]);
        Assert.Equal("https://other.com", mgr2.Favorites[1]);
    }

    [Fact]
    public void Load_WithNoFile_StartsEmpty()
    {
        var mgr = new FavoritesManager(_filePath);
        mgr.Load();
        Assert.Empty(mgr.Favorites);
    }

    [Fact]
    public void Load_WithCorruptFile_StartsEmpty()
    {
        File.WriteAllText(_filePath, "this is not json");
        var mgr = new FavoritesManager(_filePath);
        mgr.Load();
        Assert.Empty(mgr.Favorites);
    }

    [Fact]
    public void Load_ClearsPreviousFavorites()
    {
        var mgr = new FavoritesManager(_filePath);
        mgr.Add("https://old.com");

        // Write a different list to the file
        File.WriteAllText(_filePath, "[\"https://new.com\"]");
        mgr.Load();

        Assert.Single(mgr.Favorites);
        Assert.Equal("https://new.com", mgr.Favorites[0]);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNeeded()
    {
        var deepPath = Path.Combine(_tempDir, "sub", "dir", "favorites.json");
        var mgr = new FavoritesManager(deepPath);
        mgr.Add("https://example.com");
        mgr.Save();

        Assert.True(File.Exists(deepPath));
    }
}
