using System.IO.Compression;

namespace Broiler.LogAnalyzer;

/// <summary>
/// Discovers Apache access log files in a directory, including rotated
/// files (access.log.1, access.log.2, …) and gzip-compressed variants
/// (access.log.2.gz, access.log.3.gz, …).
/// </summary>
public static class LogFileDiscovery
{
    /// <summary>
    /// Given a path that is either a single file or a directory, returns all
    /// access log files found, sorted in natural log-rotation order
    /// (current log first, then .1, .2, … then .1.gz, .2.gz, …).
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
            .OrderBy(GetSortKey)
            .ToList();

        return files;
    }

    /// <summary>
    /// Returns true if the filename looks like an Apache access log
    /// (access.log, access.log.N, access.log.N.gz, access_log variants).
    /// </summary>
    public static bool IsAccessLogFile(string filePath)
    {
        var name = Path.GetFileName(filePath);
        bool isGz = name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

        // Strip trailing .gz to check the base name
        var baseName = isGz ? name[..^3] : name;

        // Exact match: access.log or access_log (only plain files, not bare .gz)
        if (!isGz &&
            (baseName.Equals("access.log", StringComparison.OrdinalIgnoreCase) ||
             baseName.Equals("access_log", StringComparison.OrdinalIgnoreCase)))
            return true;

        // Rotated: access.log.N or access_log.N (plain or .gz)
        if (baseName.StartsWith("access.log.", StringComparison.OrdinalIgnoreCase) ||
            baseName.StartsWith("access_log.", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = baseName.StartsWith("access.log.", StringComparison.OrdinalIgnoreCase)
                ? baseName["access.log.".Length..]
                : baseName["access_log.".Length..];
            return int.TryParse(suffix, out _);
        }

        return false;
    }

    /// <summary>
    /// Returns true if the file is gzip-compressed (ends with .gz).
    /// </summary>
    public static bool IsGzipFile(string filePath) =>
        filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);

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
    /// access.log → (0, 0), access.log.1 → (0, 1), access.log.5.gz → (0, 5)
    /// </summary>
    private static (int Group, int Index) GetSortKey(string filePath)
    {
        var name = Path.GetFileName(filePath);
        var baseName = name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? name[..^3]
            : name;

        // Current log (no rotation number)
        if (baseName.Equals("access.log", StringComparison.OrdinalIgnoreCase) ||
            baseName.Equals("access_log", StringComparison.OrdinalIgnoreCase))
            return (0, 0);

        // Extract rotation number
        var dotIdx = baseName.LastIndexOf('.');
        if (dotIdx >= 0 && int.TryParse(baseName[(dotIdx + 1)..], out var idx))
            return (0, idx);

        return (1, 0);
    }
}
