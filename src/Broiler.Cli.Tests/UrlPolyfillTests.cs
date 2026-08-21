using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>URL</c> / <c>URLSearchParams</c> polyfill (Polyfills/content-rendering-polyfills.js),
/// against the WHATWG URL Standard.
/// </summary>
/// <remarks>
/// The reported failure was mediawiki.org's start page: CentralNotice's <c>setInitialData</c>
/// begins with <c>( new URL( location ) ).searchParams.forEach( … )</c>, and the polyfill's
/// constructor used the argument as a string without coercing it — <c>location.match</c> is
/// undefined, so the call threw "undefined is not a function" out of the constructor, took
/// <c>reallyChooseAndMaybeDisplay</c> with it, and killed the startup module chain behind it.
/// The same page also builds banner URLs with <c>new URL( '//meta.wikimedia.org/…', location )</c>,
/// which the old parser resolved to <c>https://www.mediawiki.org//meta.wikimedia.org/…</c>.
/// </remarks>
public sealed class UrlPolyfillTests
{
    private const string PageUrl = "https://www.mediawiki.org/wiki/MediaWiki?uselang=de&banner=x#s";

    /// <summary>
    /// Runs <paramref name="jsCode"/> in a DOM-attached context at <paramref name="documentUrl"/>
    /// and returns the rendered text. The script writes what it learned into <c>#result</c>.
    /// </summary>
    private static string ExecJs(string jsCode, string documentUrl = PageUrl)
    {
        var html = $$"""
            <!doctype html>
            <html><head><title>Test</title></head>
            <body>
            <div id="result"></div>
            <script>
            function show(text) { document.getElementById('result').textContent = text; }
            try {
            {{jsCode}}
            } catch (e) { show('THREW:' + e.name + ':' + e.message); }
            </script>
            </body>
            </html>
            """;
        // The capture hands back serialized HTML, so the text the script wrote comes back with its
        // markup-significant characters escaped. URLs are full of ampersands; undo that here so the
        // expectations below can be written the way a URL is written.
        return CaptureService.ExecuteScriptsWithDom(html, documentUrl)
            .Replace("&amp;", "&")
            .Replace("&quot;", "\"");
    }

    // ---------------------------------------------------------------
    //  The reported call: a Location where a USVString is declared
    // ---------------------------------------------------------------

    [Fact]
    public void NewUrl_AcceptsLocation_AndExposesItsQuery()
    {
        var result = ExecJs("""
            var out = [];
            ( new URL( location ) ).searchParams.forEach( function ( value, key ) {
                out.push( key + '=' + value );
            } );
            show( 'PARAMS:' + out.join( ',' ) );
            """);

        Assert.Contains("PARAMS:uselang=de,banner=x", result);
    }

    [Fact]
    public void NewUrl_StringifiesLocation_IntoItsComponents()
    {
        var result = ExecJs("""
            var u = new URL( location );
            show( 'HREF:' + u.href + '|HOST:' + u.hostname + '|PATH:' + u.pathname +
                  '|SEARCH:' + u.search + '|HASH:' + u.hash + '|ORIGIN:' + u.origin );
            """);

        Assert.Contains(
            "HREF:https://www.mediawiki.org/wiki/MediaWiki?uselang=de&banner=x#s" +
            "|HOST:www.mediawiki.org|PATH:/wiki/MediaWiki|SEARCH:?uselang=de&banner=x|HASH:#s" +
            "|ORIGIN:https://www.mediawiki.org",
            result);
    }

    [Fact]
    public void NewUrl_AcceptsALocationAsTheBase()
    {
        // CentralNotice's dispatcher URL: `new URL( dispatcherUrl, location )`.
        var result = ExecJs("""
            show( 'HREF:' + new URL( '/w/index.php?title=Special:BannerLoader', location ).href );
            """);

        Assert.Contains("HREF:https://www.mediawiki.org/w/index.php?title=Special:BannerLoader", result);
    }

    [Fact]
    public void NewUrl_AcceptsAnotherUrlAsEitherArgument()
    {
        var result = ExecJs("""
            var base = new URL( 'https://a.example/x/y/z?q=1' );
            show( 'COPY:' + new URL( base ).href + '|REL:' + new URL( '../w', base ).href );
            """);

        Assert.Contains("COPY:https://a.example/x/y/z?q=1|REL:https://a.example/x/w", result);
    }

    // ---------------------------------------------------------------
    //  Relative resolution
    // ---------------------------------------------------------------

    [Fact]
    public void SchemeRelativeReference_KeepsOnlyTheSchemeOfTheBase()
    {
        // The old parser prefixed the base's origin and produced
        // "https://www.mediawiki.org//meta.wikimedia.org/w/index.php".
        var result = ExecJs("""
            show( 'HREF:' + new URL( '//meta.wikimedia.org/w/index.php?title=Special:RecordImpression', location ).href );
            """);

        Assert.Contains(
            "HREF:https://meta.wikimedia.org/w/index.php?title=Special:RecordImpression",
            result);
    }

    [Theory]
    // reference, expected href — resolved against https://a.example/x/y/z?q=1#f
    [InlineData("/p/q", "https://a.example/p/q")]
    [InlineData("p/q", "https://a.example/x/y/p/q")]
    [InlineData("./p", "https://a.example/x/y/p")]
    [InlineData("../p", "https://a.example/x/p")]
    [InlineData("../../../p", "https://a.example/p")]
    [InlineData("?only=this", "https://a.example/x/y/z?only=this")]
    [InlineData("#top", "https://a.example/x/y/z?q=1#top")]
    [InlineData("", "https://a.example/x/y/z?q=1")]
    [InlineData("//b.example/p", "https://b.example/p")]
    [InlineData("https://b.example:443/p", "https://b.example/p")]
    [InlineData("https://b.example:8443/p", "https://b.example:8443/p")]
    public void RelativeReference_ResolvesAgainstItsBase(string reference, string expected)
    {
        var result = ExecJs($$"""
            show( 'HREF:' + new URL( {{Quote(reference)}}, 'https://a.example/x/y/z?q=1#f' ).href );
            """);

        Assert.Contains("HREF:" + expected, result);
    }

    [Fact]
    public void AReferenceWithNoBase_ThrowsTypeErrorAsTheConstructorDoes()
    {
        var result = ExecJs("show( 'HREF:' + new URL( '/w/index.php' ).href );");

        Assert.Contains("THREW:TypeError", result);
    }

    // ---------------------------------------------------------------
    //  Opaque paths and non-special schemes
    // ---------------------------------------------------------------

    [Fact]
    public void AnOpaquePath_KeepsItsBodyAndReportsANullOrigin()
    {
        var result = ExecJs("""
            var u = new URL( 'data:text/javascript,alert(1)' );
            var m = new URL( 'mailto:x@y.example' );
            show( 'PROTO:' + u.protocol + '|PATH:' + u.pathname + '|HOST:' + u.hostname +
                  '|ORIGIN:' + u.origin + '|MAILTO:' + m.href );
            """);

        Assert.Contains(
            "PROTO:data:|PATH:text/javascript,alert(1)|HOST:|ORIGIN:null|MAILTO:mailto:x@y.example",
            result);
    }

    [Fact]
    public void AFileUrl_HasNoAuthorityUnlessTwoSlashesGiveItOne()
    {
        var result = ExecJs("""
            show( 'LOCAL:' + new URL( 'file:///test.html' ).pathname +
                  '|HOST:' + new URL( 'file://server/share/x' ).hostname +
                  '|ORIGIN:' + new URL( 'file:///test.html' ).origin );
            """, "file:///test.html");

        Assert.Contains("LOCAL:/test.html|HOST:server|ORIGIN:null", result);
    }

    // ---------------------------------------------------------------
    //  Normalization
    // ---------------------------------------------------------------

    [Fact]
    public void ParsingNormalizesCase_DefaultPorts_DotSegmentsAndSpaces()
    {
        var result = ExecJs("""
            show( 'A:' + new URL( 'HTTPS://EXAMPLE.COM/Path' ).href +
                  '|B:' + new URL( 'https://a.example:443/p/../q' ).href +
                  '|C:' + new URL( '  https://a.example/a b  ' ).href +
                  '|D:' + new URL( 'http://user:pw@a.example:8080/p' ).href );
            """);

        Assert.Contains(
            "A:https://example.com/Path|B:https://a.example/q|C:https://a.example/a%20b" +
            "|D:http://user:pw@a.example:8080/p",
            result);
    }

    // ---------------------------------------------------------------
    //  A URL is mutable, and its searchParams is the same object each read
    // ---------------------------------------------------------------

    [Fact]
    public void WritingThroughSearchParams_RewritesTheUrl()
    {
        var result = ExecJs("""
            var u = new URL( 'https://a.example/p?a=1' );
            var sp = u.searchParams;
            sp.set( 'b', '2 3' );
            var withB = u.toString();
            sp.delete( 'a' );
            sp.delete( 'b' );
            show( 'WITH:' + withB + '|EMPTY:' + u.href + '|SAME:' + ( u.searchParams === sp ) );
            """);

        Assert.Contains("WITH:https://a.example/p?a=1&b=2+3|EMPTY:https://a.example/p|SAME:true", result);
    }

    [Fact]
    public void SettingAComponent_IsVisibleInHref()
    {
        var result = ExecJs("""
            var u = new URL( 'https://a.example/p?a=1#f' );
            u.hash = 'top';
            u.pathname = 'q/r';
            u.search = '?b=2';
            u.port = '8443';
            show( 'HREF:' + u.href + '|PARAM:' + u.searchParams.get( 'b' ) );
            """);

        Assert.Contains("HREF:https://a.example:8443/q/r?b=2#top|PARAM:2", result);
    }

    // ---------------------------------------------------------------
    //  URLSearchParams
    // ---------------------------------------------------------------

    [Fact]
    public void SearchParams_DecodesFormEncodingAndReEncodesIt()
    {
        var result = ExecJs("""
            var p = new URLSearchParams( '?q=a+b%20c&lang=en&q=2' );
            var round = new URLSearchParams();
            round.append( 'a', 'b c' );
            round.append( 'd', 'e&f' );
            show( 'Q:' + p.get( 'q' ) + '|ALL:' + p.getAll( 'q' ).join( ';' ) +
                  '|SIZE:' + p.size + '|ROUND:' + round.toString() );
            """);

        Assert.Contains("Q:a b c|ALL:a b c;2|SIZE:3|ROUND:a=b+c&d=e%26f", result);
    }

    [Fact]
    public void SearchParams_TakesTheInitializersTheStandardLists()
    {
        var result = ExecJs("""
            var fromPairs = new URLSearchParams( [ [ 'a', '1' ], [ 'b', '2' ] ] );
            show( 'PAIRS:' + fromPairs.toString() +
                  '|OBJECT:' + new URLSearchParams( { a: '1', b: '2' } ).toString() +
                  '|COPY:' + new URLSearchParams( fromPairs ).toString() +
                  '|EMPTY:' + new URLSearchParams().toString() );
            """);

        Assert.Contains("PAIRS:a=1&b=2|OBJECT:a=1&b=2|COPY:a=1&b=2|EMPTY:", result);
    }

    [Fact]
    public void SearchParams_SortsStablyAndIterates()
    {
        var result = ExecJs("""
            var p = new URLSearchParams( 'c=3&a=1&b=2&a=0' );
            p.sort();
            var pairs = [];
            for ( var e of p ) { pairs.push( e[ 0 ] + ':' + e[ 1 ] ); }
            show( 'SORTED:' + p.toString() + '|KEYS:' + Array.from( p.keys() ).join( ',' ) +
                  '|ITER:' + pairs.join( ',' ) );
            """);

        Assert.Contains("SORTED:a=1&a=0&b=2&c=3|KEYS:a,a,b,c|ITER:a:1,a:0,b:2,c:3", result);
    }

    [Fact]
    public void SearchParams_KeepsAMalformedEscapeVerbatim()
    {
        // decodeURIComponent rejects a stray '%'; a page's query string still has to survive it.
        var result = ExecJs("""
            show( 'A:' + new URLSearchParams( 'a=100%&b=2' ).get( 'a' ) +
                  '|B:' + new URLSearchParams( 'a&b=2' ).get( 'a' ) );
            """);

        Assert.Contains("A:100%|B:", result);
    }

    // ---------------------------------------------------------------
    //  The non-throwing spellings a page feature-detects
    // ---------------------------------------------------------------

    [Fact]
    public void UrlParse_And_UrlCanParse_AnswerWithoutThrowing()
    {
        var result = ExecJs("""
            show( 'CAN:' + URL.canParse( 'https://a.example/' ) + ',' + URL.canParse( '/x' ) +
                  '|PARSE:' + URL.parse( '/x' ) +
                  '|JSON:' + JSON.stringify( new URL( 'https://a.example/p' ) ) );
            """);

        Assert.Contains("CAN:true,false|PARSE:null|JSON:\"https://a.example/p\"", result);
    }

    private static string Quote(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
}
