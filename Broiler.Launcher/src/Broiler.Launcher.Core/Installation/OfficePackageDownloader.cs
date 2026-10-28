using System.Net.Http;
using System.Security.Cryptography;

namespace Broiler.Launcher.Installation;

/// <summary>Downloads only a component named by an already verified Office release.</summary>
public sealed class OfficePackageDownloader(HttpClient client, string cacheDirectory)
{
    public async Task<VerifiedPackage> EnsureArchiveAsync(VerifiedPackage package, IProgress<InstallProgress>? progress = null, CancellationToken token = default)
    {
        if (File.Exists(package.ArchivePath)) return package; // Installer rehashes the protected snapshot.
        if (package.Release.Demo) throw new InvalidDataException("Unsigned demo packages are local-only and cannot be downloaded automatically.");
        CancellationToken callerToken = token;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        token = timeout.Token;
        OfficeCatalog.ValidateComponent(package.Component);
        if (package.Release.Tag != "office-v" + package.Release.OfficeVersion) throw new InvalidDataException("Invalid Office release identity.");
        string cache = Path.GetFullPath(cacheDirectory);
        OfficeInstallPaths.RejectLinks(cache);
        Directory.CreateDirectory(cache);
        string destination = Path.Combine(cache, package.Component.Sha256 + ".zip");
        OfficeInstallPaths.RejectLinks(destination);
        if (File.Exists(destination))
        {
            await using var existing = File.OpenRead(destination);
            if (existing.Length == package.Component.Size && Convert.ToHexStringLower(await SHA256.HashDataAsync(existing, token)) == package.Component.Sha256)
                return package with { ArchivePath = destination };
        }
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            var uri = new Uri("https://github.com/Broiler-Platform/Broiler/releases/download/" +
                Uri.EscapeDataString(package.Release.Tag) + "/" + Uri.EscapeDataString(package.Component.Asset));
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != package.Component.Size)
                throw new InvalidDataException("Downloaded package size does not match the signed manifest.");
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int count, lastPercent = -1;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    total = checked(total + count);
                    if (total > package.Component.Size) throw new InvalidDataException("Downloaded package exceeds its declared size.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    int percent = (int)(total * 100 / package.Component.Size);
                    if (percent != lastPercent) { progress?.Report(new("Downloading product", percent)); lastPercent = percent; }
                }
                if (total != package.Component.Size) throw new InvalidDataException("Downloaded package is truncated.");
                output.Position = 0;
                if (Convert.ToHexStringLower(await SHA256.HashDataAsync(output, token)) != package.Component.Sha256)
                    throw new InvalidDataException("Downloaded package hash does not match the signed manifest.");
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
            return package with { ArchivePath = destination };
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw new TimeoutException("The Office package download timed out; retry when the connection is available.");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
