using System;

namespace Broiler.Launcher.Models;

/// <summary>
/// Outcome of launching an application process.
/// </summary>
public sealed record LaunchResult(
    bool Succeeded,
    int? ProcessId,
    string Message,
    string? Path,
    Exception? Error = null)
{
    public static LaunchResult Success(int processId, string message, string path) =>
        new(Succeeded: true, ProcessId: processId, Message: message, Path: path);

    public static LaunchResult Failure(string message, Exception? error = null) =>
        new(Succeeded: false, ProcessId: null, Message: message, Path: null, Error: error);
}
