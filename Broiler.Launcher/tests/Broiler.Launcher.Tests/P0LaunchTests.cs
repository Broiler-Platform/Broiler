using System.Diagnostics;
using Broiler.Launcher.Cli;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed class P0LaunchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public P0LaunchTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private sealed class Store : ILauncherSettingsStore
    {
        public string SettingsFilePath => "memory";
        public LauncherSettings Value = new();
        public bool Fail;
        public LauncherSettings Load() => Value;
        public void Save(LauncherSettings settings)
        {
            if (Fail) throw new IOException("denied");
            Value = settings;
        }
    }

    [Fact]
    public void CliPreservesTokensAndPrependsSettingsArguments()
    {
        var app = BroilerAppInfo.CreateDefaultSuite()[0];
        string binary = Path.Combine(_root, "custom.dll");
        File.WriteAllText(binary, "fixture");
        var store = new Store();
        store.Value.Apps[app.Id] = new() { UseAutoDetect = false, ExecutablePath = binary, Arguments = "--profile \"two words\"" };
        ProcessStartInfo? captured = null;
        var service = new AppLauncherService([app], store, startProcess: info => { captured = info; return 42; }, inventory: new EmptyInstallationInventory());
        string[] tokens = ["a b", "", "quote\"value", @"C:\folder with spaces\", "Ünicode"];
        Assert.True(LauncherCliHandler.TryHandleCli(["--app", "browser", .. tokens], out int code, service));
        Assert.Equal(0, code);
        Assert.NotNull(captured);
        Assert.Equal("dotnet", captured.FileName);
        Assert.False(captured.UseShellExecute);
        Assert.Equal(new[] { binary, "--profile", "two words" }.Concat(tokens), captured.ArgumentList);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("quote\"value")]
    [InlineData(@"C:\folder with spaces\")]
    [InlineData("Ünicode")]
    public void LegacyArgumentGrammarRoundTrips(string token) =>
        Assert.Equal(new[] { token }, LaunchArguments.Parse(LaunchArguments.Format([token])));

    [Fact]
    public void InvalidQuotesFailWithoutStartingProcess()
    {
        bool started = false;
        var service = new AppLauncherService(settingsStore: new Store(), startProcess: _ => { started = true; return 1; }, inventory: new EmptyInstallationInventory());
        Assert.False(service.LaunchApp(service.Apps[0], "\"unterminated").Succeeded);
        Assert.False(started);
    }

    [Theory]
    [InlineData(BroilerAppId.Cell)]
    [InlineData(BroilerAppId.Plate)]
    public void UnsupportedLinuxProductsCannotBypassCapabilityWithCustomPath(BroilerAppId id)
    {
        var app = BroilerAppInfo.CreateDefaultSuite().Single(a => a.Id == id);
        string binary = Path.Combine(_root, "custom.dll");
        File.WriteAllText(binary, "fixture");
        var store = new Store();
        store.Value.Apps[id] = new() { UseAutoDetect = false, ExecutablePath = binary };
        var service = new AppLauncherService([app], store, isWindows: false, inventory: new EmptyInstallationInventory());
        Assert.Null(app.GetPlatform(false));
        Assert.False(service.ResolveApp(app, _root).IsAvailable);
        Assert.False(service.AutoDetect(app, _root).IsAvailable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WorkspaceDiscoverySelectsOnlyRequestedPlatform(bool windows)
    {
        var app = BroilerAppInfo.CreateDefaultSuite()[0];
        foreach (string relative in app.RelativeProbePaths)
        {
            string file = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "fixture");
        }
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var service = new AppLauncherService([app], new Store(), isWindows: windows, environment: _ => null, currentDirectory: _root, inventory: new EmptyInstallationInventory());
        var resolution = service.AutoDetect(app, _root);
        Assert.True(resolution.IsAvailable);
        Assert.Contains(windows ? ".Windows" : ".Linux", resolution.ResolvedPath);
    }

    [Fact]
    public void FailedSaveDoesNotReplaceActiveSettings()
    {
        var store = new Store();
        var service = new AppLauncherService(settingsStore: store, inventory: new EmptyInstallationInventory());
        var original = service.Settings;
        store.Fail = true;
        Assert.Throws<IOException>(() => service.UpdateSettings(new LauncherSettings()));
        Assert.Same(original, service.Settings);
    }
}
