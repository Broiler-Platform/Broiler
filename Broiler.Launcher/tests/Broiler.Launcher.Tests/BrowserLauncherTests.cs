using Broiler.Launcher.Browser;
using Xunit;

namespace Broiler.Launcher.Tests;
public class BrowserLauncherTests
{
    private sealed class Preferences : IBrowserPreferences
    {
        private readonly Dictionary<string, string> _values = new();
        public string? Read(string key) => _values.GetValueOrDefault(key);
        public void Write(string key, string value) => _values[key] = value;
    }
    [Fact] public void BrowserCompositionCannotResolveNativeServices()
    {
        var references = typeof(BrowserLauncher).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name!.Contains("Launcher.Core") || reference.Name.Contains("Process") || reference.Name.Contains("FileSystem"));
        var launcher = new BrowserLauncher(_ => throw new Exception("disabled product navigated"), new Preferences());
        Assert.False(launcher.Capabilities.CanInstall); Assert.False(launcher.Capabilities.CanStartServer);
        Assert.False(launcher.Capabilities.CanUpdateNativeProducts); Assert.False(launcher.Capabilities.CanConfigureExecutablePaths);
        Assert.False(launcher.Launch("browser")); Assert.False(launcher.Launch("unknown"));
    }
    [Theory]
    [InlineData("writer", "https://writer.broiler.app/")]
    [InlineData("cell", "https://cell.broiler.app/")]
    [InlineData("plate", "https://plate.broiler.app/")]
    public void BrowserLaunchUsesExplicitOrigin(string id, string expected)
    {
        Uri? address = null; var launcher = new BrowserLauncher(uri => address = uri, new Preferences());
        Assert.True(launcher.Launch(id)); Assert.Equal(expected, address!.AbsoluteUri);
    }
    [Fact] public void BrowserPreferencesRemainSeparateAndValidated()
    {
        var preferences = new Preferences(); var launcher = new BrowserLauncher(_ => { }, preferences);
        launcher.SetTheme("dark"); Assert.Equal("dark", new BrowserLauncher(_ => { }, preferences).Theme);
        Assert.Throws<ArgumentException>(() => launcher.SetTheme("path"));
    }
    [Fact] public void BrowserDefaultsToSameTabAndPersistsNewTabPreference()
    {
        var preferences = new Preferences(); var launcher = new BrowserLauncher(_ => { }, preferences);
        Assert.False(launcher.OpenInNewTab);
        launcher.SetOpenInNewTab(true);
        Assert.True(new BrowserLauncher(_ => { }, preferences).OpenInNewTab);
    }
    [Theory]
    [InlineData("writer", "/writer/")]
    [InlineData("cell", "/cell/")]
    [InlineData("plate", "/plate/")]
    public void BrowserLaunchSupportsSameOriginLocalBossPaths(string id, string expected)
    {
        var deployment = new BrowserDeploymentInfo("preview", "1.0.0", "1.0.0", "1.0.0", "1.0.0", true)
        {
            WriterUrl = "/writer/",
            CellUrl = "/cell/",
            PlateUrl = "/plate/",
        };
        Uri? address = null; var launcher = new BrowserLauncher(uri => address = uri, new Preferences(), deployment);
        Assert.True(launcher.Launch(id));
        Assert.False(address!.IsAbsoluteUri);
        Assert.Equal(expected, address.OriginalString);
    }
}
