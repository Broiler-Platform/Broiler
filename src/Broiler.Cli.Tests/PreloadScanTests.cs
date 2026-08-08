using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// Covers the speculative preload scan — multithreading roadmap item #17: what a document's raw
/// source says it will fetch, discovered on a worker before the parse that would otherwise have
/// discovered it.
/// </summary>
/// <remarks>
/// Two claims are being tested and they are separable. <see cref="PreloadScanner"/> is a pure
/// function of the source, so the bulk of this file pins down <em>what it finds and what it
/// refuses to find</em> — the refusals matter as much, because a speculated request the document
/// never makes is a round trip charged to an origin for nothing.
/// <see cref="SpeculativePreloadScan"/> adds the worker, the policy filter and the sink, and the
/// exit gate for it is that turning it off changes nothing but timing.
/// </remarks>
public sealed class PreloadScanTests
{
    private const string PageUrl = "https://example.test/dir/page.html";

    private static PreloadScanResult Scan(string html) => PreloadScanner.Scan(html, PageUrl);

    // ────────────── what it finds ──────────────

    [Fact]
    public void Finds_Each_Family_In_Document_Order()
    {
        var result = Scan("""
            <link rel="stylesheet" href="a.css">
            <script src="b.js"></script>
            <img src="c.png">
            <iframe src="d.html"></iframe>
            """);

        Assert.Equal(
            [
                (PreloadKind.StyleSheet, "a.css"),
                (PreloadKind.Script, "b.js"),
                (PreloadKind.Image, "c.png"),
                (PreloadKind.Frame, "d.html"),
            ],
            result.Candidates.Select(c => (c.Kind, c.RawUrl)));
    }

    [Fact]
    public void Resolves_Every_Candidate_Against_The_Page_Url()
    {
        var result = Scan("""<link rel="stylesheet" href="a.css">""");

        Assert.Equal(["https://example.test/dir/a.css"], result.ResolvedUrls(PreloadKind.StyleSheet));
        Assert.Equal(["a.css"], result.RawUrls(PreloadKind.StyleSheet));
    }

    /// <summary>
    /// HTML §4.2.3 makes the first <c>&lt;base href&gt;</c> the base for the whole document, not
    /// just for what follows it — which is why the scan resolves after its pass rather than during
    /// it. A scanner that resolved as it went would get this one wrong.
    /// </summary>
    [Fact]
    public void A_Base_Href_Declared_After_A_Resource_Still_Applies_To_It()
    {
        var result = Scan("""
            <link rel="stylesheet" href="a.css">
            <base href="/assets/">
            """);

        Assert.Equal("https://example.test/assets/", result.DocumentBaseUrl);
        Assert.Equal(["https://example.test/assets/a.css"], result.ResolvedUrls(PreloadKind.StyleSheet));
    }

    [Fact]
    public void The_First_Base_Href_Wins()
    {
        var result = Scan("""
            <base href="/first/">
            <base href="/second/">
            <img src="a.png">
            """);

        Assert.Equal(["https://example.test/first/a.png"], result.ResolvedUrls(PreloadKind.Image));
    }

    [Fact]
    public void The_Same_Url_Named_Twice_Is_One_Candidate()
    {
        var result = Scan("""
            <script src="a.js"></script>
            <script src="a.js"></script>
            """);

        Assert.Single(result.Candidates);
    }

    /// <summary>
    /// Deduplication is per family: the same file linked as a stylesheet and preloaded as an image
    /// is two different requests to two different consume sites.
    /// </summary>
    [Fact]
    public void The_Same_Url_In_Two_Families_Is_Two_Candidates()
    {
        var result = Scan("""
            <link rel="stylesheet" href="a.css">
            <link rel="preload" as="image" href="a.css">
            """);

        Assert.Equal(2, result.Candidates.Count);
    }

    [Theory]
    [InlineData("""<link rel="stylesheet" href="a.css">""", PreloadKind.StyleSheet)]
    [InlineData("""<link rel="alternate stylesheet" href="a.css">""", PreloadKind.StyleSheet)]
    [InlineData("""<link rel="STYLESHEET" href="a.css">""", PreloadKind.StyleSheet)]
    [InlineData("""<link rel="modulepreload" href="a.js">""", PreloadKind.Script)]
    [InlineData("""<link rel="preload" as="script" href="a.js">""", PreloadKind.Script)]
    [InlineData("""<link rel="preload" as="style" href="a.css">""", PreloadKind.StyleSheet)]
    [InlineData("""<link rel="preload" as="image" href="a.png">""", PreloadKind.Image)]
    public void Link_Rel_Is_Read_As_A_Token_List(string html, PreloadKind expected)
    {
        var candidate = Assert.Single(Scan(html).Candidates);
        Assert.Equal(expected, candidate.Kind);
    }

    [Theory]
    [InlineData("""<link rel="icon" href="favicon.ico">""")]
    [InlineData("""<link rel="canonical" href="/here">""")]
    [InlineData("""<link href="a.css">""")]
    [InlineData("""<link rel="preload" href="a.woff2">""")]
    [InlineData("""<link rel="preload" as="font" href="a.woff2">""")]
    public void A_Link_This_Engine_Has_No_Consume_Site_For_Is_Not_Speculated_On(string html) =>
        Assert.Empty(Scan(html).Candidates);

    [Fact]
    public void Input_Is_An_Image_Only_When_Its_Type_Says_So()
    {
        Assert.Single(Scan("""<input type="image" src="submit.png">""").Candidates);
        Assert.Empty(Scan("""<input type="text" src="submit.png">""").Candidates);
        Assert.Empty(Scan("""<input src="submit.png">""").Candidates);
    }

    [Fact]
    public void Object_Data_And_Embed_Src_Are_Frame_Resources()
    {
        Assert.Equal(PreloadKind.Frame, Assert.Single(Scan("""<object data="a.svg"></object>""").Candidates).Kind);
        Assert.Equal(PreloadKind.Frame, Assert.Single(Scan("""<embed src="a.svg">""").Candidates).Kind);
    }

    // ────────────── what it refuses to find ──────────────

    /// <summary>
    /// The reason the scan tokenizes instead of matching a pattern over the buffer: none of these
    /// three is a resource, and all three look exactly like one to a regex.
    /// </summary>
    [Theory]
    [InlineData("""<!-- <img src="a.png"> -->""")]
    [InlineData("""<script>var s = '<img src="a.png">';</script>""")]
    [InlineData("""<style>/* <img src="a.png"> */</style>""")]
    public void Text_That_Merely_Looks_Like_A_Tag_Is_Not_A_Resource(string html) =>
        Assert.Empty(Scan(html).Candidates);

    [Fact]
    public void A_Quoted_Attribute_Containing_A_Close_Bracket_Does_Not_Truncate_The_Tag()
    {
        var candidate = Assert.Single(Scan("""<img alt="a > b" src="c.png">""").Candidates);
        Assert.Equal("c.png", candidate.RawUrl);
    }

    /// <summary>
    /// Template contents are inert until something clones them, which is a script decision this
    /// scan cannot predict — so a template's resources are never fetched by the parse it overlaps.
    /// </summary>
    [Fact]
    public void Template_Contents_Are_Inert()
    {
        Assert.Empty(Scan("""<template><img src="a.png"></template>""").Candidates);
        Assert.Empty(Scan("""<template><template><img src="a.png"></template></template>""").Candidates);
    }

    [Fact]
    public void A_Nested_Templates_End_Tag_Does_Not_Re_Open_The_Outer_One()
    {
        // The inner </template> closes only the inner one; `b.png` is still inert. A boolean
        // "inside a template" flag rather than a depth counter gets exactly this case wrong.
        Assert.Empty(Scan("""
            <template><template><img src="a.png"></template><img src="b.png"></template>
            """).Candidates);
    }

    [Fact]
    public void Resources_After_A_Template_Are_Found()
    {
        var candidate = Assert.Single(Scan("""<template><img src="a.png"></template><img src="b.png">""").Candidates);
        Assert.Equal("b.png", candidate.RawUrl);
    }

    [Fact]
    public void Noscript_Contents_Are_Inert_Because_This_Engine_Runs_Scripts() =>
        Assert.Empty(Scan("""<noscript><img src="a.png"></noscript>""").Candidates);

    [Theory]
    [InlineData("""<img src="data:image/png;base64,iVBORw0KGgo=">""")]
    [InlineData("""<iframe src="about:blank"></iframe>""")]
    [InlineData("""<iframe src="javascript:0"></iframe>""")]
    [InlineData("""<img src="blob:https://example.test/abc">""")]
    [InlineData("""<img src="#fragment">""")]
    [InlineData("""<img src="">""")]
    [InlineData("<img>")]
    public void A_Url_With_No_Round_Trip_To_Save_Is_Not_A_Candidate(string html) =>
        Assert.Empty(Scan(html).Candidates);

    [Fact]
    public void An_Iframe_With_Srcdoc_Carries_Its_Own_Content() =>
        Assert.Empty(Scan("""<iframe srcdoc="<p>hi" src="a.html"></iframe>""").Candidates);

    /// <summary>
    /// Which <c>srcset</c> candidate a browser fetches depends on the viewport and the device pixel
    /// ratio, and this scan runs before layout. Preloading all of them would put four requests on
    /// the wire for one image, so the documented boundary is that <c>srcset</c> is not scanned at
    /// all — and a bare <c>srcset</c> with no <c>src</c> therefore yields nothing.
    /// </summary>
    [Fact]
    public void Srcset_Is_Not_Scanned()
    {
        Assert.Empty(Scan("""<img srcset="a.png 1x, b.png 2x">""").Candidates);

        var candidate = Assert.Single(Scan("""<img src="a.png" srcset="b.png 2x">""").Candidates);
        Assert.Equal("a.png", candidate.RawUrl);
    }

    [Fact]
    public void A_Relative_Url_With_No_Base_Resolves_To_Nothing_Rather_Than_To_Itself()
    {
        var result = PreloadScanner.Scan("""<link rel="stylesheet" href="a.css">""", pageUrl: null);

        Assert.Equal(["a.css"], result.RawUrls(PreloadKind.StyleSheet));
        Assert.Empty(result.ResolvedUrls(PreloadKind.StyleSheet));
    }

    // ────────────── the cheap pre-check ──────────────

    [Fact]
    public void A_Document_That_Names_No_Resource_Does_Not_Get_A_Tokenizer_Pass()
    {
        Assert.False(PreloadScanner.MightContainCandidates("<p>hello <b>world</b></p>"));
        Assert.False(PreloadScanner.MightContainCandidates(""));
        Assert.False(PreloadScanner.MightContainCandidates(null));
        Assert.True(PreloadScanner.MightContainCandidates("""<LINK REL="stylesheet" HREF="a.css">"""));
    }

    /// <summary>
    /// The pre-check is allowed to be wrong in one direction only. Saying "might" about a document
    /// with nothing in it costs a tokenizer pass on a pool thread; saying "no" about a document
    /// that does name a resource would silently switch the feature off for it.
    /// </summary>
    [Fact]
    public void The_Pre_Check_Is_Over_Permissive_Never_Under()
    {
        Assert.True(PreloadScanner.MightContainCandidates("<p>the word script appears in <scriptish>"));
        Assert.Empty(Scan("<p>the word script appears in <scriptish>").Candidates);
    }
}
