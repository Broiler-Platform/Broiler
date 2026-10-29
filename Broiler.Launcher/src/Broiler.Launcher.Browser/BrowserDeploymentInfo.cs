using System.Text.Json;
using System.Text.Json.Serialization;

namespace Broiler.Launcher.Browser;

/// <summary>Validated same-origin catalog metadata and allowlisted product release URLs.</summary>
public sealed record BrowserDeploymentInfo(string Channel, string CatalogVersion, string WriterVersion, string CellVersion, bool Approved)
{
    public string WriterUrl { get; init; } = "https://writer.broiler.app/";
    public string CellUrl { get; init; } = "https://cell.broiler.app/";
    public static BrowserDeploymentInfo LocalPreview { get; } = new("preview", "Local preview", "Preview", "Preview", false);
    public static BrowserDeploymentInfo Read(string json)
    {
        try {
            var data = JsonSerializer.Deserialize(json, DeploymentJsonContext.Default.DeploymentDocument);
            if (data is null || data.SchemaVersion != 1 || data.Status != "approved" ||
                !Label(data.CatalogVersion) || data.Channel is not ("preview" or "stable") || data.Products?.Length != 2)
                return LocalPreview;
            if (data.Products.Any(item => item is null)) return LocalPreview;
            var writer = data.Products.SingleOrDefault(item => item.Id == "writer");
            var cell = data.Products.SingleOrDefault(item => item.Id == "cell");
            if (writer is null || cell is null || !Destination(writer.Url, "writer", writer.Version) || !Destination(cell.Url, "cell", cell.Version) || !Label(writer.Version) || !Label(cell.Version))
                return LocalPreview;
            return new(data.Channel, data.CatalogVersion!, writer.Version!, cell.Version!, true) { WriterUrl = writer.Url!, CellUrl = cell.Url! };
        } catch (Exception error) when (error is JsonException or InvalidOperationException) { return LocalPreview; }
    }
    private static bool Label(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 80 && !value.Any(char.IsControl);
    private static bool Destination(string? url, string product, string? version) =>
        Label(version) && version!.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+') &&
        (url == $"https://{product}.broiler.app/" || url == $"https://{product}.broiler.app/releases/{version}/" ||
         url == $"https://broiler-platform.github.io/{product}/releases/{version}/");
}
internal sealed class DeploymentDocument
{
    public int SchemaVersion { get; set; }
    public string? Status { get; set; }
    public string? Channel { get; set; }
    public string? CatalogVersion { get; set; }
    public DeploymentProduct[]? Products { get; set; }
}
internal sealed class DeploymentProduct
{
    public string? Id { get; set; }
    public string? Url { get; set; }
    public string? Version { get; set; }
}
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DeploymentDocument))]
internal partial class DeploymentJsonContext : JsonSerializerContext { }
