using System;
using System.IO;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed class P0PersistenceTests
{
    [Fact]
    public void SaveRetainsPreviousVersionAndNormalizesNullApps()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, "{\"Apps\":null}");
            var store = new LauncherSettingsStore(path);
            Assert.NotNull(store.Load().Apps);
            store.Save(new LauncherSettings());
            Assert.Equal("{\"Apps\":null}", File.ReadAllText(path + ".bak"));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void MalformedSettingsRemainAvailableForRecovery()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "invalid json");
            var store = new LauncherSettingsStore(path);
            Assert.NotNull(store.Load());
            Assert.NotNull(store.LastLoadError);
            Assert.Equal("invalid json", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }
}
