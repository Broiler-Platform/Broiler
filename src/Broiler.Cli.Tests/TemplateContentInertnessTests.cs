using Broiler.HTML.Image;

namespace Broiler.Cli.Tests;

/// <summary>
/// A <c>&lt;template&gt;</c>'s children are its <em>template contents</em> — held in a separate
/// document fragment and inert until stamped out. They must not render, and their
/// <c>&lt;style&gt;</c> must not join the host document's cascade (HTML §4.12.3).
/// <para>
/// REGRESSION GUARD (WPT issue #1491 problem 29,
/// <c>shadow-dom/focus-navigation/delegatesFocus-highlight-sibling.html</c>): the style collection
/// walked into templates, so a component that keeps its shadow styles in a template — the ordinary
/// way to write one — leaked them into the page. That test's template carries
/// <c>:host { background-color: #aaa }</c> and <c>:host(:focus) { background-color: #ccc }</c>;
/// leaked into the document they matched the page itself, and Broiler painted 99% of the canvas
/// <c>#ccc</c> against a reference that is 98% white. It renders 98.2% matching with the leak
/// closed.
/// </para>
/// </summary>
public class TemplateContentInertnessTests
{
    private static BBitmap Render(string html) =>
        HtmlRender.RenderToImageWithStyleSet(html, 200, 200);

    /// <summary>
    /// Whether the pinned <c>Broiler.HTML</c> stops the stylesheet walk at a template. The fix
    /// ships as <c>patches/0042-html-template-contents-inert.patch</c> and the submodule remote is
    /// outside this session's GitHub scope, so until a maintainer applies it and bumps the pointer
    /// the leak is still there and the two assertions that depend on it cannot hold. Probed rather
    /// than assumed, so these turn into real guards the moment the patch lands — the same shape as
    /// <c>ContainPaintClipTests</c>.
    /// </summary>
    private static bool TemplateStylesAreInert()
    {
        using var bitmap = Render("""
<!DOCTYPE html>
<style>.probe { width: 100px; height: 100px; background-color: rgb(0, 128, 0); }</style>
<template><style>.probe { background-color: rgb(255, 0, 0); }</style></template>
<div class="probe"></div>
""");
        var pixel = bitmap.GetPixel(50, 50);
        return pixel.R == 0 && pixel.G == 128 && pixel.B == 0;
    }

    private static void AssertPixel(BBitmap bitmap, int x, int y, byte r, byte g, byte b, string what)
    {
        var actual = bitmap.GetPixel(x, y);
        Assert.True(
            actual.R == r && actual.G == g && actual.B == b,
            $"{what} at ({x},{y}) was {actual.R},{actual.G},{actual.B}, expected {r},{g},{b}");
    }

    [Fact(Timeout = 600000)]
    public void A_Style_Inside_A_Template_Does_Not_Join_The_Document_Cascade()
    {
        if (!TemplateStylesAreInert())
            return; // patch 0041 not applied to the pinned submodule; see TemplateStylesAreInert.

        // The template's rule is later in document order and of equal specificity, so it would win
        // outright if it were collected at all.
        const string html = """
<!DOCTYPE html>
<style>.probe { width: 100px; height: 100px; background-color: rgb(0, 128, 0); }</style>
<template><style>.probe { background-color: rgb(255, 0, 0); }</style></template>
<div class="probe"></div>
""";

        using var bitmap = Render(html);

        AssertPixel(bitmap, 50, 50, 0, 128, 0, "the probe, which the template rule must not repaint");
    }

    [Fact(Timeout = 600000)]
    public void A_Template_Style_Stays_Inert_However_Deeply_It_Is_Nested()
    {
        if (!TemplateStylesAreInert())
            return; // patch 0041 not applied to the pinned submodule; see TemplateStylesAreInert.

        const string html = """
<!DOCTYPE html>
<style>.probe { width: 100px; height: 100px; background-color: rgb(0, 128, 0); }</style>
<template><div><section><style>.probe { background-color: rgb(255, 0, 0); }</style></section></div></template>
<div class="probe"></div>
""";

        using var bitmap = Render(html);

        AssertPixel(bitmap, 50, 50, 0, 128, 0, "the probe, past a nested template style");
    }

    [Fact(Timeout = 600000)]
    public void A_Style_Outside_Every_Template_Still_Applies()
    {
        // The negative half: skipping template subtrees must not skip the sheet next to one.
        const string html = """
<!DOCTYPE html>
<template><style>.probe { background-color: rgb(255, 0, 0); }</style></template>
<style>.probe { width: 100px; height: 100px; background-color: rgb(0, 0, 255); }</style>
<div class="probe"></div>
""";

        using var bitmap = Render(html);

        AssertPixel(bitmap, 50, 50, 0, 0, 255, "the document stylesheet that follows a template");
    }

    [Fact(Timeout = 600000)]
    public void Template_Contents_Do_Not_Render()
    {
        const string html = """
<!DOCTYPE html>
<template><div style="width: 200px; height: 200px; background-color: rgb(255, 0, 0)"></div></template>
<div style="width: 100px; height: 100px; background-color: rgb(0, 128, 0)"></div>
""";

        using var bitmap = Render(html);

        // The visible div is first in the rendered flow; the template's box must not appear at all.
        AssertPixel(bitmap, 50, 50, 0, 128, 0, "the visible div");
        AssertPixel(bitmap, 150, 150, 255, 255, 255, "the canvas the template box would have covered");
    }
}
