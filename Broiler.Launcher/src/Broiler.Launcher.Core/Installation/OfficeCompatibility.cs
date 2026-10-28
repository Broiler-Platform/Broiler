namespace Broiler.Launcher.Installation;

/// <summary>Stable component operations must leave the managed scope on an approved Office vector.</summary>
public static class OfficeCompatibility
{
    public static IReadOnlyDictionary<string, InstallReceipt?> CapturePeers(string targetId, InstallScope scope, IInstallationInventory inventory) =>
        OfficeCatalog.ProductIds.Where(id => id != targetId).ToDictionary(id => id, id => inventory.Find(id, scope).Receipt);

    public static string? PeersChanged(IReadOnlyDictionary<string, InstallReceipt?> expected, InstallScope scope, IInstallationInventory inventory)
    {
        foreach (var (id, receipt) in expected)
        {
            OfficeCatalog.ValidateId(id);
            var actual = inventory.Find(id, scope);
            if (actual.State == InstallationState.Invalid || actual.Receipt != receipt)
                return "Another product changed during BOSS readiness. Automatic rollback was deferred to preserve the current Office combination.";
        }
        return null;
    }
    public static string? BlockingReason(OfficeRelease release, string targetId, InstallScope scope, IInstallationInventory inventory)
    {
        if (release.Channel != "stable") return null;
        foreach (string id in OfficeCatalog.ProductIds)
        {
            if (id == targetId) continue;
            var installed = inventory.Find(id, scope);
            if (installed.State == InstallationState.NotInstalled) continue;
            var approved = release.Components.SingleOrDefault(c => c.Id == id && c.Rid == installed.Receipt?.Rid);
            if (installed.State != InstallationState.Installed || installed.Receipt is not { } receipt || approved is null ||
                approved.Version != receipt.Version || approved.Sha256 != receipt.Sha256)
                return $"Stable operation deferred: {id} in this scope does not match the Office-approved combination. Select a compatible Office release or have the combination integration-tested first.";
        }
        return null;
    }
}
