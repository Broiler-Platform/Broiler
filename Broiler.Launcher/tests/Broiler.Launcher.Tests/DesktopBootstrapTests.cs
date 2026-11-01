using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Broiler.Launcher.Installation;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed class DesktopBootstrapTests
{
    [Fact]
    public void Bootstrap_is_an_explicit_demo_profile()
        => Assert.True(!OfficeBuild.IsBootstrapDemo || OfficeBuild.IsDemo);

    [Fact]
    public async Task Missing_demo_archive_can_only_be_downloaded_by_bootstrap_build()
    {
        string root = Path.Combine(Path.GetTempPath(), "broiler-bootstrap-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] payload = [1, 2, 3, 4];
            var component = new OfficeComponent("writer", "0.1.0-preview.16", "win-x64", "Writer.zip", payload.Length,
                Convert.ToHexStringLower(SHA256.HashData(payload)), "zip", "Broiler.Writer.Windows.exe", "native", "self-contained");
            var release = new OfficeRelease(1, "0.1.0-demo.6", "preview", "office-v0.1.0-demo.6", new string('a', 40), "0.1.0-demo.1", [component]) { Demo = true };
            File.WriteAllText(Path.Combine(root, "office-release.json"), JsonSerializer.Serialize(release, OfficeJson.Options));
            var source = new BundledOfficePackageSource(root, new OfficeReleaseVerifier(new Dictionary<string, string>(), "0.1.0-demo.6", allowUnsignedDemo: true));
            var package = new VerifiedPackage(release, component, Path.Combine(root, component.Asset));
            using var client = new HttpClient(new BytesHandler(payload));
            var downloader = new OfficePackageDownloader(client, Path.Combine(root, "cache"));
            if (!OfficeBuild.IsBootstrapDemo)
            {
                Assert.Throws<InvalidDataException>(() => source.Read("writer"));
                await Assert.ThrowsAsync<InvalidDataException>(() => downloader.EnsureArchiveAsync(package));
                return;
            }
            Assert.Equal(component, source.Read("writer").Component);
            var downloaded = await downloader.EnsureArchiveAsync(package);
            Assert.Equal(payload, File.ReadAllBytes(downloaded.ArchivePath));
            var badComponent = component with { Sha256 = new string('0', 64) };
            await Assert.ThrowsAsync<InvalidDataException>(() => downloader.EnsureArchiveAsync(package with { Component = badComponent }));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class BytesHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.StartsWith("https://github.com/Broiler-Platform/Broiler/releases/download/office-v0.1.0-demo.6/", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
        }
    }
}
