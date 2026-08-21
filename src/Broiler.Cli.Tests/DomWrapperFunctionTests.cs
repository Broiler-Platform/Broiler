namespace Broiler.Cli.Tests;

/// <summary>
/// A node's JS wrapper is built eagerly and per node — an element gets ~149 own members, each a
/// native callable. Those callables are WebIDL operations and attribute accessors, so per
/// <see href="https://webidl.spec.whatwg.org/#es-operations">WebIDL</see> they are ordinary
/// functions: not constructors, and carrying no <c>prototype</c> property. The bridge used to mint
/// them with <c>JSFunction</c>'s default <c>createPrototype: true</c>, which both diverged from the
/// spec and allocated a second throwaway <see cref="Broiler.JavaScript.Runtime.JSObject"/> (plus its
/// <c>constructor</c> back-reference) for every member of every node.
///
/// That was the top-ranked problem of the 2026-07-31 WPT run: <c>css/css-variables/url-syntax-crash.html</c>
/// appends 10,000 spans and was aborted for exceeding the 4096 MiB per-test memory limit. The cost
/// was never about <c>var()</c> or <c>@property</c> — 2,000 spans parsed from static markup are
/// free, while 2,000 bare <c>document.createElement("span")</c> calls (never inserted) cost ~0.5 MiB
/// each.
/// </summary>
public class DomWrapperFunctionTests
{
    private static string ExecJs(string jsCode)
    {
        var html = $@"<!doctype html>
<html><head><title>Test</title></head>
<body>
<div id=""result""></div>
<script>
{jsCode}
</script>
</body>
</html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com");
    }

    [Fact(Timeout = 600000)]
    public void Dom_Operations_Are_Not_Constructors_And_Have_No_Prototype()
    {
        var result = ExecJs(@"
            var el = document.createElement('span');
            var out = [];
            out.push('proto:' + typeof el.setAttribute.prototype);
            out.push('appendProto:' + typeof el.appendChild.prototype);
            var threw = false;
            try { new el.setAttribute(); } catch (e) { threw = true; }
            out.push('newThrows:' + threw);
            document.getElementById('result').textContent = out.join(',');
        ");

        Assert.Contains("proto:undefined", result);
        Assert.Contains("appendProto:undefined", result);
        Assert.Contains("newThrows:true", result);
    }

    [Fact(Timeout = 600000)]
    public void Dom_Operations_And_Accessors_Still_Work()
    {
        // The prototype is the only thing that goes away — calling and property access are unchanged.
        var result = ExecJs(@"
            var el = document.createElement('span');
            el.setAttribute('data-x', '1');
            el.id = 'abc';
            el.className = 'c1';
            el.classList.add('c2');
            el.style.color = 'red';
            el.appendChild(document.createTextNode('hi'));
            document.getElementById('result').textContent = [
                el.getAttribute('data-x'), el.id, el.className,
                el.style.color, el.textContent, el.childNodes.length
            ].join('|');
        ");

        Assert.Contains("1|abc|c1 c2|red|hi|1", result);
    }

    [Fact(Timeout = 600000)]
    public void Interface_Objects_Remain_Constructable()
    {
        // `createPrototype: false` also makes a function non-constructable
        // (JSConstructorOperations.IsConstructor tests `prototype != null || IsConstructable`), so
        // the interface objects JS may legitimately `new` must keep the default JSFunction.
        var result = ExecJs(@"
            var out = [];
            out.push('url:' + new URL('https://a.example/b').pathname);
            out.push('urlProto:' + typeof URL.prototype);
            out.push('headers:' + (new Headers() instanceof Headers));
            out.push('formData:' + typeof new FormData());
            document.getElementById('result').textContent = out.join(',');
        ");

        Assert.Contains("url:/b", result);
        Assert.Contains("urlProto:object", result);
        Assert.Contains("headers:true", result);
        Assert.Contains("formData:object", result);
    }

    /// <summary>
    /// Source guard: wrapper members must be minted through <c>DomFunction</c>. Only the files that
    /// register interface objects (which JS constructs with <c>new</c>) may use <c>JSFunction</c>
    /// directly. Without this, a new binding added the obvious way silently reintroduces a
    /// per-member prototype allocation on every node.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Bridge_Mints_Wrapper_Members_Through_DomFunction()
    {
        // These register interface objects — URL, Response, Request, Headers, FormData,
        // MessageChannel, CSSStyleSheet, Notification, MediaSource — which must stay constructable.
        var interfaceObjectFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Document.cs",
            "Registration.cs",
            "FetchBinding.cs",
            "NotificationBinding.cs",
            "MediaCapabilityBinding.cs",
        };

        var bridgeRoot = Path.Combine(FindRepositoryRoot(), "src", "Broiler.HtmlBridge.Dom");
        Assert.True(Directory.Exists(bridgeRoot), $"Bridge source not found at {bridgeRoot}");

        var offenders = Directory
            .EnumerateFiles(bridgeRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !interfaceObjectFiles.Contains(Path.GetFileName(path)))
            .Where(path => File.ReadAllText(path).Contains("new JSFunction(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(bridgeRoot, path))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "These bridge files mint wrapper members with `new JSFunction(`, which allocates a " +
            "throwaway prototype object per member per node. Use `new DomFunction(` instead — or, " +
            "if the file genuinely registers a constructable interface object, add it to " +
            $"interfaceObjectFiles above:{Environment.NewLine}  " +
            string.Join($"{Environment.NewLine}  ", offenders));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".gitmodules")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root containing .gitmodules and Directory.Build.props.");
    }
}
