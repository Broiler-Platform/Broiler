using System.Text;

namespace Broiler.LogAnalyzer;

/// <summary>
/// Formats accessed endpoints into a sorted tree with per-file access counts.
/// </summary>
public static class AccessedFilesTreeFormatter
{
    public static string Format(IReadOnlyList<LogEntry> entries)
    {
        if (entries.Count == 0)
            return string.Empty;

        var accessPaths = entries
            .Select(entry => AccessPath.Parse(entry.Endpoint))
            .Where(path => path is not null)
            .Cast<AccessPath>()
            .ToList();

        if (accessPaths.Count == 0)
            return string.Empty;

        var allAbsolute = accessPaths.All(path => path.IsAbsolute);
        var anyAbsolute = accessPaths.Any(path => path.IsAbsolute);
        var commonPrefix = GetCommonPrefix(accessPaths, allAbsolute);

        var root = new AccessTreeNode(string.Empty);
        foreach (var accessPath in accessPaths)
        {
            var segments = accessPath.Segments.Skip(commonPrefix.Count).ToArray();
            AddPath(root, segments);
        }

        var builder = new StringBuilder();
        builder.Append(GetRootLabel(commonPrefix, allAbsolute, anyAbsolute));
        if (root.Count > 0)
            builder.Append($" ({root.Count})");
        builder.AppendLine();

        var children = GetSortedChildren(root);
        for (int i = 0; i < children.Count; i++)
        {
            AppendTreeNode(builder, children[i], string.Empty, i == children.Count - 1);
        }

        return builder.ToString();
    }

    private static void AddPath(AccessTreeNode root, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
        {
            root.Count++;
            return;
        }

        var current = root;
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (!current.Children.TryGetValue(segment, out var child))
            {
                child = new AccessTreeNode(segment);
                current.Children[segment] = child;
            }

            current = child;
            if (i == segments.Count - 1)
                current.Count++;
        }
    }

    private static void AppendTreeNode(StringBuilder builder, AccessTreeNode node, string prefix, bool isLast)
    {
        builder.Append(prefix);
        builder.Append(isLast ? "└─ " : "├─ ");
        builder.Append(node.Name);
        if (node.Count > 0)
            builder.Append($" ({node.Count})");
        builder.AppendLine();

        var childPrefix = prefix + (isLast ? "   " : "│  ");
        var children = GetSortedChildren(node);
        for (int i = 0; i < children.Count; i++)
        {
            AppendTreeNode(builder, children[i], childPrefix, i == children.Count - 1);
        }
    }

    private static List<AccessTreeNode> GetSortedChildren(AccessTreeNode node)
    {
        return node.Children.Values
            .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(child => child.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> GetCommonPrefix(IReadOnlyList<AccessPath> paths, bool allAbsolute)
    {
        if (!allAbsolute || paths.Count == 0)
            return [];

        var prefix = new List<string>(paths[0].Segments);
        for (int i = 1; i < paths.Count && prefix.Count > 0; i++)
        {
            var segments = paths[i].Segments;
            int shared = 0;
            while (shared < prefix.Count &&
                   shared < segments.Length &&
                   string.Equals(prefix[shared], segments[shared], StringComparison.Ordinal))
            {
                shared++;
            }

            prefix.RemoveRange(shared, prefix.Count - shared);
        }

        return prefix;
    }

    private static string GetRootLabel(IReadOnlyList<string> commonPrefix, bool allAbsolute, bool anyAbsolute)
    {
        if (commonPrefix.Count > 0)
            return $"{(allAbsolute ? "/" : string.Empty)}{string.Join('/', commonPrefix)}";

        if (anyAbsolute)
            return "/";

        return ".";
    }

    private sealed record AccessPath(bool IsAbsolute, string[] Segments)
    {
        public static AccessPath? Parse(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return null;

            var path = endpoint.Trim();
            int queryIndex = path.IndexOf('?');
            if (queryIndex >= 0)
                path = path[..queryIndex];

            if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
                path = absoluteUri.AbsolutePath;

            path = path.Replace('\\', '/');
            var isAbsolute = path.StartsWith("/", StringComparison.Ordinal);
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            return new AccessPath(isAbsolute, segments);
        }
    }

    private sealed class AccessTreeNode(string name)
    {
        public string Name { get; } = name;

        public int Count { get; set; }

        public Dictionary<string, AccessTreeNode> Children { get; } = new(StringComparer.Ordinal);
    }
}
