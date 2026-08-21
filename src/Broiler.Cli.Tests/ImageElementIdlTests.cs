namespace Broiler.Cli.Tests;

/// <summary>
/// HTMLImageElement's IDL attributes (HTML §4.8.3). The interface used to carry nothing but
/// <c>width</c>/<c>height</c>: <c>src</c>, <c>currentSrc</c>, <c>alt</c>, <c>srcset</c>, <c>sizes</c>,
/// <c>useMap</c>, <c>isMap</c> and the fetch hints were all absent, so reading one gave
/// <c>undefined</c> — not the empty string a missing content attribute reflects as — and writing one
/// set a plain JS property on the wrapper that no content attribute, and so no layout or
/// serialization, ever saw. www.mediawiki.org's start page died on the read half; see
/// <see cref="Reading_Src_Off_A_Parsed_Image_Survives_The_MediaWiki_Thumbnail_Walk"/>.
/// </summary>
public sealed class ImageElementIdlTests
{
    // The reported throw, reduced to the code that produced it. mmv.bootstrap walks the page's
    // thumbnails and, for each, calls mw.Title.newFromImg( $thumb ) — `img[0].src` — and hands the
    // result to mw.util.parseImageUrl, which opens with `url.match( … )`. src was undefined, so the
    // property read on it threw "Cannot get property match of undefined (evaluating 'url.match')",
    // and because mediawiki.org serves its modules as one load.php bundle that throw took
    // processThumbs and everything queued behind it down with it. The two functions below are the
    // real ones, transcribed; what matters is that they now run to a Title instead of throwing.
    [Fact]
    public void Reading_Src_Off_A_Parsed_Image_Survives_The_MediaWiki_Thumbnail_Walk()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<a class=""mw-file-description"" href=""/wiki/File:Example.png""><img class=""mw-file-element""
   src=""//upload.wikimedia.org/wikipedia/commons/thumb/a/a9/Example.png/120px-Example.png""
   srcset=""//upload.wikimedia.org/wikipedia/commons/thumb/a/a9/Example.png/240px-Example.png 2x""
   alt=""Example"" width=""120"" height=""90""></a>
<div id=""result""></div>
<script>
// mw.util.parseImageUrl, reduced to the branch the page takes.
function parseImageUrl( url ) {
    var regexes = [
        /\/[\da-f]\/[\da-f]{2}\/([^\s\/]+)\/(?:[^\s\/]+-)?(\d+)px-(?:\1|thumbnail)(?:\.[^\s\/?]+)?$/,
        /\/[\da-f]\/[\da-f]{2}\/([^\s\/?]+)$/
    ];
    for ( var i = 0; i < regexes.length; i++ ) {
        var match = url.match( regexes[ i ] );
        if ( match ) {
            return { name: match[ 1 ], width: match[ 2 ] || null };
        }
    }
    return null;
}
// mw.Title.newFromImg, and mmv.bootstrap's old-version guard right behind it.
function newFromImg( img ) {
    var src = img.jquery ? img[ 0 ].src : img.src;
    var data = parseImageUrl( src );
    return data ? 'File:' + data.name : null;
}
function isOldFileVersionThumb( src ) {
    return src.includes( '/archive/' );
}

var img = document.querySelector( 'img.mw-file-element' );
var parts = [];
try {
    parts.push( 'title=' + newFromImg( img ) );
    parts.push( 'old=' + isOldFileVersionThumb( img.currentSrc || img.src ) );
} catch ( e ) {
    parts.push( 'threw=' + e.message );
}
document.getElementById( 'result' ).textContent = parts.join( '|' );
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "https://www.mediawiki.org/wiki/MediaWiki");

        Assert.Contains(">title=File:Example.png|old=false<", result);
    }

    [Fact]
    public void Src_Getter_Resolves_Against_The_Page_Url_And_The_Setter_Writes_The_Content_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<img id=""parsed"" src=""thumb/pic.png"">
<div id=""result""></div>
<script>
var parsed = document.getElementById( 'parsed' );
var made = document.createElement( 'img' );
made.src = 'other/deep.png';
document.body.appendChild( made );
document.getElementById( 'result' ).textContent =
    'parsed=' + parsed.src +
    '|made=' + made.src +
    '|attr=' + made.getAttribute( 'src' );
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "https://example.org/dir/page.html");

        // The IDL getter is an absolute URL either way; the content attribute keeps what was written.
        Assert.Contains(
            ">parsed=https://example.org/dir/thumb/pic.png" +
            "|made=https://example.org/dir/other/deep.png" +
            "|attr=other/deep.png<",
            result);
    }

    // A missing content attribute reflects as the empty string, and currentSrc reports the URL the
    // element settled on — nothing at all when there is no src to settle on. `img.currentSrc ||
    // img.src` (mmv.bootstrap's idiom) has to reach the second operand rather than a page URL that
    // resolving an absent src would otherwise manufacture.
    [Fact]
    public void An_Image_Without_A_Src_Reports_Empty_Strings_Not_Undefined()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<img id=""bare"">
<div id=""result""></div>
<script>
var bare = document.getElementById( 'bare' );
document.getElementById( 'result' ).textContent =
    'src=' + JSON.stringify( bare.src ) +
    '|currentSrc=' + JSON.stringify( bare.currentSrc ) +
    '|alt=' + JSON.stringify( bare.alt ) +
    '|srcset=' + JSON.stringify( bare.srcset ) +
    '|isMap=' + bare.isMap;
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "https://example.org/dir/page.html");

        // The JSON.stringify quotes come back escaped, because textContent serializes as markup.
        const string emptyString = "&quot;&quot;";
        Assert.Contains(
            $">src={emptyString}|currentSrc={emptyString}|alt={emptyString}|srcset={emptyString}|isMap=false<",
            result);
    }

    [Fact]
    public void The_Plain_String_Idl_Attributes_Round_Trip_Through_The_Content_Attributes()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var img = document.createElement( 'img' );
img.alt = 'Example';
img.srcset = 'wide.png 2x';
img.sizes = '(max-width: 600px) 100vw';
img.useMap = '#map';
img.crossOrigin = 'anonymous';
img.referrerPolicy = 'no-referrer';
img.decoding = 'async';
img.loading = 'lazy';
img.fetchPriority = 'low';
img.isMap = true;
document.body.appendChild( img );
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "https://example.org/dir/page.html");

        foreach (var attribute in new[]
                 {
                     @"alt=""Example""", @"srcset=""wide.png 2x""",
                     @"sizes=""(max-width: 600px) 100vw""", @"usemap=""#map""",
                     @"crossorigin=""anonymous""", @"referrerpolicy=""no-referrer""",
                     @"decoding=""async""", @"loading=""lazy""", @"fetchpriority=""low""",
                     "ismap",
                 })
        {
            Assert.Contains(attribute, result);
        }
    }

    // isMap is boolean-reflected: present or absent, never the string "false". Setting it back to
    // false has to remove the attribute, or the getter reads the empty-but-present value as true.
    [Fact]
    public void IsMap_Set_Back_To_False_Removes_The_Content_Attribute()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<img id=""m"" src=""pic.png"" ismap>
<div id=""result""></div>
<script>
var img = document.getElementById( 'm' );
var before = img.isMap;
img.isMap = false;
document.getElementById( 'result' ).textContent =
    'before=' + before + '|after=' + img.isMap + '|has=' + img.hasAttribute( 'ismap' );
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "https://example.org/dir/page.html");

        Assert.Contains(">before=true|after=false|has=false<", result);
    }
}
