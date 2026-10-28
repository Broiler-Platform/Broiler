using Broiler.Launcher.Installation;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed partial class OfficeInstallationTests
{
    [Fact]
    public async Task StableInstallRequiresTheOtherInstalledProductsToMatchItsApprovedVector()
    {
        var writer = _release.Components[0];
        Assert.True((await Install()).Succeeded);
        BossPackage("1.1.0");
        var blocked = await Installer().InstallAsync(_source.Read("boss"), InstallScope.User);
        Assert.False(blocked.Succeeded); Assert.Contains("Office-approved", blocked.Message);
        Assert.Equal("1.0.0", Installed().Receipt!.Version);
        _release = _release with { Components = [.. _release.Components, writer with { Asset = "retained-writer.zip" }] };
        Sign();
        Assert.True((await Installer().InstallAsync(_source.Read("boss"), InstallScope.User)).Succeeded);
    }

    [Fact]
    public async Task StableUpdateRejectsMixingComponentsFromDifferentApprovedOfficeReleases()
    {
        var writer = _release.Components[0];
        Assert.True((await Install()).Succeeded);
        BossPackage("1.0.0");
        var boss = _release.Components[0] with { Asset = "retained-boss.zip" };
        _release = _release with { Components = [.. _release.Components, writer with { Asset = "retained-writer.zip" }] }; Sign();
        Assert.True((await Installer().InstallAsync(_source.Read("boss"), InstallScope.User)).Succeeded);
        WritePackage("1.1.0");
        var nextWriter = _release.Components[0];
        _release = _release with { Components = [nextWriter, boss with { Version = "1.1.0" }] }; Sign();
        var updater = Updater(); var plan = Assert.Single(updater.FindUpdates([Candidate()]).Where(p => p.Component.Id == "writer"));
        var blocked = await updater.ApplyAsync(plan);
        Assert.False(blocked.Succeeded); Assert.Contains("Office-approved", blocked.Message);
        Assert.Equal("1.0.0", Installed().Receipt!.Version);
        // A separately tested single-component change has a valid intermediate vector.
        _release = _release with { Components = [nextWriter, boss] }; Sign();
        plan = Assert.Single(updater.FindUpdates([Candidate()]));
        Assert.True((await updater.ApplyAsync(plan)).Succeeded);
        Assert.Equal("1.1.0", Installed().Receipt!.Version);
    }

    [Fact]
    public async Task PreviewKeepsExplicitExperimentalCombinationSupport()
    {
        Assert.True((await Install()).Succeeded);
        BossPackage("1.1.0");
        _release = _release with { Channel = "preview" }; Sign();
        Assert.True((await Installer().InstallAsync(_source.Read("boss"), InstallScope.User)).Succeeded);
    }

    [Fact]
    public async Task StableRecoveryDetectsChangedPeerReceiptsUnderTheActivationLock()
    {
        var writer = _release.Components[0] with { Asset = "retained-writer.zip" };
        Assert.True((await Install()).Succeeded);
        BossPackage("1.0.0");
        var boss = _release.Components[0] with { Asset = "retained-boss.zip" };
        _release = _release with { Components = [.. _release.Components, writer] }; Sign();
        Assert.True((await Installer().InstallAsync(_source.Read("boss"), InstallScope.User)).Succeeded);
        var inventory = new InstallationInventory(_paths);
        var peers = OfficeCompatibility.CapturePeers("boss", InstallScope.User, inventory);
        Assert.Null(OfficeCompatibility.PeersChanged(peers, InstallScope.User, inventory));
        WritePackage("1.0.0");
        _release = _release with { Components = [.. _release.Components, boss] }; Sign();
        Assert.True((await Install(repair: true)).Succeeded); // Same bits/version, new receipt identity.
        Assert.NotNull(OfficeCompatibility.PeersChanged(peers, InstallScope.User, inventory));
        var result = Installer().Rollback("boss", InstallScope.User, expectedPeers: peers);
        Assert.False(result.Succeeded);
        Assert.Contains("Another product changed", result.Message);
    }
}
