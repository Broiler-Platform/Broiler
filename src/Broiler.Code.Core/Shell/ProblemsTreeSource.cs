using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Code.Core.Diagnostics;
using Broiler.UI.CodeEditor;
using Broiler.UI.TreeView;

namespace Broiler.Code.Core.Shell;

/// <summary>
/// Presents the Problems model as a tree grouped by document.
///
/// A tree rather than a flat list because that is what the data is: diagnostics
/// belong to files. Reusing <see cref="UiTreeView"/> also means the Problems
/// pane inherits its virtualization, keyboard navigation, and the semantics
/// that announce a row's level and position — none of which would exist in a
/// bespoke list.
/// </summary>
public sealed class ProblemsTreeSource(ProblemsModel model) : IObservableTreeDataSource
{
    private readonly ProblemsModel _model = model ?? throw new ArgumentNullException(nameof(model));
    private readonly Dictionary<string, ProblemEntry> _entriesByNode = [];
    private readonly List<string> _documents = [];
    private readonly Dictionary<string, List<string>> _children = [];
    private bool _valid;

    public event EventHandler<TreeDataChangedEventArgs>? DataChanged;

    public TreeNodeId Root => new("problems");

    /// <summary>The entry a node stands for, or null for a document group.</summary>
    public ProblemEntry? EntryFor(TreeNodeId node)
    {
        EnsureBuilt();
        return _entriesByNode.GetValueOrDefault(node.Value);
    }

    public void Refresh()
    {
        _valid = false;
        DataChanged?.Invoke(this, new TreeDataChangedEventArgs(TreeNodeId.None));
    }

    public int GetChildCount(TreeNodeId node)
    {
        EnsureBuilt();
        if (node.Value == Root.Value)
            return _documents.Count;
        return _children.TryGetValue(node.Value, out List<string>? children) ? children.Count : 0;
    }

    public TreeNodeId GetChild(TreeNodeId node, int index)
    {
        EnsureBuilt();
        return new TreeNodeId(node.Value == Root.Value
            ? $"doc:{_documents[index]}"
            : _children[node.Value][index]);
    }

    public bool CanExpand(TreeNodeId node) => GetChildCount(node) > 0;

    public TreeNodePresentation GetPresentation(TreeNodeId node)
    {
        EnsureBuilt();

        if (_entriesByNode.TryGetValue(node.Value, out ProblemEntry? entry))
        {
            return new TreeNodePresentation(
                node,
                entry.Message,
                entry.IsProjectLevel
                    ? entry.Code
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{entry.Code}  line {entry.Line + 1}"),
                "diagnostic",
                entry.Severity switch
                {
                    CodeDiagnosticSeverity.Error => TreeNodeDecoration.Error,
                    CodeDiagnosticSeverity.Warning => TreeNodeDecoration.Warning,
                    CodeDiagnosticSeverity.Information => TreeNodeDecoration.Information,
                    _ => TreeNodeDecoration.None,
                });
        }

        string document = node.Value.StartsWith("doc:", StringComparison.Ordinal)
            ? node.Value[4..]
            : node.Value;
        int count = _children.TryGetValue(node.Value, out List<string>? children) ? children.Count : 0;
        return new TreeNodePresentation(node, ShortName(document), $"{count}", "file");
    }

    private void EnsureBuilt()
    {
        if (_valid)
            return;

        _entriesByNode.Clear();
        _documents.Clear();
        _children.Clear();

        int ordinal = 0;
        foreach ((string document, IReadOnlyList<ProblemEntry> entries) in _model.GetGroupedByDocument())
        {
            string documentKey = $"doc:{document}";
            _documents.Add(document);
            var childKeys = new List<string>(entries.Count);

            foreach (ProblemEntry entry in entries)
            {
                // Keyed by ordinal rather than by content: two identical
                // diagnostics on one line are two rows, and a key collision
                // would silently drop one.
                string key = $"entry:{ordinal++}";
                _entriesByNode[key] = entry;
                childKeys.Add(key);
            }

            _children[documentKey] = childKeys;
        }

        _valid = true;
    }

    private static string ShortName(string path)
    {
        int slash = path.LastIndexOfAny(['/', '\\']);
        return slash < 0 ? path : path[(slash + 1)..];
    }
}
