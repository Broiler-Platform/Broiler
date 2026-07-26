using Broiler.HtmlBridge;
using Broiler.HTML.Image;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// End-to-end validation that an <c>&lt;meta name="color-scheme"&gt;</c> reaches the used
/// <c>color-scheme</c> of the root element and thus the UA canvas backdrop (WPT
/// <c>html/semantics/…/meta-color-scheme-*</c>). The renderer already paints the dark canvas
/// (<c>rgb(18,18,18)</c>) for a <c>color-scheme: dark</c> root; the bridge's serialization
/// transform is what translates the meta into that property, applied only when the root declares
/// no <c>color-scheme</c> of its own.
/// </summary>
public class MetaColorSchemeRenderTests
{
    private static bool IsDarkCanvas(string html)
    {
        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, html, "file:///test.html");
        var serialized = bridge.SerializeToHtml();
        using var bitmap = HtmlRender.RenderToImageWithStyleSet(serialized, 200, 200);
        // The UA dark canvas is rgb(18,18,18); the light default is white.
        var corner = bitmap.GetPixel(5, 5);
        return corner.R == 18 && corner.G == 18 && corner.B == 18;
    }

    [Fact]
    public void Meta_Color_Scheme_Dark_Paints_The_Dark_Canvas() =>
        Assert.True(IsDarkCanvas("<!DOCTYPE html><meta name=\"color-scheme\" content=\"dark\"><title>x</title>"));

    [Fact]
    public void Meta_Color_Scheme_Dark_In_Body_Applies() =>
        // The meta may appear in the body (WPT meta-color-scheme-single-value-in-body).
        Assert.True(IsDarkCanvas("<!DOCTYPE html><body><meta name=\"color-scheme\" content=\"dark\">"));

    [Fact]
    public void Author_Root_Color_Scheme_Beats_The_Meta() =>
        // :root { color-scheme: light } beside <meta content=dark> stays light — author wins.
        Assert.False(IsDarkCanvas(
            "<!DOCTYPE html><meta name=\"color-scheme\" content=\"dark\"><style>:root{color-scheme:light}</style>"));

    [Fact]
    public void Invalid_Meta_Color_Scheme_Falls_Back_To_Light() =>
        // An unrecognised content value contributes no scheme, so the canvas stays the light
        // default (matches the ",,invalid" case in the spec's test suite).
        Assert.False(IsDarkCanvas("<!DOCTYPE html><meta name=\"color-scheme\" content=\",,invalid\"><title>x</title>"));

    [Fact]
    public void Http_Equiv_Color_Scheme_Is_Not_A_Color_Scheme_Meta() =>
        // Only name=color-scheme counts; an http-equiv of the same token does not.
        Assert.False(IsDarkCanvas("<!DOCTYPE html><meta http-equiv=\"color-scheme\" content=\"dark\"><title>x</title>"));
}
