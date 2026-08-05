using System;
using System.Collections.Generic;

namespace Broiler.Code.Workspaces.Storage;

/// <summary>
/// Normalization and containment for workspace-relative paths.
///
/// Every path that crosses into storage goes through here. A workspace opens
/// files a user did not write — a project can name any path it likes — so
/// "../../../../etc/passwd" and "src/../../outside" are inputs to handle, not
/// hypotheticals.
/// </summary>
public static class WorkspacePath
{
    /// <summary>
    /// Normalizes to forward slashes and resolves <c>.</c> and <c>..</c>
    /// segments. Returns null when the path escapes the root, rather than
    /// returning something that looks usable.
    /// </summary>
    public static string? Normalize(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        // A rooted or drive-qualified path is not relative, whatever it claims.
        if (relativePath.Length >= 2 && relativePath[1] == ':')
            return null;
        if (relativePath.StartsWith('/') || relativePath.StartsWith('\\'))
            return null;

        var segments = new List<string>();
        foreach (string raw in relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == ".")
                continue;
            if (raw == "..")
            {
                if (segments.Count == 0)
                    return null;
                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            // A trailing dot or space is stripped by Windows when the path is
            // opened, so "name." and "name" reach the same file. Treating them
            // as distinct lets one bypass a check made against the other.
            if (raw.TrimEnd('.', ' ').Length != raw.Length)
                return null;

            // Reserved device names resolve to a device rather than a file.
            if (IsReservedDeviceName(raw))
                return null;

            segments.Add(raw);
        }

        return segments.Count == 0 ? string.Empty : string.Join('/', segments);
    }

    /// <summary>
    /// True when <paramref name="candidate"/> is inside
    /// <paramref name="root"/>. The separator matters: without it, "src2"
    /// passes a prefix test against "src".
    /// </summary>
    public static bool IsContained(string candidate, string root)
    {
        if (root.Length == 0)
            return true;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;
        return candidate.Length == root.Length || candidate[root.Length] == '/';
    }

    /// <summary>
    /// A comparer for detecting collisions on a case-insensitive provider. Two
    /// paths differing only in case are the same file there and different files
    /// elsewhere, so the workspace has to know which rule its provider follows
    /// before it creates the second one.
    /// </summary>
    public static StringComparer GetComparer(bool caseInsensitive) =>
        caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static bool IsReservedDeviceName(string segment)
    {
        int dot = segment.IndexOf('.');
        string stem = dot < 0 ? segment : segment[..dot];
        return stem.ToUpperInvariant() switch
        {
            "CON" or "PRN" or "AUX" or "NUL" => true,
            ['C', 'O', 'M', >= '1' and <= '9'] => true,
            ['L', 'P', 'T', >= '1' and <= '9'] => true,
            _ => false,
        };
    }
}
