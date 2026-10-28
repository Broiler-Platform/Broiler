using System.Reflection;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Models;
using Broiler.Launcher.UI;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Xunit;

namespace Broiler.Launcher.Tests;

public partial class LauncherViewTests
{
    private sealed class UpdateCatalog : IReleaseCatalogClient
    {
        public readonly OfficeReleaseCandidate Candidate = new(new(1, "1.1.0", "stable", "office-v1.1.0", new string('a', 40), "0.1.0", []), "unused", true, true);
        public Task<ReleaseCheck> CheckAsync(string channel, bool force = false, CancellationToken token = default) => Task.FromResult(new ReleaseCheck([Candidate], "Checked"));
        public OfficeReleaseCandidate ReadLocal(string directory) => Candidate;
    }
    private sealed class ControlledUpdates : IUpdateService
    {
        public readonly TaskCompletionSource<ProductUpdate> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<InstallResult> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<ProductUpdate> FindUpdates(IEnumerable<OfficeReleaseCandidate> releases) => releases.SelectMany(c => new[] { InstallScope.User, InstallScope.Machine }.Select(scope =>
            new ProductUpdate(c, new("writer", "1.1.0", "win-x64", "writer.zip", 1, new string('a', 64), "zip", "Broiler.Writer.Windows.exe", "native", "self-contained"),
                new(1, "writer", scope, "1.0.0", "win-x64", "office-v1.0.0", "writer.zip", new string('b', 64), "Broiler.Writer.Windows.exe", "native", new string('c', 32), DateTimeOffset.UtcNow)))).ToArray();
        public Task<InstallResult> ApplyAsync(ProductUpdate update, IProgress<InstallProgress>? progress = null, CancellationToken token = default)
        { Started.TrySetResult(update); return Completion.Task.WaitAsync(token); }
    }
    private static List<StandardButton> UpdateButtons(LauncherView view) => (List<StandardButton>)typeof(LauncherView)
        .GetField("_updateButtons", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;

    [Fact]
    public async Task UpdateButtonAppearsOnlyForEligibleProductAndApplyRetainsSelectedScope()
    {
        var launcher = new MockLauncherService();
        var installer = new ControlledInstaller();
        var updates = new ControlledUpdates();
        using var view = new LauncherView(launcher, installer, new FakeBossController(), new UpdateCatalog(), updates);
        Assert.All(UpdateButtons(view), x => Assert.Equal(UiVisibility.Collapsed, x.Visibility));
        await view.CheckUpdatesAsync();
        Assert.Equal(UiVisibility.Visible, UpdateButtons(view)[1].Visibility);
        Assert.Equal(UiVisibility.Collapsed, UpdateButtons(view)[0].Visibility);
        var pending = view.UpdateProductAsync("writer", InstallScope.Machine);
        Assert.Equal(InstallScope.Machine, (await updates.Started.Task.WaitAsync(TimeSpan.FromSeconds(5))).Expected.Scope);
        view.LaunchById(BroilerAppId.Writer);
        Assert.Equal(0, launcher.LaunchCount);
        await view.InstallProductAsync(BroilerAppId.Browser, InstallScope.User);
        Assert.Equal(0, installer.Calls);
        Assert.False(UpdateButtons(view)[1].IsEnabled);
        updates.Completion.SetResult(new(true, "Updated"));
        await pending;
        Assert.True(UpdateButtons(view)[1].IsEnabled);
    }

    [Fact]
    public void UpdateSettingsCloneDoesNotShareMutablePreferences()
    {
        var settings = new LauncherSettings();
        var copy = settings.Clone();
        copy.Updates.Channel = "preview";
        copy.Updates.CheckOnStartup = false;
        Assert.True(settings.Updates.CheckOnStartup);
        Assert.NotSame(settings.Updates, copy.Updates);
    }
}
