using System.IO;

namespace Broiler.Wpt.Tests;

/// <summary>
/// WPT's <c>sub</c> pipe, as the runner performs it: a <c>*.sub.*</c> file is a template the
/// server expands before serving, so reading one straight off disk renders placeholder text and
/// URLs that address nothing.
/// </summary>
public class WptSubstitutionTests : IDisposable
{
    private readonly string _root =
        Directory.CreateTempSubdirectory("broiler-wpt-substitution").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // ── Which files are templates ────────────────────────────────────────────

    [Theory]
    [InlineData("a.sub.html", true)]
    [InlineData("a.sub.css", true)]
    [InlineData("a.sub.js", true)]
    [InlineData("deep/dir/a.sub.xhtml", true)]
    [InlineData("a.tentative.sub.html", true)]
    [InlineData("a.sub.tentative.html", true)]
    [InlineData("a.html", false)]
    [InlineData("a.subtle.html", false)]
    [InlineData("sub.html", false)]
    [InlineData("a.sub", false)]
    [InlineData("a.SUB.html", false)]
    public void IsSubstitutionFile_Follows_The_Naming_Rule(string path, bool expected) =>
        Assert.Equal(expected, WptSubstitution.IsSubstitutionFile(path));

    [Fact]
    public void IsSubstitutionFile_Tolerates_Empty_Input()
    {
        Assert.False(WptSubstitution.IsSubstitutionFile(null));
        Assert.False(WptSubstitution.IsSubstitutionFile(""));
    }

    // ── The fields a file on disk can answer ─────────────────────────────────

    [Fact]
    public void Host_And_Alternate_Host_Are_WPTs_Defaults()
    {
        Assert.Equal("web-platform.test", WptSubstitution.Apply("{{host}}"));
        Assert.Equal("not-web-platform.test", WptSubstitution.Apply("{{hosts[alt][]}}"));
        Assert.Equal("web-platform.test", WptSubstitution.Apply("{{hosts[][]}}"));
    }

    [Fact]
    public void Domains_Index_The_Primary_Hosts_Subdomains()
    {
        Assert.Equal("www.web-platform.test", WptSubstitution.Apply("{{domains[www]}}"));
        Assert.Equal("www2.web-platform.test", WptSubstitution.Apply("{{domains[www2]}}"));
        Assert.Equal("web-platform.test", WptSubstitution.Apply("{{domains[]}}"));
        Assert.Equal("www1.not-web-platform.test", WptSubstitution.Apply("{{hosts[alt][www1]}}"));
    }

    /// <summary>WPT closes its subdomain set under two-deep products (`_make_subdomains_product`).</summary>
    [Fact]
    public void A_Two_Deep_Subdomain_Product_Resolves()
    {
        Assert.Equal("www.www1.web-platform.test", WptSubstitution.Apply("{{domains[www.www1]}}"));
    }

    /// <summary>Non-ASCII labels are IDNA-encoded, exactly as <c>_get_domains</c> encodes them.</summary>
    [Fact]
    public void A_Non_Ascii_Subdomain_Is_Idna_Encoded()
    {
        Assert.Equal("xn--lve-6lad.web-platform.test", WptSubstitution.Apply("{{domains[élève]}}"));
    }

    [Fact]
    public void Ports_Are_Indexed_Per_Protocol()
    {
        Assert.Equal("8000", WptSubstitution.Apply("{{ports[http][0]}}"));
        Assert.Equal("8001", WptSubstitution.Apply("{{ports[http][1]}}"));
        Assert.Equal("8443", WptSubstitution.Apply("{{ports[https][0]}}"));
    }

    [Fact]
    public void An_Out_Of_Range_Port_Index_Is_Left_Alone() =>
        Assert.Equal("{{ports[http][9]}}", WptSubstitution.Apply("{{ports[http][9]}}"));

    [Fact]
    public void Location_Reports_The_Documents_Own_Url()
    {
        const string path = "/css/x/y.sub.html";
        Assert.Equal("http://web-platform.test:8000", WptSubstitution.Apply("{{location[server]}}", path));
        Assert.Equal("web-platform.test:8000", WptSubstitution.Apply("{{location[host]}}", path));
        Assert.Equal("web-platform.test", WptSubstitution.Apply("{{location[hostname]}}", path));
        Assert.Equal("http", WptSubstitution.Apply("{{location[scheme]}}", path));
        Assert.Equal("8000", WptSubstitution.Apply("{{location[port]}}", path));
        Assert.Equal(path, WptSubstitution.Apply("{{location[path]}}", path));
    }

    /// <summary>Without a known path the document has no URL to report, so the field is left alone.</summary>
    [Fact]
    public void Location_Without_A_Path_Is_Left_Alone() =>
        Assert.Equal("{{location[host]}}", WptSubstitution.Apply("{{location[host]}}"));

    // ── What is deliberately *not* substituted ───────────────────────────────

    /// <summary>
    /// A placeholder that describes a live request has no answer here. Leaving it verbatim renders
    /// the test exactly as it did before this existed; inventing a value would be a worse lie.
    /// </summary>
    [Theory]
    [InlineData("{{uuid()}}")]
    [InlineData("{{$id:uuid()}}")]
    [InlineData("{{$id}}")]
    [InlineData("{{headers[Host]}}")]
    [InlineData("{{GET[name]}}")]
    [InlineData("{{file_hash(md5, dom/interfaces.html)}}")]
    [InlineData("{{fs_path(css/x.html)}}")]
    [InlineData("{{header_or_default(X-Test, absent)}}")]
    public void A_Request_Scoped_Placeholder_Is_Left_Verbatim(string placeholder) =>
        Assert.Equal(placeholder, WptSubstitution.Apply(placeholder));

    /// <summary>
    /// <c>not_domains</c> names a host that is *meant* not to resolve. Substituting it would hand a
    /// load-failure test a working URL and turn it into a test of something else.
    /// </summary>
    [Fact]
    public void Not_Domains_Is_Left_Verbatim() =>
        Assert.Equal("{{not_domains[nonexistent]}}", WptSubstitution.Apply("{{not_domains[nonexistent]}}"));

    [Fact]
    public void An_Unknown_Field_Is_Left_Verbatim() =>
        Assert.Equal("{{nonsense[1]}}", WptSubstitution.Apply("{{nonsense[1]}}"));

    // ── Whole-document behaviour ─────────────────────────────────────────────

    [Fact]
    public void A_Frame_Url_Is_Expanded_In_Place()
    {
        const string markup =
            "<iframe src=\"http://{{hosts[alt][]}}:{{ports[http][0]}}/css/a/support/light-frame.html\"></iframe>";

        Assert.Equal(
            "<iframe src=\"http://not-web-platform.test:8000/css/a/support/light-frame.html\"></iframe>",
            WptSubstitution.Apply(markup));
    }

    [Fact]
    public void Content_Without_Placeholders_Is_Returned_Unchanged()
    {
        const string markup = "<!doctype html><p>nothing to expand</p>";
        Assert.Same(markup, WptSubstitution.Apply(markup));
    }

    /// <summary>Only a template is expanded; an ordinary document keeps any braces it contains.</summary>
    [Fact]
    public void ReadDocument_Expands_Only_A_Substitution_File()
    {
        var template = Path.Combine(_root, "a.sub.html");
        var plain = Path.Combine(_root, "a.html");
        File.WriteAllText(template, "<p>{{host}}</p>");
        File.WriteAllText(plain, "<p>{{host}}</p>");

        Assert.Equal("<p>web-platform.test</p>", WptSubstitution.ReadDocument(template, _root));
        Assert.Equal("<p>{{host}}</p>", WptSubstitution.ReadDocument(plain, _root));
    }

    [Fact]
    public void ReadDocument_Anchors_Location_At_The_Checkout()
    {
        var nested = Path.Combine(_root, "css", "x");
        Directory.CreateDirectory(nested);
        var template = Path.Combine(nested, "y.sub.html");
        File.WriteAllText(template, "<p>{{location[path]}}</p>");

        Assert.Equal("<p>/css/x/y.sub.html</p>", WptSubstitution.ReadDocument(template, _root));
    }

    // ── The document root a path is reported against ─────────────────────────

    [Fact]
    public void TryRootRelativePath_Is_Relative_To_The_Checkout()
    {
        var path = Path.Combine(_root, "css", "x.sub.html");
        Assert.Equal("/css/x.sub.html", WptSubstitution.TryRootRelativePath(path, _root));
    }

    /// <summary>
    /// The separator matters: a sibling directory whose name merely starts with the root's is
    /// outside it.
    /// </summary>
    [Fact]
    public void TryRootRelativePath_Rejects_A_Path_Outside_The_Checkout()
    {
        Assert.Null(WptSubstitution.TryRootRelativePath(_root + "-sibling/x.sub.html", _root));
        Assert.Null(WptSubstitution.TryRootRelativePath(Path.Combine(_root, "x.sub.html"), null));
    }

    // ── The hosts served from the checkout ───────────────────────────────────

    [Fact]
    public void A_Served_Origin_Is_Stripped_To_Its_Path()
    {
        Assert.Equal("/css/a.html",
            WptSubstitution.TryStripServedOrigin("http://web-platform.test:8000/css/a.html"));
        Assert.Equal("/css/a.html",
            WptSubstitution.TryStripServedOrigin("http://not-web-platform.test:8000/css/a.html"));
        Assert.Equal("/css/a.html",
            WptSubstitution.TryStripServedOrigin("https://www.web-platform.test:8443/css/a.html"));
    }

    [Fact]
    public void A_Served_Origin_Keeps_Its_Query()
    {
        Assert.Equal("/css/a.html?pipe=trickle(d1)",
            WptSubstitution.TryStripServedOrigin("http://web-platform.test:8000/css/a.html?pipe=trickle(d1)"));
    }

    /// <summary>
    /// WPT mints <c>nonexistent.*</c> hosts to be unreachable. Serving them from the checkout would
    /// make a load-failure test pass by loading something.
    /// </summary>
    [Theory]
    [InlineData("http://nonexistent.web-platform.test:8000/css/a.html")]
    [InlineData("http://example.com/css/a.html")]
    [InlineData("http://web-platform.test.evil.example/css/a.html")]
    [InlineData("ftp://web-platform.test:8000/css/a.html")]
    [InlineData("/css/a.html")]
    [InlineData("css/a.html")]
    [InlineData(null)]
    public void An_Unserved_Url_Is_Not_Stripped(string? url) =>
        Assert.Null(WptSubstitution.TryStripServedOrigin(url));

    /// <summary>
    /// Every host the substitution can produce is a host the runner serves — otherwise a URL it
    /// wrote itself would be one it then refuses to resolve.
    /// </summary>
    [Fact]
    public void Every_Substitutable_Host_Is_Served()
    {
        foreach (var placeholder in new[]
                 {
                     "{{host}}", "{{hosts[][]}}", "{{hosts[alt][]}}", "{{domains[www]}}",
                     "{{domains[www.www1]}}", "{{domains[élève]}}", "{{hosts[alt][www2]}}",
                 })
        {
            var host = WptSubstitution.Apply(placeholder);
            Assert.Equal("/x.html", WptSubstitution.TryStripServedOrigin($"http://{host}:8000/x.html"));
        }
    }

    /// <summary>
    /// The runner's own resource loaders take the same view: a substituted absolute URL names the
    /// same file as the root-relative one it was built from.
    /// </summary>
    [Fact]
    public void The_Resource_Resolver_Accepts_A_Served_Origin()
    {
        var dir = Path.Combine(_root, "images");
        Directory.CreateDirectory(dir);
        var onDisk = Path.Combine(dir, "blue.png");
        File.WriteAllBytes(onDisk, [0]);

        Assert.Equal(onDisk,
            WptTestRunner.TryResolveWptRootRelativePath("http://web-platform.test:8000/images/blue.png", _root));
        Assert.Equal(onDisk,
            WptTestRunner.TryResolveWptRootRelativePath("/images/blue.png", _root));
        Assert.Null(
            WptTestRunner.TryResolveWptRootRelativePath("http://example.com/images/blue.png", _root));
    }
}
