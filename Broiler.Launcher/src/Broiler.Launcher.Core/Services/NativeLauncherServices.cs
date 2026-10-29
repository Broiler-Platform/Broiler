using Broiler.Launcher.Installation;
using Broiler.Launcher.Services.Boss;

namespace Broiler.Launcher.Services;

/// <summary>Desktop composition root. Browser heads must use Broiler.Launcher.Browser instead.</summary>
public sealed record NativeLauncherServices(IAppLauncherService Launcher, IProductInstallationService Installation,
    IBossController Boss, IReleaseCatalogClient Catalog, IUpdateService Updates)
{
    private static readonly Lazy<System.Net.Http.HttpClient> Http = new(() => new() { Timeout = TimeSpan.FromSeconds(45) });
    public static void EnsureAvailable()
    {
        if (OperatingSystem.IsBrowser()) throw new PlatformNotSupportedException("Native launcher services are unavailable in the browser. Use the browser composition root.");
    }

    public static IAppLauncherService ResolveLauncher(IAppLauncherService? launcher = null)
    {
        EnsureAvailable();
        return launcher ?? new AppLauncherService();
    }

    public static NativeLauncherServices Create(IAppLauncherService? launcher = null,
        IProductInstallationService? installation = null, IBossController? boss = null,
        IReleaseCatalogClient? catalog = null, IUpdateService? updates = null, IMachineUpdateBroker? broker = null)
    {
        EnsureAvailable();
        var service = ResolveLauncher(launcher);
        var server = boss ?? BossController.CreateDefault(service);
        var paths = catalog is null || updates is null ? OfficeInstallPaths.CreateDefault() : null;
        var releases = catalog ?? new OfficeReleaseCatalog(Http.Value,
            Path.Combine(paths!.UserRoot, "cache", "releases"), OfficeReleaseVerifier.CreateDefault());
        return new(service, installation ?? ProductInstallationService.CreateDefault(), server, releases,
            updates ?? new ProductUpdateService(paths!, OfficeReleaseVerifier.CreateDefault(), server, broker));
    }
}
