namespace Broiler.Launcher.Browser;

public sealed record BrowserProduct(string Id, string Name, Uri? Address)
{
    public bool CanLaunch => Address is not null;
}
public sealed record BrowserCapabilities
{
    public bool CanInstall => false;
    public bool CanStartServer => false;
    public bool CanUpdateNativeProducts => false;
    public bool CanConfigureExecutablePaths => false;
}
public interface IBrowserPreferences
{
    string? Read(string key);
    void Write(string key, string value);
}

/// <summary>No dependency on Launcher.Core, filesystem discovery, installation or process services.</summary>
public sealed class BrowserLauncher(Action<Uri> navigate, IBrowserPreferences preferences, BrowserDeploymentInfo? deployment = null)
{
    public BrowserCapabilities Capabilities { get; } = new();
    public IReadOnlyList<BrowserProduct> Products { get; } = Array.AsReadOnly(new[] {
        new BrowserProduct("browser", "Broiler Browser", null),
        new BrowserProduct("writer", "Broiler Writer", new Uri(deployment is { Approved: true } ? deployment.WriterUrl : "https://writer.broiler.app/")),
        new BrowserProduct("cell", "Broiler Cell", new Uri(deployment is { Approved: true } ? deployment.CellUrl : "https://cell.broiler.app/")),
        new BrowserProduct("plate", "Broiler Plate", null),
    });

    public bool Launch(string id)
    {
        var product = Products.FirstOrDefault(item => item.Id == id);
        if (product?.Address is null) return false;
        navigate(product.Address);
        return true;
    }

    public string Theme => preferences.Read("office.web.theme") is "dark" ? "dark" : "light";
    public bool OpenInNewTab => preferences.Read("office.web.openNewTab") == "true";
    public void SetOpenInNewTab(bool enabled) => preferences.Write("office.web.openNewTab", enabled ? "true" : "false");
    public void SetTheme(string theme)
    {
        if (theme is not ("light" or "dark")) throw new ArgumentException("Unknown theme.", nameof(theme));
        preferences.Write("office.web.theme", theme);
    }
}

