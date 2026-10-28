using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Broiler.Launcher.Services.Boss;

public enum BossProbeKind { Absent, Healthy, Conflict }
public sealed record BossProbeResult(BossProbeKind Kind, string? InstanceId = null, string? Error = null);
public interface IBossHealthProbe { Task<BossProbeResult> ProbeAsync(Uri address, CancellationToken token); }

public sealed class BossHealthProbe(HttpClient client) : IBossHealthProbe
{
    public static BossHealthProbe CreateDefault() => new(new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan });
    public async Task<BossProbeResult> ProbeAsync(Uri address, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            if ((await ReadAsync(new Uri(address, "healthz"), 32, deadline.Token)).Trim() != "OK") return new(BossProbeKind.Conflict, Error: "The listener is not a healthy BOSS server.");
            using var info = JsonDocument.Parse(await ReadAsync(new Uri(address, "api/info"), 65536, deadline.Token));
            var root = info.RootElement;
            if (!root.TryGetProperty("server", out var server) || server.GetString() != "BOSS" ||
                !root.TryGetProperty("product", out var product) || product.GetString() != "Broiler Office Standalone Server")
                return new(BossProbeKind.Conflict, Error: "The port is occupied by a different application.");
            string index = await ReadAsync(address, 1024 * 1024, deadline.Token);
            var map = Regex.Match(index, "<script\\s+type=\"importmap\"[^>]*>(.*?)</script>", RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            if (!map.Success) return new(BossProbeKind.Conflict, Error: "BOSS has no valid published web shell.");
            using var imports = JsonDocument.Parse(map.Groups[1].Value);
            if (!imports.RootElement.TryGetProperty("imports", out var modules) || modules.ValueKind != JsonValueKind.Object || !modules.EnumerateObject().Any())
                return new(BossProbeKind.Conflict, Error: "The BOSS web import map is empty.");
            foreach (var module in modules.EnumerateObject())
            {
                string relative = module.Value.GetString() ?? "";
                if (!Regex.IsMatch(relative, @"^\./(?:_framework/)?[A-Za-z0-9._-]+\.js\z") || relative.Contains("..", StringComparison.Ordinal))
                    return new(BossProbeKind.Conflict, Error: "Unexpected BOSS runtime module path.");
                using var response = await client.GetAsync(new Uri(address, relative), HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                if (!response.IsSuccessStatusCode || !(response.Content.Headers.ContentType?.MediaType?.Contains("javascript", StringComparison.OrdinalIgnoreCase) ?? false))
                    return new(BossProbeKind.Conflict, Error: "The BOSS runtime assets are missing or mismatched.");
            }
            string? id = root.TryGetProperty("launcherInstanceId", out var instance) && instance.ValueKind == JsonValueKind.String ? instance.GetString() : null;
            return new(BossProbeKind.Healthy, id);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
        { return new(BossProbeKind.Absent); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { return new(BossProbeKind.Conflict, Error: "The listener did not respond before the readiness deadline."); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or InvalidOperationException or RegexMatchTimeoutException)
        { return new(BossProbeKind.Conflict, Error: ex.Message); }
    }
    private async Task<string> ReadAsync(Uri uri, int maxBytes, CancellationToken token)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > maxBytes) throw new InvalidDataException("BOSS readiness response exceeds its size limit.");
            output.Write(buffer, 0, count);
        }
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }
}
