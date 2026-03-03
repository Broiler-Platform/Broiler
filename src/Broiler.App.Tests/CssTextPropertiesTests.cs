using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
[Trait("Feature", "Text")]
public class CssTextPropertiesTests
{
    [Fact]
    public void TextLayout_ParseWhiteSpace_Normal() => Assert.Equal(CssWhiteSpace.Normal, TextLayout.ParseWhiteSpace("normal"));

    [Fact]
    public void TextLayout_ParseWhiteSpace_NoWrap() => Assert.Equal(CssWhiteSpace.NoWrap, TextLayout.ParseWhiteSpace("nowrap"));

    [Fact]
    public void TextLayout_ParseWhiteSpace_Pre() => Assert.Equal(CssWhiteSpace.Pre, TextLayout.ParseWhiteSpace("pre"));

    [Fact]
    public void TextLayout_ParseWordBreak_Normal() => Assert.Equal(CssWordBreak.Normal, TextLayout.ParseWordBreak("normal"));

    [Fact]
    public void TextLayout_ParseWordBreak_BreakAll() => Assert.Equal(CssWordBreak.BreakAll, TextLayout.ParseWordBreak("break-all"));

    [Fact]
    public void TextLayout_ParseTextOverflow_Clip() => Assert.Equal(CssTextOverflow.Clip, TextLayout.ParseTextOverflow("clip"));

    [Fact]
    public void TextLayout_ParseTextOverflow_Ellipsis() => Assert.Equal(CssTextOverflow.Ellipsis, TextLayout.ParseTextOverflow("ellipsis"));

    [Fact]
    public void TextLayout_ShouldWrap_Normal_ReturnsTrue() => Assert.True(TextLayout.ShouldWrap(CssWhiteSpace.Normal));

    [Fact]
    public void TextLayout_ShouldWrap_NoWrap_ReturnsFalse() => Assert.False(TextLayout.ShouldWrap(CssWhiteSpace.NoWrap));

    [Fact]
    public void TextLayout_ShouldWrap_PreWrap_ReturnsTrue() => Assert.True(TextLayout.ShouldWrap(CssWhiteSpace.PreWrap));

    [Fact]
    public void TextLayout_ResolveWhiteSpace_Normal_CollapsesWhitespace()
    {
        var result = TextLayout.ResolveWhiteSpace(CssWhiteSpace.Normal, "hello   world\n  test");
        Assert.Equal("hello world test", result);
    }

    [Fact]
    public void TextLayout_ResolveWhiteSpace_Pre_PreservesWhitespace()
    {
        var result = TextLayout.ResolveWhiteSpace(CssWhiteSpace.Pre, "hello   world\n  test");
        Assert.Equal("hello   world\n  test", result);
    }

    [Fact]
    public void TextLayout_ApplyTextOverflow_Ellipsis_TruncatesLongText()
    {
        var result = TextLayout.ApplyTextOverflow(CssTextOverflow.Ellipsis, "Hello World", 50f, 10f);
        Assert.Contains("\u2026", result);
    }

    [Fact]
    public void TextLayout_ApplyTextOverflow_Clip_TruncatesWithoutEllipsis()
    {
        var result = TextLayout.ApplyTextOverflow(CssTextOverflow.Clip, "Hello World", 50f, 10f);
        Assert.DoesNotContain("\u2026", result);
        Assert.True(result.Length <= 5);
    }

    [Fact]
    public void CssFontFace_Parse_ExtractsFamilyAndSource()
    {
        var css = @"font-family: 'Open Sans'; src: url('opensans.woff2') format('woff2'); font-weight: 400; font-style: normal;";
        var face = CssFontFace.Parse(css);
        Assert.Equal("Open Sans", face.Family);
        Assert.Equal("opensans.woff2", face.Source);
        Assert.Equal("woff2", face.Format);
        Assert.Equal("400", face.Weight);
        Assert.Equal("normal", face.Style);
    }

    [Fact]
    public void CssFontFaceCollection_ExtractFromCss_FindsFontFaces()
    {
        var css = @"@font-face { font-family: 'MyFont'; src: url('myfont.woff2'); }";
        var collection = new CssFontFaceCollection();
        collection.ExtractFromCss(css);
        Assert.Single(collection.Faces);
        Assert.Equal("MyFont", collection.Faces[0].Family);
    }
}
