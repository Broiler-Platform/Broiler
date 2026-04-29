using System.IO.Compression;

namespace Broiler.LogAnalyzer;

/// <summary>
/// Discovers Apache access log files in a directory, including rotated
/// files (access.log.1, access.log.2, access.log.14.1, …), productive
/// current files (access.log.current), and gzip-compressed variants.
/// </summary>
public static class LogFileDiscovery
{
    /// <summary>
    /// Given a path that is either a single file or a directory, returns all
    /// access log files found, sorted in natural log-rotation order
    /// (current log first, then .1, .2, …), preferring gzip-compressed
    /// copies when both plain and compressed variants of the same log exist.
    /// </summary>
    public static IReadOnlyList<string> Resolve(string path)
    {
        if (File.Exists(path))
            return [path];

        if (!Directory.Exists(path))
            return [];

        // Match files that look like access logs:
        //   access.log, access.log.1, access.log.2.gz,
        //   access_log, access_log.1, etc.
        var files = Directory.GetFiles(path)
            .Where(IsAccessLogFile)
            .GroupBy(GetLogIdentity, StringComparer.OrdinalIgnoreCase)
            .Select(PreferCompressedVariant)
            .OrderBy(GetSortKey)
            .ToList();

        return files;
    }

    /// <summary>
    /// Returns true if the filename looks like an Apache access log
    /// (access.log, access.log.current, access.log.N, access.log.N.M.gz,
    /// access_log variants).
    /// </summary>
    public static bool IsAccessLogFile(string filePath) => TryGetSortKey(filePath, out _);

    /// <summary>
    /// Returns true if the file is gzip-compressed (ends with .gz).
    /// </summary>
    public static bool IsGzipFile(string filePath) =>
        filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

    private static string GetLogIdentity(string filePath)
    {
        var name = Path.GetFileName(filePath);
        return IsGzipFile(name) ? name[..^3] : name;
    }

    private static string PreferCompressedVariant(IEnumerable<string> variants)
    {
        return variants
            .OrderByDescending(IsGzipFile)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    /// <summary>
    /// Opens a stream reader for the file, decompressing gzip files automatically.
    /// </summary>
    public static StreamReader OpenReader(string filePath)
    {
        var stream = File.OpenRead(filePath);
        if (IsGzipFile(filePath))
        {
            var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            return new StreamReader(gzipStream);
        }
        return new StreamReader(stream);
    }

    /// <summary>
    /// Lazily reads lines from a file, decompressing gzip files automatically.
    /// </summary>
    public static IEnumerable<string> ReadLines(string filePath)
    {
        using var reader = OpenReader(filePath);
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    /// <summary>
    /// Sort key that keeps current log first, then numerically by rotation index.
    /// access.log.current → (0, "current", 0), access.log.14 → (1, "14", 0),
    /// access.log.14.1.gz → (1, "14.1", 1)
    /// </summary>
    private static (int Group, string RotationKey, int Compression, string Name) GetSortKey(string filePath)
    {
        return TryGetSortKey(filePath, out var sortKey)
            ? sortKey
            : (int.MaxValue, string.Empty, int.MaxValue, Path.GetFileName(filePath));
    }

    private static bool TryGetSortKey(string filePath, out (int Group, string RotationKey, int Compression, string Name) sortKey)
    {
        var name = Path.GetFileName(filePath);
        bool isGz = name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        var baseName = isGz ? name[..^3] : name;

        if (baseName.Equals("access.log", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("access_log", StringComparison.OrdinalIgnoreCase))
        {
            sortKey = (0, string.Empty, isGz ? 1 : 0, name);
            return !isGz;
        }

        if (baseName.Equals("access.log.current", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("access_log.current", StringComparison.OrdinalIgnoreCase))
        {
            sortKey = (0, "current", isGz ? 1 : 0, name);
            return true;
        }

        if (!TryGetRotationSuffix(baseName, out var suffix))
        {
            sortKey = default;
            return false;
        }

        var parsedNumericParts = suffix.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var value) ? value : (int?)null)
            .ToArray();
        if (parsedNumericParts.Length == 0 || parsedNumericParts.Any(part => part is null))
        {
            sortKey = default;
            return false;
        }

        sortKey = (
            1,
            string.Join('.', parsedNumericParts.Select(part => part!.Value.ToString("D10"))),
            isGz ? 1 : 0,
            name);
        return true;
    }

    private static bool TryGetRotationSuffix(string baseName, out string suffix)
    {
        if (baseName.StartsWith("access.log.", StringComparison.OrdinalIgnoreCase))
        {
            suffix = baseName["access.log.".Length..];
            return true;
        }

        if (baseName.StartsWith("access_log.", StringComparison.OrdinalIgnoreCase))
        {
            suffix = baseName["access_log.".Length..];
            return true;
        }

        suffix = string.Empty;
        return false;
    }
}
