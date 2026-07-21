using System;
using System.Collections.Generic;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// Builds and links a document's ES module graph (Phase 7 item 6). Starting from the document's
/// <c>&lt;script type="module"&gt;</c> entries, it resolves each static import specifier to a URL, fetches
/// the dependency, recurses, dedups repeated modules, orders the graph dependency-first (cycle-safe), and
/// renders each module to a linked plain-JS program (via <see cref="EsModuleLinker"/>) that the existing
/// non-module evaluator runs in order. A module whose syntax the scanner cannot confidently transform
/// falls back to running as-is (today's behaviour), so the feature is strictly additive.
/// </summary>
internal static class ModuleGraphLoader
{
    /// <summary>A discovered/entry module: its registry key (resolved URL or synthetic inline id), its
    /// source text, and the base URL its relative imports resolve against.</summary>
    public readonly record struct GraphModule(string Key, string Source, string? BaseUrl);

    /// <summary>Resolves an import specifier (relative to <paramref name="baseUrl"/>) to its absolute key
    /// and fetches its source; returns <c>null</c> when it cannot be resolved/fetched/authorised.</summary>
    public delegate (string Key, string Source)? ResolveAndFetch(string specifier, string? baseUrl);

    /// <summary>The linked, dependency-ordered module programs to evaluate in sequence.</summary>
    public sealed record Result(IReadOnlyList<string> Programs, IReadOnlyList<string> OrderedKeys);

    private sealed class Node
    {
        public required string Key;
        public required string Source;
        public required string? BaseUrl;
        public required EsModuleSyntax Syntax;
        public readonly Dictionary<string, string?> DepKeys = new(StringComparer.Ordinal); // specifier -> resolved key
    }

    /// <summary>
    /// Loads and links the graph rooted at <paramref name="entries"/> (in document order). Entry modules
    /// are already resolved+fetched (inline modules carry a synthetic key); <paramref name="resolveAndFetch"/>
    /// loads their transitive dependencies.
    /// </summary>
    public static Result Load(IReadOnlyList<GraphModule> entries, ResolveAndFetch resolveAndFetch)
    {
        var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);

        Node Intern(GraphModule m)
        {
            if (nodes.TryGetValue(m.Key, out var existing))
                return existing;
            var node = new Node
            {
                Key = m.Key,
                Source = m.Source,
                BaseUrl = m.BaseUrl,
                Syntax = EsModuleScanner.Scan(m.Source),
            };
            nodes[m.Key] = node;
            return node;
        }

        var order = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        void LoadDependency(Node node, string specifier)
        {
            if (node.DepKeys.ContainsKey(specifier))
                return; // already resolved (e.g. imported both statically and dynamically)

            var loaded = resolveAndFetch(specifier, node.BaseUrl);
            if (loaded is not { } dep)
            {
                node.DepKeys[specifier] = null; // unresolved — a static import throws / a dynamic import rejects
                return;
            }

            node.DepKeys[specifier] = dep.Key;
            var depNode = Intern(new GraphModule(dep.Key, dep.Source, dep.Key));
            if (!visiting.Contains(depNode.Key))   // skip the back-edge of a cycle
                Visit(depNode);
        }

        void Visit(Node node)
        {
            if (!visited.Add(node.Key)) return;   // already fully processed
            visiting.Add(node.Key);

            if (node.Syntax.Supported)
            {
                // Static imports/re-exports, then dynamic import("literal") specifiers. Both are resolved,
                // fetched, and linked into the registry ahead of the importer so the module namespace exists
                // when it is needed (a dynamic import is loaded eagerly rather than on first call — a timing
                // approximation, but it makes import() resolve to the linked namespace).
                foreach (var specifier in node.Syntax.Dependencies())
                    LoadDependency(node, specifier);
                foreach (var specifier in node.Syntax.DynamicDependencies())
                    LoadDependency(node, specifier);
            }

            visiting.Remove(node.Key);
            order.Add(node.Key);                  // post-order: dependencies precede this module
        }

        foreach (var entry in entries)
            Visit(Intern(entry));

        var programs = new List<string>(order.Count);
        foreach (var key in order)
        {
            var node = nodes[key];
            programs.Add(node.Syntax.Supported
                ? EsModuleLinker.Render(node.Source, node.Syntax, node.Key,
                    spec => node.DepKeys.TryGetValue(spec, out var k) ? k : null)
                : ModuleScriptWrapper.WrapInlineModule(node.Source)); // fallback: today's behaviour
        }

        return new Result(programs, order);
    }
}
