namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>document</c> surface that names the document's own contents: its eight live collections,
/// and the <c>doctype</c>/<c>dir</c>/<c>designMode</c> accessors.
/// </summary>
/// <remarks>
/// <para>
/// A document-wide probe of ~30 properties against Chromium found one coherent cluster left over
/// once the individually-fixed items were removed. <c>anchors</c>, <c>embeds</c>, <c>plugins</c>,
/// <c>doctype</c>, <c>dir</c> and <c>designMode</c> were absent outright — each read
/// <c>undefined</c>, so the ordinary <c>document.embeds.length</c> was a <c>TypeError</c>. The four
/// collections that did exist (<c>forms</c>, <c>images</c>, <c>links</c>, <c>scripts</c>, plus
/// <c>styleSheets</c>) were plain arrays rebuilt per read, which is not live, carries
/// <c>map</c>/<c>filter</c> but not <c>item</c>/<c>namedItem</c>, and makes
/// <c>document.forms === document.forms</c> false.
/// </para>
/// <para>
/// <c>doctype</c> is the odd one: the node was <em>already there</em> — the parser appends a
/// canonical doctype as the document's first child, and <c>document.firstChild</c> returned it — but
/// the accessor DOM §4.5 names for it did not exist, and <c>document.childNodes</c> filtered to
/// elements, so the same node was reachable by position and invisible everywhere else.
/// </para>
/// <para>
/// Every expectation below was taken from Chromium through Playwright, running this fixture's own
/// probe script. <see cref="DocumentSurfaceTests"/> is the sibling fixture for the document's
/// scalar metadata (<c>charset</c>, <c>referrer</c>, <c>domain</c>, <c>lastModified</c>,
/// <c>activeElement</c>); this one covers what the document says about its own contents.
/// </para>
/// </remarks>
public class DocumentCollectionSurfaceTests
{
    private const string Page =
        "<!doctype html><html dir=\"rtl\"><head><title>T</title><style>p{color:red}</style></head><body>"
        + "<form name=\"f1\" id=\"fid\"></form><form></form>"
        + "<img name=\"i1\"><img id=\"i2\">"
        + "<a href=\"x\">l</a><a name=\"an1\">a</a><area href=\"y\">"
        + "<embed name=\"e1\">"
        + "<div id=\"result\"></div>";

    private static string Run(string script, string page = Page)
    {
        var html = page + "\n<script>\n" + script + "\n</script></body></html>";
        var serialized = CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
        const string start = "<div id=\"result\">";
        var open = serialized.IndexOf(start, StringComparison.Ordinal);
        Assert.True(open >= 0, $"probe did not run; document was:\n{serialized}");
        open += start.Length;
        var close = serialized.IndexOf("</div>", open, StringComparison.Ordinal);
        Assert.True(close > open, $"probe wrote nothing; document was:\n{serialized}");
        // The probe writes through textContent, so the serializer escapes what it wrote; `>` is the
        // one character these expectations need back.
        return serialized[open..close].Replace("&gt;", ">");
    }

    private const string Write = "document.getElementById('result').textContent = ";

    private const string Describe = """
        function describe(collection) {
          return Array.prototype.map.call(collection, function (e) {
            return e.tagName + (e.id ? '#' + e.id : '') + (e.getAttribute('name') ? '@' + e.getAttribute('name') : '');
          }).join(',');
        }
        """;

    /// <summary>
    /// Each collection holds the elements its definition names, in tree order — including the three
    /// that did not exist at all, and including the <c>links</c>/<c>anchors</c> split, which is not
    /// two names for one set: <c>links</c> is <c>a</c>/<c>area</c> with an <c>href</c> and
    /// <c>anchors</c> is <c>a</c> with a <c>name</c>, so the page's three anchors land in one, the
    /// other, and neither.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Each_Collection_Holds_What_Its_Definition_Names()
    {
        var result = Run(Describe + Write + """
            ['forms', 'images', 'links', 'anchors', 'embeds', 'plugins'].map(function (name) {
              return name + '=' + describe(document[name]);
            }).join('|');
            """);

        Assert.Equal(
            "forms=FORM#fid@f1,FORM|images=IMG@i1,IMG#i2|links=A,AREA|anchors=A@an1|embeds=EMBED@e1|plugins=EMBED@e1",
            result);
    }

    /// <summary>
    /// They are <c>HTMLCollection</c>s — the interface, through the prototype chain — and
    /// <c>styleSheets</c> is CSSOM's <c>StyleSheetList</c>, which is neither of the node collections.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Collections_Are_Their_Declared_Interfaces()
    {
        var result = Run(Write + """
            [
              document.forms.constructor.name,
              document.forms instanceof HTMLCollection,
              document.anchors instanceof HTMLCollection,
              document.styleSheets.constructor.name,
              document.styleSheets instanceof StyleSheetList,
              document.styleSheets.length
            ].join('|');
            """);

        Assert.Equal("HTMLCollection|true|true|StyleSheetList|true|1", result);
    }

    /// <summary>
    /// A document hands back the <em>same</em> collection object every time, and
    /// <c>plugins</c> is the same object as <c>embeds</c> rather than a second one over the same
    /// filter — which HTML §3.1.5 requires outright.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Collection_Is_One_Object_Per_Document()
    {
        var result = Run(Write + """
            [
              document.forms === document.forms,
              document.styleSheets === document.styleSheets,
              document.embeds === document.plugins,
              document.forms === document.images
            ].join('|');
            """);

        Assert.Equal("true|true|true|false", result);
    }

    /// <summary>
    /// The collections are live: a reference taken before a mutation sees the mutation. This is the
    /// failure the array shape produced silently — a wrong number rather than an error.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Held_Collection_Sees_Later_Mutations()
    {
        var result = Run(Write + """
            (function () {
              var forms = document.forms, images = document.images;
              var before = forms.length + '/' + images.length;
              document.body.appendChild(document.createElement('form'));
              document.body.removeChild(document.images[0]);
              return before + '->' + forms.length + '/' + images.length;
            })();
            """);

        Assert.Equal("2/2->3/1", result);
    }

    /// <summary>
    /// The named getter: by <c>id</c>, by <c>name</c>, through the <c>namedItem</c> spelling and the
    /// property spelling alike, with <c>null</c> for a name nothing carries.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Named_Access_Answers_By_Id_And_By_Name()
    {
        var result = Run(Write + """
            [
              document.forms.namedItem('f1') === document.forms[0],
              document.forms.fid === document.forms[0],
              document.forms.f1 === document.forms[0],
              document.images.i1 === document.images[0],
              document.images.i2 === document.images[1],
              document.anchors.an1 === document.anchors[0],
              String(document.links.namedItem('nope')),
              String(document.forms.namedItem(''))
            ].join('|');
            """);

        Assert.Equal("true|true|true|true|true|true|null|null", result);
    }

    /// <summary>
    /// When an <c>id</c> and a <c>name</c> compete for one key, the earlier element in tree order
    /// wins whichever attribute it carries — DOM §4.2.10.2 asks for the first element for which
    /// <em>at least one</em> of the two is true, not for all ids and then all names. Chromium agrees.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_First_Member_In_Tree_Order_Wins_A_Contested_Name()
    {
        var result = Run(
            Write + "document.forms.b === document.forms[0] ? 'first' : document.forms.b === document.forms[2] ? 'third' : 'neither';",
            "<!doctype html><html><body><form id=\"b\"></form><form name=\"a\" id=\"c\"></form><form name=\"b\"></form>"
            + "<div id=\"result\"></div>");

        Assert.Equal("first", result);
    }

    /// <summary>
    /// The Web IDL shape, in both directions: <c>item()</c> exists and answers <c>null</c> past the
    /// end, <c>forEach</c> does <em>not</em> exist on an <c>HTMLCollection</c> (DOM declares no
    /// <c>iterable&lt;&gt;</c> on it), and only the indices are own properties — <c>length</c> and the
    /// named entries stay out of <c>Object.keys</c>, as a browser's do.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Collections_Have_A_Web_Idl_Shape_Not_An_Array_Shape()
    {
        var result = Run(Write + """
            [
              document.forms.item(0) === document.forms[0],
              String(document.forms.item(9)),
              typeof document.forms.forEach,
              typeof document.forms.map,
              Object.keys(document.forms).join(','),
              Array.prototype.slice.call(document.styleSheets).length
            ].join('|');
            """);

        Assert.Equal("true|null|undefined|undefined|0,1|1", result);
    }

    /// <summary>
    /// <c>document.doctype</c> is the node the parser already put at the front of the document, with
    /// its name and its two identifiers, and it is <c>null</c> — not <c>undefined</c> — on a document
    /// that declares none.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Doctype_Names_The_Node_The_Parser_Produced()
    {
        var result = Run(Write + """
            [
              document.doctype.constructor.name,
              document.doctype.nodeType,
              document.doctype.name,
              document.doctype.publicId === '',
              document.doctype.systemId === '',
              document.doctype === document.firstChild
            ].join('|');
            """);

        Assert.Equal("DocumentType|10|html|true|true|true", result);

        Assert.Equal("null", Run(
            Write + "String(document.doctype);",
            "<html><body><div id=\"result\"></div>"));
    }

    /// <summary>
    /// <c>document.childNodes</c> carries the doctype, so it agrees with <c>firstChild</c> about what
    /// the document's first child is. It filtered to elements, which made the two disagree.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Document_ChildNodes_Carries_The_Doctype()
    {
        var result = Run(Write + """
            Array.prototype.map.call(document.childNodes, function (n) {
              return n.nodeType + ':' + n.nodeName;
            }).join(',') + '|' + (document.childNodes[0] === document.firstChild)
              + '|' + (document.childNodes instanceof NodeList);
            """);

        Assert.Equal("10:html,1:HTML|true|true", result);
    }

    /// <summary>
    /// <c>document.dir</c> reflects the document element's <c>dir</c> <em>limited to only known
    /// values</em>: the getter answers a canonical keyword or the empty string, while the setter
    /// writes what it was given straight through. So assigning <c>LTR</c> reads back as <c>ltr</c>
    /// over an attribute still spelled <c>LTR</c>, and assigning an unknown keyword reads back as
    /// empty over an attribute that holds it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Dir_Reflects_The_Document_Element_Limited_To_Known_Values()
    {
        var result = Run(Write + """
            (function () {
              var out = [document.dir];
              document.dir = 'LTR';
              out.push(document.dir, document.documentElement.getAttribute('dir'));
              document.dir = 'bogus';
              out.push(document.dir, document.documentElement.getAttribute('dir'));
              document.documentElement.removeAttribute('dir');
              out.push(document.dir === '' ? 'empty' : document.dir);
              return out.join('|');
            })();
            """);

        Assert.Equal("rtl|ltr|LTR||bogus|empty", result);
    }

    /// <summary>
    /// <c>document.designMode</c> is an enumerated state, so a value that is neither <c>on</c> nor
    /// <c>off</c> is ignored rather than stored — the previous value stays.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void DesignMode_Is_An_Enumerated_State_That_Ignores_Unknown_Values()
    {
        var result = Run(Write + """
            (function () {
              var out = [document.designMode];
              document.designMode = 'ON';
              out.push(document.designMode);
              document.designMode = 'zzz';
              out.push(document.designMode);
              document.designMode = 'off';
              out.push(document.designMode);
              return out.join('|');
            })();
            """);

        Assert.Equal("off|on|on|off", result);
    }
}
