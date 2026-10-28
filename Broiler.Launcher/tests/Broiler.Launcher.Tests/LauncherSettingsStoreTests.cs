using System;
using System.IO;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Xunit;

namespace Broiler.Launcher.Tests;

public class LauncherSettingsStoreTests
{
    [Fact]
    public void Load_ReturnsDefaultSettings_WhenFileDoesNotExist()
    {
        string nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        var store = new LauncherSettingsStore(nonExistentPath);

        var settings = store.Load();

        Assert.NotNull(settings);
        Assert.Empty(settings.Apps);
    }

    [Fact]
    public void SaveAndLoad_PersistsCustomPathsAndArguments()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        string testFile = Path.Combine(tempDir, "launcher.settings.json");

        try
        {
            var store = new LauncherSettingsStore(testFile);
            var settings = new LauncherSettings();

            var browserSetting = settings.GetOrCreate(BroilerAppId.Browser);
            browserSetting.ExecutablePath = "C:\\Custom\\browser.exe";
            browserSetting.Arguments = "--enable-gpu";
            browserSetting.UseAutoDetect = false;

            var writerSetting = settings.GetOrCreate(BroilerAppId.Writer);
            writerSetting.Arguments = "--page-width 800";
            writerSetting.UseAutoDetect = true;

            store.Save(settings);

            Assert.True(File.Exists(testFile));

            var reloaded = store.Load();
            Assert.NotNull(reloaded);

            var reloadedBrowser = reloaded.GetOrCreate(BroilerAppId.Browser);
            Assert.Equal("C:\\Custom\\browser.exe", reloadedBrowser.ExecutablePath);
            Assert.Equal("--enable-gpu", reloadedBrowser.Arguments);
            Assert.False(reloadedBrowser.UseAutoDetect);

            var reloadedWriter = reloaded.GetOrCreate(BroilerAppId.Writer);
            Assert.Null(reloadedWriter.ExecutablePath);
            Assert.Equal("--page-width 800", reloadedWriter.Arguments);
            Assert.True(reloadedWriter.UseAutoDetect);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_FallsBackToDefault_OnCorruptedJson()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "broiler_test_" + Guid.NewGuid().ToString("N"));
        string testFile = Path.Combine(tempDir, "corrupted.json");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(testFile, "{ this is not valid json! }");

            var store = new LauncherSettingsStore(testFile);
            var loaded = store.Load();

            Assert.NotNull(loaded);
            Assert.Empty(loaded.Apps);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        var original = new LauncherSettings();
        var app = original.GetOrCreate(BroilerAppId.Cell);
        app.ExecutablePath = "/opt/cell/cell.bin";
        app.Arguments = "--calc-threads 4";
        app.UseAutoDetect = false;

        var clone = original.Clone();
        Assert.NotNull(clone);

        var cloneApp = clone.GetOrCreate(BroilerAppId.Cell);
        Assert.Equal(app.ExecutablePath, cloneApp.ExecutablePath);
        Assert.Equal(app.Arguments, cloneApp.Arguments);
        Assert.Equal(app.UseAutoDetect, cloneApp.UseAutoDetect);

        // Mutate original, clone remains unchanged
        app.Arguments = "--single-thread";
        Assert.Equal("--calc-threads 4", cloneApp.Arguments);
    }
}
