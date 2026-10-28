using System;
using System.IO;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Xunit;

namespace Broiler.Launcher.Tests;

public class AppLauncherServiceSettingsTests
{
    private sealed class InMemorySettingsStore : ILauncherSettingsStore
    {
        public string SettingsFilePath => "in-memory://settings.json";
        public LauncherSettings Settings { get; set; } = new();

        public LauncherSettings Load() => Settings.Clone();

        public void Save(LauncherSettings settings) => Settings = settings.Clone();
    }

    [Fact]
    public void ResolveApp_UsesCustomPath_WhenConfiguredAndFileExists()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "broiler_res_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string dummyExe = Path.Combine(tempDir, "CustomWriter.exe");
        File.WriteAllText(dummyExe, "dummy");

        try
        {
            var store = new InMemorySettingsStore();
            var appSetting = store.Settings.GetOrCreate(BroilerAppId.Writer);
            appSetting.ExecutablePath = dummyExe;
            appSetting.UseAutoDetect = false;

            var service = new AppLauncherService(settingsStore: store, inventory: new EmptyInstallationInventory());
            var writerApp = BroilerAppInfo.CreateDefaultSuite()[1]; // Writer

            var resolution = service.ResolveApp(writerApp);

            Assert.True(resolution.IsAvailable);
            Assert.True(resolution.IsCustom);
            Assert.Equal(Path.GetFullPath(dummyExe), resolution.ResolvedPath);
            Assert.Equal("Ready (Custom)", resolution.StatusText);
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
    public void ResolveApp_ReportsNotFound_WhenCustomPathDoesNotExist()
    {
        var store = new InMemorySettingsStore();
        var appSetting = store.Settings.GetOrCreate(BroilerAppId.Writer);
        appSetting.ExecutablePath = "C:\\NonExistentDirectory\\DoesNotExist.exe";
        appSetting.UseAutoDetect = false;

        var service = new AppLauncherService(settingsStore: store, inventory: new EmptyInstallationInventory());
        var writerApp = BroilerAppInfo.CreateDefaultSuite()[1]; // Writer

        var resolution = service.ResolveApp(writerApp);

        Assert.False(resolution.IsAvailable);
        Assert.Equal("Custom Path Not Found", resolution.StatusText);
    }

    [Fact]
    public void ResolveApp_FallsBackToAutoDetect_WhenUseAutoDetectIsTrue()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "broiler_auto_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var store = new InMemorySettingsStore();
            var appSetting = store.Settings.GetOrCreate(BroilerAppId.Browser);
            // Ignored because UseAutoDetect is true
            appSetting.ExecutablePath = "C:\\IgnoredPath\\browser.exe";
            appSetting.UseAutoDetect = true;

            var service = new AppLauncherService(settingsStore: store, inventory: new EmptyInstallationInventory());
            var browserApp = BroilerAppInfo.CreateDefaultSuite()[0]; // Browser

            var resolution = service.ResolveApp(browserApp, baseDirectory: tempDir);

            // In empty temp directory, auto-detect returns not found (unless on system PATH)
            // But critically, it didn't use the invalid custom path!
            Assert.NotEqual("C:\\IgnoredPath\\browser.exe", resolution.ResolvedPath);
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
    public void AutoDetect_IgnoresCustomPathSettings()
    {
        var store = new InMemorySettingsStore();
        var appSetting = store.Settings.GetOrCreate(BroilerAppId.Cell);
        appSetting.ExecutablePath = "C:\\Custom\\Cell.exe";
        appSetting.UseAutoDetect = false;

        var service = new AppLauncherService(settingsStore: store, inventory: new EmptyInstallationInventory());
        var cellApp = BroilerAppInfo.CreateDefaultSuite()[2]; // Cell

        // AutoDetect ignores custom path overrides completely
        var resolution = service.AutoDetect(cellApp, baseDirectory: Path.GetTempPath());
        Assert.NotEqual("C:\\Custom\\Cell.exe", resolution.ResolvedPath);
    }
}
