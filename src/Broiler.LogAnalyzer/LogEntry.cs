namespace Broiler.LogAnalyzer;

/// <summary>
/// Represents a single parsed Apache access log entry.
/// Supports both Common and Combined log formats.
/// </summary>
internal sealed record LogEntry(
    string RemoteHost,
    string Ident,
    string User,
    DateTimeOffset Timestamp,
    string Method,
    string Endpoint,
    string Protocol,
    int StatusCode,
    long ResponseSize,
    string? Referer,
    string? UserAgent);
