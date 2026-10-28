using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Broiler.Launcher.Models;

namespace Broiler.Launcher.Installation;

public sealed record OfficeReleaseCandidate(OfficeRelease Release, string PackageDirectory, bool Compatible, bool Local)
{
    public Uri ReleasePage => new("https://github.com/Broiler-Platform/Broiler/releases/tag/" + Uri.EscapeDataString(Release.Tag));
}
public sealed record ReleaseCheck(IReadOnlyList<OfficeReleaseCandidate> Releases, string Message);
public sealed record UpdateCheckStatus(DateTimeOffset? LastAttempt = null, DateTimeOffset? LastSuccess = null, string? LastError = null);
public interface IReleaseCatalogClient
{
    UpdateCheckStatus Status => new();
    Task<ReleaseCheck> CheckAsync(string channel, bool force = false, CancellationToken token = default);
    Task<ReleaseCheck> CheckAsync(UpdateSettings preferences, bool force = false, CancellationToken token = default) => CheckAsync(preferences.Channel, force, token);
    OfficeReleaseCandidate ReadLocal(string directory);
}

/// <summary>Public Office releases only. Cached metadata is always reverified before it confers eligibility.</summary>
public sealed class OfficeReleaseCatalog(HttpClient client, string cacheDirectory, OfficeReleaseVerifier verifier,
    Func<DateTimeOffset>? clock = null) : IReleaseCatalogClient
{
    private const string Endpoint = "https://api.github.com/repos/Broiler-Platform/Broiler/releases";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset Now => clock?.Invoke() ?? DateTimeOffset.UtcNow;
    private sealed record CachePage(string? Etag, string Body, string? Next);
    private sealed record CheckState(DateTimeOffset LastSuccess, DateTimeOffset RetryAfter, string? Channel = null, DateTimeOffset? LastAttempt = null, string? LastError = null);
    public UpdateCheckStatus Status
    {
        get { var state = ReadJson<CheckState>("state.json"); return new(state?.LastAttempt, state?.LastSuccess > DateTimeOffset.MinValue ? state.LastSuccess : null, state?.LastError); }
    }

    public OfficeReleaseCandidate ReadLocal(string directory)
    {
        directory = Path.GetFullPath(directory);
        var manifest = OfficeInstallPaths.ReadBounded(Path.Combine(directory, "office-release.json"), 1024 * 1024);
        string signaturePath = Path.Combine(directory, "office-release.signature.json");
        var signature = File.Exists(signaturePath) ? OfficeInstallPaths.ReadBounded(signaturePath, 16 * 1024) : [];
        var release = verifier.Verify(manifest, signature, allowNewerLauncher: true);
        return new(release, directory, verifier.IsCompatible(release), true);
    }

    public Task<ReleaseCheck> CheckAsync(string channel, bool force = false, CancellationToken token = default) => CheckCoreAsync(channel, 24, force, token);
    public Task<ReleaseCheck> CheckAsync(UpdateSettings preferences, bool force = false, CancellationToken token = default) => CheckCoreAsync(preferences.Channel, Math.Clamp(preferences.CheckIntervalHours, 1, 168), force, token);
    private async Task<ReleaseCheck> CheckCoreAsync(string channel, int intervalHours, bool force, CancellationToken token)
    {
        if (channel is not ("stable" or "preview")) return new([], "Unknown release channel.");
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            OfficeInstallPaths.RejectLinks(cacheDirectory);
            Directory.CreateDirectory(cacheDirectory);
            var state = ReadJson<CheckState>("state.json") ?? new(DateTimeOffset.MinValue, DateTimeOffset.MinValue);
            bool cached = Now < state.RetryAfter || (!force && state.Channel == channel && Now - state.LastSuccess < TimeSpan.FromHours(intervalHours));
            if (!cached) { state = state with { LastAttempt = Now }; Save("state.json", state); }
            var releases = new List<OfficeReleaseCandidate>();
            int rejected = 0;
            string? url = Endpoint + "?per_page=100";
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            for (int page = 1; url is not null; page++)
            {
                if (page > 20) throw new InvalidDataException("Release catalog exceeds the pagination limit.");
                ValidatePageUrl(url);
                string filename = $"page-{page}.json";
                var old = ReadJson<CachePage>(filename);
                CachePage data;
                if (cached)
                {
                    if (old is null) return new([], "Update checks are deferred; use Check for updates later.");
                    data = old;
                }
                else
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.UserAgent.ParseAdd("Broiler-Office-Launcher/1.0");
                    request.Headers.Accept.ParseAdd("application/vnd.github+json");
                    request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
                    if (old?.Etag is { } etag) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                    if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                    {
                        var retry = response.Headers.RetryAfter?.Date ?? Now + (response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(15));
                        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset) && long.TryParse(reset.FirstOrDefault(), out long seconds))
                            retry = new[] { retry, DateTimeOffset.FromUnixTimeSeconds(seconds) }.Max();
                        Save("state.json", state with { RetryAfter = retry, LastError = $"GitHub rate limited until {retry.LocalDateTime:g}." });
                        return new(CachedReleases(channel), $"GitHub release checks are rate limited until {retry.LocalDateTime:g}.");
                    }
                    if (response.StatusCode == HttpStatusCode.NotModified && old is not null) data = old;
                    else
                    {
                        response.EnsureSuccessStatusCode();
                        string body = System.Text.Encoding.UTF8.GetString(await ReadBody(response, 4 * 1024 * 1024, deadline.Token));
                        string? next = null;
                        if (response.Headers.TryGetValues("Link", out var links))
                            foreach (string link in links.SelectMany(x => x.Split(',')))
                            {
                                var match = Regex.Match(link, "<([^>]+)>;\\s*rel=\"next\"");
                                if (match.Success) { next = match.Groups[1].Value; ValidatePageUrl(next); }
                            }
                        data = new(response.Headers.ETag?.ToString(), body, next);
                        Save(filename, data);
                    }
                }
                using var document = JsonDocument.Parse(data.Body);
                if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid release catalog.");
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    string tag = entry.GetProperty("tag_name").GetString() ?? "";
                    if (entry.GetProperty("draft").GetBoolean() || !tag.StartsWith("office-v", StringComparison.Ordinal)) continue;
                    OfficeVersion version;
                    try { version = OfficeVersion.Parse(tag[8..]); } catch { continue; }
                    bool preview = entry.GetProperty("prerelease").GetBoolean() || tag[8..].Split('+')[0].Contains('-');
                    if (channel == "stable" && preview) continue;
                    // Ignore source-only/NuGet releases without the Office contract.
                    var names = entry.GetProperty("assets").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToHashSet();
                    if (!names.Contains("office-release.json")) continue;
                    try
                    {
                        string directory = Path.Combine(cacheDirectory, tag);
                        if (!cached)
                        {
                            string prefix = "https://github.com/Broiler-Platform/Broiler/releases/download/" + Uri.EscapeDataString(tag) + "/";
                            byte[] manifest = await GetBytes(prefix + "office-release.json", 1024 * 1024, deadline.Token);
                            byte[] signature = names.Contains("office-release.signature.json")
                                ? await GetBytes(prefix + "office-release.signature.json", 16 * 1024, deadline.Token) : [];
                            var verified = verifier.Verify(manifest, signature, allowNewerLauncher: true);
                            if (verified.Tag != tag) throw new InvalidDataException("Release tag does not match its manifest.");
                            OfficeInstallPaths.RejectLinks(directory);
                            Directory.CreateDirectory(directory);
                            AtomicBytes(Path.Combine(directory, "office-release.json"), manifest);
                            AtomicBytes(Path.Combine(directory, "office-release.signature.json"), signature);
                        }
                        var candidate = ReadLocal(directory) with { Local = false };
                        if (candidate.Release.Tag != tag || (channel == "stable" && candidate.Release.Channel != "stable")) continue;
                        releases.Add(candidate);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or JsonException or FormatException or ArgumentException or System.Security.Cryptography.CryptographicException)
                    {
                        rejected++; // An untrusted/malformed release cannot suppress valid releases on later pages.
                    }
                }
                url = data.Next;
            }
            if (!cached) Save("state.json", new CheckState(Now, DateTimeOffset.MinValue, channel, Now,
                rejected == 0 ? null : $"{rejected} release(s) rejected by verification."));
            return new(releases.OrderByDescending(x => OfficeVersion.Parse(x.Release.OfficeVersion)).ToArray(),
                (cached ? "Using cached release information." : "Office release check completed.") +
                (rejected == 0 ? "" : $" {rejected} release(s) rejected by verification or compatibility checks."));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            try { var previous = ReadJson<CheckState>("state.json") ?? new(DateTimeOffset.MinValue, DateTimeOffset.MinValue); Save("state.json", previous with { RetryAfter = Now + TimeSpan.FromMinutes(15), LastError = ex.Message }); } catch { }
            return new(CachedReleases(channel), "Update check unavailable: " + ex.Message + " Launching installed products is still available.");
        }
        finally { _gate.Release(); }
    }

    private static void ValidatePageUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "api.github.com" ||
            !uri.IsDefaultPort || uri.AbsolutePath != "/repos/Broiler-Platform/Broiler/releases" || uri.UserInfo.Length != 0)
            throw new InvalidDataException("Unexpected release pagination URL.");
    }
    private IReadOnlyList<OfficeReleaseCandidate> CachedReleases(string channel)
    {
        var result = new List<OfficeReleaseCandidate>();
        for (int page = 1; page <= 20; page++)
        {
            var data = ReadJson<CachePage>($"page-{page}.json");
            if (data is null) break;
            try
            {
                using var document = JsonDocument.Parse(data.Body);
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    try
                    {
                        string tag = entry.GetProperty("tag_name").GetString() ?? "";
                        if (entry.GetProperty("draft").GetBoolean() || !tag.StartsWith("office-v", StringComparison.Ordinal)) continue;
                        _ = OfficeVersion.Parse(tag[8..]);
                        var candidate = ReadLocal(Path.Combine(cacheDirectory, tag)) with { Local = false };
                        if (candidate.Release.Tag != tag || (channel == "stable" && (entry.GetProperty("prerelease").GetBoolean() || candidate.Release.Channel != "stable"))) continue;
                        result.Add(candidate);
                    }
                    catch { } // A corrupt cache entry is never trusted.
                }
            }
            catch { }
            if (data.Next is null) break;
        }
        return result.OrderByDescending(x => OfficeVersion.Parse(x.Release.OfficeVersion)).ToArray();
    }
    private async Task<byte[]> GetBytes(string url, int limit, CancellationToken token)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadBody(response, limit, token).ConfigureAwait(false);
    }
    private static async Task<byte[]> ReadBody(HttpResponseMessage response, int limit, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Release response exceeds limits.");
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Release response exceeds limits.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    private T? ReadJson<T>(string name)
    {
        try { return JsonSerializer.Deserialize<T>(OfficeInstallPaths.ReadBounded(Path.Combine(cacheDirectory, name), 6 * 1024 * 1024), OfficeJson.Options); }
        catch { return default; }
    }
    private void Save<T>(string name, T value) => AtomicBytes(Path.Combine(cacheDirectory, name), JsonSerializer.SerializeToUtf8Bytes(value, OfficeJson.Options));
    private static void AtomicBytes(string path, byte[] bytes)
    {
        OfficeInstallPaths.RejectLinks(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, bytes); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
