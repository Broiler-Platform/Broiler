using Broiler.Launcher.Browser;
using Xunit;

namespace Broiler.Launcher.Tests;
public class BrowserDeploymentTests
{
    private const string Approved = """
        {"schemaVersion":1,"status":"approved","channel":"preview","catalogVersion":"office-demo-1","products":[
        {"id":"writer","url":"https://writer.broiler.app/","version":"writer-8"},
        {"id":"cell","url":"https://cell.broiler.app/","version":"cell-2"}]}
        """;
    [Fact] public void ApprovedDeploymentDisplaysIndependentProductVersions()
    {
        var info = BrowserDeploymentInfo.Read(Approved);
        Assert.True(info.Approved); Assert.Equal("writer-8", info.WriterVersion); Assert.Equal("cell-2", info.CellVersion);
        Assert.Equal("office-demo-1", info.CatalogVersion);
    }
    [Theory]
    [InlineData("{}")] [InlineData("null")] [InlineData("invalid")]
    public void InvalidMetadataFallsBackToLocalPreview(string json) => Assert.Equal(BrowserDeploymentInfo.LocalPreview, BrowserDeploymentInfo.Read(json));
    [Fact] public void UnapprovedMetadataCannotClaimProductVersions() => Assert.False(BrowserDeploymentInfo.Read(Approved.Replace("approved", "pending")).Approved);
    [Fact] public void MetadataCannotRedirectProducts() => Assert.False(BrowserDeploymentInfo.Read(Approved.Replace("https://writer.broiler.app/", "https://other.example/")).Approved);
    [Fact] public void DuplicateProductsFailClosed() => Assert.False(BrowserDeploymentInfo.Read(Approved.Replace("\"id\":\"cell\"", "\"id\":\"writer\"")).Approved);
    [Fact] public void ImmutableDestinationsPinTheDisplayedVersions()
    {
        var json = Approved.Replace("https://writer.broiler.app/", "https://writer.broiler.app/releases/writer-8/")
            .Replace("https://cell.broiler.app/", "https://cell.broiler.app/releases/cell-2/");
        var info = BrowserDeploymentInfo.Read(json);
        Assert.True(info.Approved);
        Assert.Equal("https://writer.broiler.app/releases/writer-8/", info.WriterUrl);
        Assert.False(BrowserDeploymentInfo.Read(json.Replace("releases/writer-8/", "releases/writer-9/")).Approved);
    }
    [Fact] public void WebsiteDestinationsUseExactProductAndVersionPaths()
    {
        var json = Approved.Replace("https://writer.broiler.app/", "https://broiler-platform.github.io/writer/releases/writer-8/")
            .Replace("https://cell.broiler.app/", "https://broiler-platform.github.io/cell/releases/cell-2/");
        Assert.True(BrowserDeploymentInfo.Read(json).Approved);
        Assert.False(BrowserDeploymentInfo.Read(json.Replace("/writer/releases/", "/demo/releases/")).Approved);
        Assert.False(BrowserDeploymentInfo.Read(json.Replace("broiler-platform.github.io", "other.github.io")).Approved);
    }
    [Fact] public void NullProductFailsClosed() => Assert.False(BrowserDeploymentInfo.Read(Approved.Replace("{\"id\":\"cell\",\"url\":\"https://cell.broiler.app/\",\"version\":\"cell-2\"}", "null")).Approved);
}
