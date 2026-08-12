using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Broiler.Wpt;

/// <summary>
/// WPT's server-side template substitution — the <c>sub</c> pipe every <c>*.sub.*</c> file is
/// served through — and the set of hosts the runner therefore serves from the checkout.
/// </summary>
/// <remarks>
/// <para>
/// A file whose name carries a <c>.sub</c> component is a <em>template</em>, not a document: the
/// WPT server expands <c>{{host}}</c>, <c>{{ports[http][0]}}</c> and friends before sending it
/// (<c>tools/wptserve/wptserve/pipes.py</c>). Read straight off disk the placeholders survive into
/// the markup, so a URL built from them addresses nothing — which is why WPT's cross-origin
/// reftests rendered an empty frame here and scored 0.0% against a reference whose frame was
/// empty for the same reason. There are ~1 400 <c>.sub.html</c> files in the tree.
/// </para>
/// <para>
/// The values are WPT's own defaults (<c>tools/serve/serve.py</c> <c>ConfigBuilder._default</c>,
/// <c>tools/wptserve/wptserve/config.py</c> <c>_get_domains</c>): primary host
/// <c>web-platform.test</c>, alternate host <c>not-web-platform.test</c>, and a subdomain set whose
/// two-deep products (<c>www.www1</c>, …) are generated the way <c>_make_subdomains_product</c>
/// generates them. Ports WPT allocates dynamically are pinned to the numbers its documentation
/// uses, because a render has to be reproducible.
/// </para>
/// <para>
/// <b>Only what a file on disk can answer is substituted.</b> <c>{{uuid()}}</c>,
/// <c>{{headers[…]}}</c>, <c>{{GET[…]}}</c>, <c>{{file_hash(…)}}</c> and <c>{{$var:…}}</c> describe
/// a live request; those placeholders are left exactly as they were read, so such a test renders
/// as it does today rather than with a fabricated value. <c>{{not_domains[…]}}</c> is left alone
/// for a stronger reason: it names a host that is *meant* not to resolve.
/// </para>
/// </remarks>
internal static class WptSubstitution
{
    /// <summary>The primary host tests are served from (<c>browser_host</c>).</summary>
    public const string PrimaryHost = "web-platform.test";

    /// <summary>The second origin, for cross-origin tests (<c>alternate_hosts["alt"]</c>).</summary>
    public const string AlternateHost = "not-web-platform.test";

    /// <summary>
    /// The port a substituted URL is given for each protocol. WPT fixes the first http and https
    /// ports and allocates the rest at start-up; a runner has no server to ask, and the number only
    /// has to round-trip through <see cref="ServedHosts"/>, so the documented ones are used.
    /// </summary>
    private static readonly Dictionary<string, int[]> Ports = new(StringComparer.Ordinal)
    {
        ["http"] = [8000, 8001],
        ["https"] = [8443, 8444],
        ["ws"] = [8888],
        ["wss"] = [8889],
        ["h2"] = [9000],
    };

    /// <summary>
    /// WPT's subdomain set, closed under the two-deep products <c>_make_subdomains_product</c>
    /// builds (<c>www</c>, <c>www.www1</c>, …), plus <c>""</c> for the bare host. Non-ASCII labels
    /// are IDNA-encoded here exactly as <c>_get_domains</c> encodes them.
    /// </summary>
    private static readonly string[] BaseSubdomains = ["www", "www1", "www2", "天気の良い日", "élève"];

    private static readonly Dictionary<string, Dictionary<string, string>> AllDomains = BuildAllDomains();

    /// <summary>
    /// Every host name <see cref="AllDomains"/> can produce, in the IDNA form a URL carries. These
    /// are the hosts the runner serves from the checkout: WPT gives them all the same document
    /// root, so an absolute URL on one of them names a file under the checkout.
    /// </summary>
    public static readonly string[] ServedHosts = AllDomains
        .SelectMany(alias => alias.Value.Values)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(host => host, StringComparer.Ordinal)
        .ToArray();

    /// <summary>Matches WPT's own template regex: <c>{{</c>, anything but <c>}</c>, <c>}}</c>.</summary>
    private static readonly System.Text.RegularExpressions.Regex TemplateRegex = new(@"\{\{([^}]*)\}\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>An identifier followed by zero or more <c>[…]</c> indexes, and nothing else.</summary>
    private static readonly System.Text.RegularExpressions.Regex FieldRegex = new(@"^([A-Za-z_][A-Za-z0-9_-]*)((?:\[[^\]]*\])*)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex IndexRegex = new(@"\[([^\]]*)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Whether <paramref name="path"/> names a template WPT would serve through the <c>sub</c>
    /// pipe. WPT's rule is a literal <c>".sub." in path</c>
    /// (<c>tools/wptserve/wptserve/handlers.py</c>), so it is a substring test and not an extension
    /// one: <c>x.sub.html</c> and <c>x.tentative.sub.html</c> are both templates, and the match is
    /// case-sensitive because Python's <c>in</c> is. Only the file name is examined — WPT tests the
    /// request path, and the difference is a checkout directory with <c>.sub.</c> in its own name,
    /// which would otherwise make every file beneath it a template.
    /// </summary>
    public static bool IsSubstitutionFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var name = System.IO.Path.GetFileName(path);
        return !string.IsNullOrEmpty(name) && name.Contains(".sub.", StringComparison.Ordinal);
    }

    /// <summary>
    /// Expands the placeholders in <paramref name="content"/> that a file on disk can answer,
    /// leaving every other placeholder untouched. <paramref name="rootRelativePath"/> is the
    /// document's own path under the checkout (<c>/css/…/x.sub.html</c>), which is what
    /// <c>{{location[…]}}</c> reports; pass <see langword="null"/> to leave those alone too.
    /// </summary>
    public static string Apply(string content, string? rootRelativePath = null)
    {
        if (string.IsNullOrEmpty(content) || !content.Contains("{{", StringComparison.Ordinal))
            return content;

        return TemplateRegex.Replace(content, match =>
        {
            var resolved = TryResolveField(match.Groups[1].Value, rootRelativePath);
            // An unsupported field keeps the placeholder verbatim: rendering a made-up value would
            // be a worse lie than rendering the template text the reference also shows.
            return resolved ?? match.Value;
        });
    }

    /// <summary>
    /// Reads <paramref name="path"/> and applies <see cref="Apply"/> when it is a template.
    /// <paramref name="wptRoot"/> anchors <c>{{location[…]}}</c>; without it the document's own URL
    /// is unknown and those placeholders are left alone.
    /// </summary>
    public static string ReadDocument(string path, string? wptRoot)
    {
        var content = System.IO.File.ReadAllText(path);
        return IsSubstitutionFile(path) ? Apply(content, TryRootRelativePath(path, wptRoot)) : content;
    }

    /// <summary>
    /// The root-relative URL path of <paramref name="path"/> under <paramref name="wptRoot"/>, or
    /// <see langword="null"/> when it lies outside the checkout.
    /// </summary>
    internal static string? TryRootRelativePath(string path, string? wptRoot)
    {
        if (string.IsNullOrEmpty(wptRoot))
            return null;

        try
        {
            var full = System.IO.Path.GetFullPath(path);
            // The separator matters: without it `/wpt` would claim `/wpt-scratch/x.sub.html`.
            var root = System.IO.Path.GetFullPath(wptRoot).TrimEnd(System.IO.Path.DirectorySeparatorChar)
                       + System.IO.Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return null;

            var relative = full[root.Length..].Replace(System.IO.Path.DirectorySeparatorChar, '/');
            return "/" + relative.TrimStart('/');
        }
        catch (Exception ex) when (ex is ArgumentException or System.IO.PathTooLongException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The root-relative form of an absolute URL on one of <see cref="ServedHosts"/>, or
    /// <see langword="null"/>. Shares the engine's implementation so the runner's resource loaders
    /// and its frame loading agree on what "served from the checkout" means.
    /// </summary>
    public static string? TryStripServedOrigin(string? url) =>
        Broiler.Layout.Engine.DocumentRoot.TryStripLocalOrigin(url, ServedHosts);

    private static string? TryResolveField(string expression, string? rootRelativePath)
    {
        // `{{$id:uuid()}}` / `{{$id}}` — a stash variable, which needs a request to have a value.
        if (expression.StartsWith("$", StringComparison.Ordinal))
            return null;

        var field = FieldRegex.Match(expression);
        if (!field.Success)
            return null;   // a function call, or something this parser does not model

        var name = field.Groups[1].Value;
        var indexes = IndexRegex.Matches(field.Groups[2].Value).Select(m => m.Groups[1].Value).ToArray();

        return name switch
        {
            "host" when indexes.Length == 0 => PrimaryHost,
            "hosts" when indexes.Length == 2 => TryDomain(indexes[0], indexes[1]),
            "domains" when indexes.Length == 1 => TryDomain("", indexes[0]),
            "ports" when indexes.Length == 2 => TryPort(indexes[0], indexes[1]),
            "location" when indexes.Length == 1 => TryLocation(indexes[0], rootRelativePath),
            _ => null,
        };
    }

    private static string? TryDomain(string alias, string subdomain) =>
        AllDomains.TryGetValue(alias, out var domains) && domains.TryGetValue(subdomain, out var host)
            ? host
            : null;

    private static string? TryPort(string protocol, string index) =>
        Ports.TryGetValue(protocol, out var ports)
        && int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var i)
        && i >= 0 && i < ports.Length
            ? ports[i].ToString(CultureInfo.InvariantCulture)
            : null;

    private static string? TryLocation(string key, string? rootRelativePath)
    {
        if (rootRelativePath is null)
            return null;

        var port = Ports["http"][0].ToString(CultureInfo.InvariantCulture);
        var pathOnly = rootRelativePath.Split('?', '#')[0];
        var query = rootRelativePath.Contains('?', StringComparison.Ordinal)
            ? "?" + rootRelativePath.Split('?')[1].Split('#')[0]
            : "?";

        return key switch
        {
            "server" => $"http://{PrimaryHost}:{port}",
            "scheme" => "http",
            "host" => $"{PrimaryHost}:{port}",
            "hostname" => PrimaryHost,
            "port" => port,
            "path" or "pathname" => pathOnly,
            "query" => query,
            _ => null,
        };
    }

    private static Dictionary<string, Dictionary<string, string>> BuildAllDomains()
    {
        var subdomains = MakeSubdomainProduct(BaseSubdomains, depth: 2);
        var hosts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [""] = PrimaryHost,
            ["alt"] = AlternateHost,
        };

        var idn = new IdnMapping();
        var all = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (alias, host) in hosts)
        {
            var domains = new Dictionary<string, string>(StringComparer.Ordinal) { [""] = host };
            foreach (var subdomain in subdomains)
                domains[subdomain] = idn.GetAscii(subdomain) + "." + host;

            all[alias] = domains;
        }

        return all;
    }

    /// <summary>
    /// WPT's <c>_make_subdomains_product</c>: every dot-joined tuple of the base labels up to
    /// <paramref name="depth"/> long, so <c>www</c> and <c>www.www1</c> are both valid subdomains.
    /// </summary>
    private static List<string> MakeSubdomainProduct(IReadOnlyList<string> labels, int depth)
    {
        var result = new List<string>();
        IEnumerable<string> level = labels;
        for (var i = 1; i <= depth; i++)
        {
            result.AddRange(level);
            level = level.SelectMany(prefix => labels.Select(label => prefix + "." + label)).ToList();
        }

        return result;
    }
}
