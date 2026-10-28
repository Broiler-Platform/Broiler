using Broiler.Launcher.Installation;

namespace Broiler.Launcher.Tests;

// Discovery/settings regressions must not depend on the developer's real installed suite.
internal sealed class EmptyInstallationInventory : IInstallationInventory
{
    public InstallationInfo Find(string productId, InstallScope scope) => new(productId, scope, InstallationState.NotInstalled);
}
