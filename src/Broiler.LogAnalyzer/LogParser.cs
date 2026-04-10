using System.Globalization;
using System.Text.RegularExpressions;

namespace Broiler.LogAnalyzer;

/// <summary>
/// Parses Apache access log lines in Common and Combined log formats.
/// </summary>
internal static partial class LogParser
{
    // Apache Combined Log Format:
    // %h %l %u %t "%r" %>s %b "%{Referer}i" "%{User-agent}i"
    //
    // Apache Common Log Format (subset without Referer/User-Agent):
    // %h %l %u %t "%r" %>s %b
    [GeneratedRegex(
        @"^(?<host>\S+)\s+"        +  // remote host
        @"(?<ident>\S+)\s+"        +  // RFC 1413 identity (usually -)
        @"(?<user>\S+)\s+"         +  // authenticated user (usually -)
        @"\[(?<time>[^\]]+)\]\s+"  +  // [day/month/year:hour:min:sec zone]
        @"""(?<method>\S+)\s+"     +  // "METHOD
        @"(?<endpoint>\S+)\s+"     +  //  /path
        @"(?<protocol>[^""]*)""\s+"+  //  HTTP/x.x"
        @"(?<status>\d{3})\s+"     +  // status code
        @"(?<size>\S+)"            +  // response size (or -)
        @"(?:\s+""(?<referer>[^""]*)""\s+""(?<agent>[^""]*)"")?", // optional referer & user-agent
        RegexOptions.Compiled)]
    private static partial Regex AccessLogPattern();

    private const string TimestampFormat = "dd/MMM/yyyy:HH:mm:ss zzz";

    /// <summary>
    /// Attempts to parse a single log line into a <see cref="LogEntry"/>.
    /// Returns <c>null</c> if the line does not match the expected format.
    /// </summary>
    internal static LogEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var match = AccessLogPattern().Match(line);
        if (!match.Success)
            return null;

        if (!DateTimeOffset.TryParseExact(
                match.Groups["time"].Value,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            return null;
        }

        if (!int.TryParse(match.Groups["status"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var status))
            return null;

        var sizeStr = match.Groups["size"].Value;
        long size = sizeStr == "-" ? 0 : long.TryParse(sizeStr, NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : 0;

        var referer = match.Groups["referer"].Success ? match.Groups["referer"].Value : null;
        var agent = match.Groups["agent"].Success ? match.Groups["agent"].Value : null;

        return new LogEntry(
            RemoteHost: match.Groups["host"].Value,
            Ident: match.Groups["ident"].Value,
            User: match.Groups["user"].Value,
            Timestamp: timestamp,
            Method: match.Groups["method"].Value,
            Endpoint: match.Groups["endpoint"].Value,
            Protocol: match.Groups["protocol"].Value,
            StatusCode: status,
            ResponseSize: size,
            Referer: referer == "-" ? null : referer,
            UserAgent: agent == "-" ? null : agent);
    }

    /// <summary>
    /// Parses all valid entries from the given lines, skipping malformed lines.
    /// </summary>
    internal static IReadOnlyList<LogEntry> ParseLines(IEnumerable<string> lines)
    {
        var entries = new List<LogEntry>();
        foreach (var line in lines)
        {
            var entry = ParseLine(line);
            if (entry is not null)
                entries.Add(entry);
        }
        return entries;
    }
}
