using System.Reflection;
using System.Text.Json;
using Broiler.Graphics.Geometry;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.UI;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.CheckBox;
using Broiler.UI.CheckBox.Standard;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.ScrollView.Standard;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed class OfficeSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "office-settings-" + Guid.NewGuid().ToString("N"));
    public OfficeSettingsTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private string SettingsPath => Path.Combine(_root, "launcher.settings.json");
    private static T Field<T>(object view, string name) => (T)view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
    private sealed class Store : ILauncherSettingsStore
    {
        public LauncherSettings Value = new(); public bool Fail; public int Saves;
        public string SettingsFilePath => "fixture";
        public LauncherSettings Load() => Value.Clone();
        public void Save(LauncherSettings settings) { if (Fail) throw new IOException("fixture save denied"); Saves++; Value = settings.Clone(); }
    }
    private static AppLauncherService Service(Store store) => new(settingsStore: store, inventory: new EmptyInstallationInventory());
    private string Binary() { string path = Path.Combine(_root, "product.exe"); File.WriteAllText(path, "fixture"); return path; }
    private sealed class Catalog : IReleaseCatalogClient
    {
        public string? Channel; public bool Force;
        public UpdateCheckStatus Status => new(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "fixture diagnostic");
        public Task<ReleaseCheck> CheckAsync(string channel, bool force = false, CancellationToken token = default)
        { Channel = channel; Force = force; return Task.FromResult(new ReleaseCheck([], "fixture checked")); }
        public OfficeReleaseCandidate ReadLocal(string directory) => throw new NotSupportedException();
    }
    [Fact]
    public async Task CheckNowUsesDraftChannelWithoutSavingOrInstalling()
    {
        var store = new Store(); var catalog = new Catalog(); using var view = new SettingsView(Service(store), catalog);
        Field<StandardComboBox>(view, "_channel").SelectedIndex = 1; Field<StandardEdit>(view, "_interval").Text = "48";
        await view.CheckNowAsync(); Assert.Equal("preview", catalog.Channel); Assert.True(catalog.Force); Assert.Equal(0, store.Saves);
        Assert.Equal(24, store.Value.Updates.CheckIntervalHours);
        Assert.Contains("Last attempt", Field<StandardLabel>(view, "_updateStatus").Text);
    }

    [Fact]
    public void LegacySettingsRetainAllFourProductsAndUnknownData()
    {
        File.WriteAllText(SettingsPath, """
        {"FutureRoot":{"enabled":true},"Apps":{
          "Browser":{"UseAutoDetect":false,"ExecutablePath":"C:\\browser.exe","Arguments":"--profile default","FutureApp":{"x":1}},
          "Writer":{"Arguments":"--page-width 800"},"Cell":{"UseAutoDetect":true},"Plate":{"Arguments":"\"two words\""},
          "FutureProduct":{"Any":"keep me"}},"Boss":{"FutureBoss":42},"Updates":{"FutureUpdate":"keep"}}
        """);
        var store = new LauncherSettingsStore(SettingsPath); var settings = store.Load();
        Assert.Null(store.LastLoadError); Assert.Equal(2, settings.SchemaVersion); Assert.Equal(4, settings.Apps.Count);
        Assert.False(settings.Apps[BroilerAppId.Browser].UseAutoDetect);
        Assert.Equal("--page-width 800", settings.Apps[BroilerAppId.Writer].Arguments);
        Assert.Equal("\"two words\"", settings.Apps[BroilerAppId.Plate].Arguments);
        var clone = settings.Clone(); clone.Extra!.Clear();
        Assert.NotEmpty(settings.Extra!);
        store.Save(settings);
        using var saved = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.True(saved.RootElement.GetProperty("FutureRoot").GetProperty("enabled").GetBoolean());
        Assert.Equal("keep me", saved.RootElement.GetProperty("Apps").GetProperty("FutureProduct").GetProperty("Any").GetString());
        Assert.Equal(1, saved.RootElement.GetProperty("Apps").GetProperty("Browser").GetProperty("FutureApp").GetProperty("x").GetInt32());
        Assert.Equal(42, saved.RootElement.GetProperty("Boss").GetProperty("FutureBoss").GetInt32());
        Assert.True(File.Exists(SettingsPath + ".bak"));
    }
    [Fact]
    public void PartiallyNullAndInvalidPreferencesRecoverWithoutLosingProducts()
    {
        File.WriteAllText(SettingsPath, """
        {"Apps":{"Browser":null,"Writer":{"Arguments":"--keep","PreferredInstalledScope":"FutureScope"}},
         "PreferredInstallScope":99,"Boss":null,"Updates":{"Channel":null,"CheckIntervalHours":0}}
        """);
        var store = new LauncherSettingsStore(SettingsPath); var settings = store.Load();
        Assert.NotNull(store.LastLoadError); Assert.Equal("--keep", settings.Apps[BroilerAppId.Writer].Arguments);
        Assert.Equal(InstallScope.User, settings.Apps[BroilerAppId.Writer].PreferredInstalledScope);
        Assert.NotNull(settings.Apps[BroilerAppId.Browser]); Assert.Equal(24, settings.Updates.CheckIntervalHours);
        Assert.Equal("http://127.0.0.1:5555/", settings.Boss.ListenUrl);
    }
    [Fact]
    public void FutureSchemaIsReadableButCannotBeOverwritten()
    {
        string original = "{\"SchemaVersion\":99,\"Apps\":{\"Writer\":{\"Arguments\":\"--keep\"}},\"Future\":true}";
        File.WriteAllText(SettingsPath, original);
        var store = new LauncherSettingsStore(SettingsPath); var settings = store.Load();
        Assert.Equal("--keep", settings.Apps[BroilerAppId.Writer].Arguments); Assert.Contains("newer", store.LastLoadError!);
        Assert.Throws<InvalidOperationException>(() => store.Save(settings)); Assert.Equal(original, File.ReadAllText(SettingsPath));
    }
    [Fact]
    public void AllOfficePreferencesRoundTrip()
    {
        var settings = new LauncherSettings { PreferredInstallScope = InstallScope.Machine };
        settings.Boss = new() { ListenUrl = "http://localhost:5612/", AdditionalArguments = "--Logging:LogLevel:Default Warning", StartupTimeoutSeconds = 90, KeepRunningOnLauncherExit = false, OpenBrowserOnStart = true };
        settings.Updates = new() { Channel = "preview", CheckOnStartup = false, CheckIntervalHours = 72 };
        var writer = settings.GetOrCreate(BroilerAppId.Writer); writer.ShowInstallActions = false; writer.ShowUpdateActions = false; writer.ShowBossAction = false; writer.PreferredInstalledScope = InstallScope.Machine;
        var store = new LauncherSettingsStore(SettingsPath); store.Save(settings); var actual = store.Load();
        Assert.Equal(InstallScope.Machine, actual.PreferredInstallScope); Assert.Equal(72, actual.Updates.CheckIntervalHours);
        Assert.False(actual.Updates.CheckOnStartup); Assert.Equal("preview", actual.Updates.Channel);
        Assert.Equal(90, actual.Boss.StartupTimeoutSeconds); Assert.True(actual.Boss.OpenBrowserOnStart);
        Assert.False(actual.Boss.KeepRunningOnLauncherExit); Assert.Equal(settings.Boss.AdditionalArguments, actual.Boss.AdditionalArguments);
        Assert.False(actual.Apps[BroilerAppId.Writer].ShowUpdateActions); Assert.Equal(InstallScope.Machine, actual.Apps[BroilerAppId.Writer].PreferredInstalledScope);
    }
    [Theory]
    [InlineData("http://0.0.0.0:5555/")]
    [InlineData("http://127.0.0.1:0/")]
    [InlineData("http://127.0.0.1:70000/")]
    [InlineData("http://127.0.0.1:5555/path")]
    public void InvalidBossUrlDoesNotSaveOrDiscardDraft(string url)
    {
        var store = new Store(); using var view = new SettingsView(Service(store));
        Field<StandardEdit>(view, "_bossUrl").Text = url;
        Field<List<StandardEdit>>(view, "_argEdits")[0].Text = "--keep-draft";
        bool saved = false; view.Saved += (_, _) => saved = true; view.SaveCurrentSettings();
        Assert.False(saved); Assert.Equal(0, store.Saves); Assert.Equal(2, view.CurrentSection);
        Assert.Equal(url, Field<StandardEdit>(view, "_bossUrl").Text); Assert.Equal("--keep-draft", Field<List<StandardEdit>>(view, "_argEdits")[0].Text);
        Assert.Contains("Could not save", Field<StandardLabel>(view, "_footerStatusLabel").Text);
    }
    [Fact]
    public void FailedPersistenceRetainsDraftAndCurrentSettings()
    {
        var store = new Store { Fail = true }; var service = Service(store); using var view = new SettingsView(service);
        Field<StandardEdit>(view, "_interval").Text = "48"; view.SaveCurrentSettings();
        Assert.Equal(24, service.Settings.Updates.CheckIntervalHours); Assert.Equal("48", Field<StandardEdit>(view, "_interval").Text);
        Assert.Contains("fixture save denied", Field<StandardLabel>(view, "_footerStatusLabel").Text);
        store.Fail = false; view.SaveCurrentSettings(); Assert.Equal(48, service.Settings.Updates.CheckIntervalHours);
    }
    [Fact]
    public void ExplicitCustomModeRetainsInactivePathAndCancelDiscards()
    {
        var store = new Store(); using var view = new SettingsView(Service(store));
        string path = Binary(); Field<List<StandardEdit>>(view, "_pathEdits")[0].Text = path;
        Field<List<StandardComboBox>>(view, "_appModes")[0].SelectedIndex = 1;
        view.SaveCurrentSettings(); Assert.False(store.Value.GetOrCreate(BroilerAppId.Browser).UseAutoDetect);
        view.AutoDetectApp(0); view.SaveCurrentSettings(); Assert.True(store.Value.GetOrCreate(BroilerAppId.Browser).UseAutoDetect);
        Assert.Equal(path, store.Value.GetOrCreate(BroilerAppId.Browser).ExecutablePath);
        Field<StandardEdit>(view, "_interval").Text = "96";
        Field<StandardButton>(view, "_cancelButton").Click();
        Assert.Equal(24, store.Value.Updates.CheckIntervalHours); Assert.Equal("24", Field<StandardEdit>(view, "_interval").Text);
    }
    [Theory]
    [InlineData("relative.exe")]
    [InlineData("")]
    public void InvalidCustomPathDoesNotSaveOrFallBackToAutodetection(string path)
    {
        var store = new Store(); using var view = new SettingsView(Service(store));
        Field<List<StandardComboBox>>(view, "_appModes")[0].SelectedIndex = 1;
        Field<List<StandardEdit>>(view, "_pathEdits")[0].Text = path;
        view.SaveCurrentSettings(); Assert.Equal(0, store.Saves); Assert.Equal(0, view.CurrentSection);
        Assert.Contains("absolute path", Field<StandardLabel>(view, "_footerStatusLabel").Text);
        Assert.Equal(1, Field<List<StandardComboBox>>(view, "_appModes")[0].SelectedIndex);
    }
    [Theory]
    [InlineData(480, 360)]
    [InlineData(840, 560)]
    [InlineData(1280, 800)]
    public void EverySectionScrollsToItsLastControlWithoutCoveringSave(int width, int height)
    {
        using var view = new SettingsView(Service(new Store()));
        for (int section = 0; section < 4; section++)
        {
            view.ShowSection(section); view.Measure(new(width, height)); view.Arrange(new(0, 0, width, height));
            var scroll = Field<StandardScrollView>(view, "_scroll");
            Assert.True(scroll.Bounds.Bottom <= Field<StandardButton>(view, "_saveButton").Bounds.Top);
            Assert.True(scroll.ExtentSize.Height >= scroll.ViewportSize.Height);
            scroll.SetOffset(new BPoint(0, scroll.ExtentSize.Height)); view.Arrange(new(0, 0, width, height));
            Assert.True(scroll.VerticalOffset <= Math.Max(0, scroll.ExtentSize.Height - scroll.ViewportSize.Height));
        }
    }
    [Theory]
    [InlineData("--urls http://0.0.0.0:5555")]
    [InlineData("--contentRoot C:/other")]
    [InlineData("--Kestrel:Endpoints:Public:Url http://0.0.0.0:1234")]
    [InlineData("--Logging:LogLevel:Default nope")]
    public void BossAdditionalArgumentsCannotOverrideListenerOrContentRoot(string arguments) =>
        Assert.Throws<ArgumentException>(() => new BossSettings { AdditionalArguments = arguments }.Validate());
}
