using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.Services.Boss;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed class BossProbeAndResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boss-probe-test-" + Guid.NewGuid().ToString("N"));
    public BossProbeAndResolverTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task ReadyProbeRequiresIdentityShellAndAllImportMapModulesIncludingRootMain()
    {
        using var client = new HttpClient(new Handler());
        var result = await new BossHealthProbe(client).ProbeAsync(new("http://127.0.0.1:5555/"), default);
        Assert.Equal(BossProbeKind.Healthy, result.Kind);
        Assert.Equal("fixture-id", result.InstanceId);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("shell")]
    [InlineData("runtime")]
    [InlineData("escape")]
    [InlineData("large")]
    public async Task ProbeRejectsWrongIdentityMissingBundleAndUnsafeResponses(string failure)
    {
        using var client = new HttpClient(new Handler(failure));
        var result = await new BossHealthProbe(client).ProbeAsync(new("http://127.0.0.1:5555/"), default);
        Assert.Equal(BossProbeKind.Conflict, result.Kind);
    }

    [Fact]
    public async Task RealRefusedLoopbackConnectionIsAbsentRatherThanHealthyOrConflicting()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var result = await BossHealthProbe.CreateDefault().ProbeAsync(new($"http://127.0.0.1:{port}/"), default);
        Assert.Equal(BossProbeKind.Absent, result.Kind);
    }

    private string Payload(string directory, bool dll = false)
    {
        Directory.CreateDirectory(Path.Combine(directory, "wwwroot", "_framework"));
        File.WriteAllText(Path.Combine(directory, "wwwroot", "index.html"), "fixture");
        File.WriteAllText(Path.Combine(directory, "wwwroot", "_framework", "runtime.js"), "fixture");
        string path = Path.Combine(directory, dll ? "Broiler.Office.Server.dll" : OperatingSystem.IsWindows() ? "Broiler.Office.Server.exe" : "Broiler.Office.Server");
        File.WriteAllText(path, "fixture");
        if (dll) File.WriteAllText(Path.ChangeExtension(path, ".runtimeconfig.json"), "{}");
        return path;
    }

    [Fact]
    public void ResolverFindsIsolatedPortableAndHonorsCustomDllWithoutFallback()
    {
        string launcher = Path.Combine(_root, "launcher");
        Directory.CreateDirectory(launcher);
        string portable = Payload(Path.Combine(_root, "products", "boss"));
        var resolver = new BossResolver(new EmptyInstallationInventory(), launcher, launcher);
        Assert.Equal(portable, resolver.Resolve(new()).Path);
        string custom = Payload(Path.Combine(_root, "custom"), dll: true);
        var result = resolver.Resolve(new() { UseAutoDetect = false, ExecutablePath = custom });
        Assert.True(result.WithDotnet);
        Assert.Equal("Custom", result.Origin);
        File.Delete(custom);
        Assert.False(resolver.Resolve(new() { UseAutoDetect = false, ExecutablePath = custom }).Available);
    }

    [Fact]
    public void MissingWebAssetsAreNotReady()
    {
        string custom = Payload(Path.Combine(_root, "custom"));
        File.Delete(Path.Combine(_root, "custom", "wwwroot", "index.html"));
        var resolver = new BossResolver(new EmptyInstallationInventory(), _root, _root);
        Assert.Contains("web bundle", resolver.Resolve(new() { UseAutoDetect = false, ExecutablePath = custom }).Error!);
    }

    [Fact]
    public void BossSettingsCloneAndNullMigrationPreserveExistingProductSettings()
    {
        var settings = new LauncherSettings { Boss = new() { ListenUrl = "http://127.0.0.1:7777/", KeepRunningOnLauncherExit = false } };
        var clone = settings.Clone();
        clone.Boss.ListenUrl = "http://127.0.0.1:8888/";
        Assert.Equal("http://127.0.0.1:7777/", settings.Boss.ListenUrl);
        string file = Path.Combine(_root, "settings.json");
        File.WriteAllText(file, "{\"Apps\":{},\"Boss\":null}");
        Assert.Equal("http://127.0.0.1:5555/", new LauncherSettingsStore(file).Load().Boss.ListenUrl);
    }

    private sealed class Handler(string? failure = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            string text = path switch
            {
                "/healthz" => failure == "large" ? new string('a', 200) : "OK",
                "/api/info" => failure == "identity" ? "{\"server\":\"other\"}" : "{\"server\":\"BOSS\",\"product\":\"Broiler Office Standalone Server\",\"launcherInstanceId\":\"fixture-id\"}",
                "/" => failure == "shell" ? "missing" : "<script type=\"importmap\">{\"imports\":{\"runtime\":\"./_framework/runtime.hash.js\",\"main\":\"" + (failure == "escape" ? "http://outside.example/main.js" : "./m.hash.js") + "\"}}</script>",
                _ => failure == "runtime" ? "<html>fallback</html>" : "export {};",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8,
                path.EndsWith(".js") && failure != "runtime" ? "application/javascript" : "text/html") });
        }
    }
}
