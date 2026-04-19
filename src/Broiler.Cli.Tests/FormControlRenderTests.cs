using Broiler.HTML.Image;
using Broiler.HtmlBridge;
using SkiaSharp;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Verifies that HTML form controls are visible in rendered output.
/// Regression tests for:
/// - UA default styles (CssDefaults.cs)
/// - Value text injection (HtmlParser.cs)
/// - Min-width / min-height in inline-block layout (CssLayoutEngine.cs)
/// - Form stripping bug (HtmlPostProcessor should NOT strip form elements)
/// - Hidden input display:none (CssDefaults.cs + CssBlock attribute selectors)
/// - Attribute-conditioned CSS blocks preserved across Clone (CssBlock.cs)
/// - Block ordering for attribute-conditioned rules (CssData.cs)
/// </summary>
public class FormControlRenderTests
{
    private static int CountNonWhitePixels(SKBitmap bmp)
    {
        int count = 0;
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var px = bmp.GetPixel(x, y);
            if (px.Red < 250 || px.Green < 250 || px.Blue < 250)
                count++;
        }
        return count;
    }

    [Fact]
    public void InputSubmit_Renders_Visible()
    {
        var html = @"<html><body><input type='submit' value='Search'></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Submit button should render visible pixels (border + text)");
    }

    [Fact]
    public void InputText_Renders_Visible()
    {
        var html = @"<html><body><input type='text' value='Hello'></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Text input should render visible pixels (border + text)");
    }

    [Fact]
    public void Button_Element_Renders_Visible()
    {
        var html = @"<html><body><button>Click Me</button></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Button element should render visible pixels");
    }

    [Fact]
    public void Select_Element_Renders_Visible()
    {
        var html = @"<html><body><select><option>One</option></select></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Select element should render visible pixels (border)");
    }

    [Fact]
    public void Textarea_Element_Renders_Visible()
    {
        var html = @"<html><body><textarea>Some text</textarea></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Textarea element should render visible pixels (border + text)");
    }

    /// <summary>
    /// Regression: HtmlPostProcessor.Process must NOT strip form elements.
    /// Previously, StripHiddenTestArtifacts removed all &lt;form&gt; elements
    /// which made form controls inside &lt;form&gt; tags invisible.
    /// </summary>
    [Fact]
    public void FormWrapped_Controls_Not_Stripped()
    {
        var html = @"<html><body>
            <form action='/search'>
                <input type='text' name='q' value='Hello'>
                <input type='submit' value='Search'>
            </form>
        </body></html>";

        var processed = HtmlPostProcessor.Process(html);

        // The form tag and its contents must be preserved
        Assert.Contains("<input", processed);
        Assert.Contains("Search", processed);
        Assert.Contains("<form", processed);
    }

    /// <summary>
    /// Regression: Controls inside a form tag must render visibly after
    /// HtmlPostProcessor.Process.
    /// </summary>
    [Fact]
    public void FormWrapped_Controls_Render_Visible()
    {
        var html = @"<html><body>
            <form action='/search'>
                <input type='text' name='q' value='Hello'>
                <input type='submit' value='Search'>
            </form>
        </body></html>";

        var processed = HtmlPostProcessor.Process(html);
        using var bmp = HtmlRender.RenderToImage(processed, 600, 100);
        Assert.True(CountNonWhitePixels(bmp) > 50,
            "Form-wrapped controls should render visible pixels after post-processing");
    }

    /// <summary>
    /// Hidden inputs must render as invisible (display:none).
    /// Previously they rendered as visible 173px-wide inline-block boxes.
    /// </summary>
    [Fact]
    public void InputHidden_Renders_Invisible()
    {
        var html = @"<html><body style='margin:0'><input type='hidden' name='x'></body></html>";
        using var bmp = HtmlRender.RenderToImage(html, 400, 50);
        Assert.Equal(0, CountNonWhitePixels(bmp));
    }

    /// <summary>
    /// Regression: Author CSS targeting <c>input</c> must NOT cause hidden
    /// input styles to bleed into all inputs.  This tests the
    /// <see cref="Broiler.HTML.Core.Core.Entities.CssBlock.Clone"/> fix
    /// that preserves <c>AttributeConditions</c> across cloning, and the
    /// <see cref="Broiler.HTML.Core.Core.Entities.CssBlock.EqualsSelector"/>
    /// fix that prevents merging blocks with different attribute conditions.
    /// </summary>
    [Fact]
    public void AuthorCss_Does_Not_Break_InputVisibility()
    {
        var html = @"<html><body>
        <style>input { color: red; }</style>
        <input type='submit' value='Test'>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Author CSS on input must not make submit buttons invisible");
    }

    /// <summary>
    /// Author CSS attribute selectors (e.g. <c>input[type="submit"]</c>)
    /// must still apply correctly when combined with UA stylesheet.
    /// </summary>
    [Fact]
    public void AuthorAttrSelector_Applies_To_MatchingInputs()
    {
        var html = @"<html><body>
        <style>
            input[type=""submit""] { color: red; padding: 10px; }
        </style>
        <input type='submit' value='Test'>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 400, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Author CSS attribute selector must apply to matching inputs");
    }

    /// <summary>
    /// Compound class + attribute selector (e.g. <c>.buttons input[type="submit"]</c>)
    /// must apply correctly.
    /// </summary>
    [Fact]
    public void CompoundClassAndAttrSelector_Applies()
    {
        var html = @"<html><body>
        <style>
            .buttons input[type=""submit""] {
                background:#f8f9fa; border:1px solid #f8f9fa;
                color:#3c4043; font-size:14px; padding:10px 16px;
            }
        </style>
        <div class='buttons'>
            <input type='submit' value='Google Search'>
            <input type='submit' value=""I'm Feeling Lucky"">
        </div>
        </body></html>";

        using var bmp = HtmlRender.RenderToImage(html, 600, 100);
        Assert.True(CountNonWhitePixels(bmp) > 10,
            "Compound class + attribute selector buttons must be visible");
    }

    [Fact]
    public void FormControls_ComputedLogicalSizes_Follow_WritingMode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<button id='button-h'>Go</button>
<button id='button-v' style='writing-mode: vertical-rl'>Go</button>
<select id='select-h'><option>One</option></select>
<select id='select-v' style='writing-mode: vertical-lr'><option>One</option></select>
<div id='result'></div>
<script>
function check(id, vertical) {
  var style = window.getComputedStyle(document.getElementById(id));
  var blockSize = parseInt(style.blockSize, 10);
  var inlineSize = parseInt(style.inlineSize, 10);
  var width = style.width;
  var height = style.height;
  return blockSize > 0 &&
         inlineSize > 0 &&
         (vertical ? blockSize === parseInt(width, 10) && inlineSize === parseInt(height, 10)
                   : blockSize === parseInt(height, 10) && inlineSize === parseInt(width, 10));
}
document.getElementById('result').textContent = [
  check('button-h', false),
  check('button-v', true),
  check('select-h', false),
  check('select-v', true)
].join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true,true,true", result);
    }

    [Fact]
    public void DateInput_ComputedLogicalSizes_Follow_WritingMode()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<input type='date' id='date-h'>
<input type='date' id='date-v' style='writing-mode: vertical-lr'>
<div id='result'></div>
<script>
function check(id, vertical) {
  var style = window.getComputedStyle(document.getElementById(id));
  var blockSize = parseInt(style.blockSize, 10);
  var inlineSize = parseInt(style.inlineSize, 10);
  var width = parseInt(style.width, 10);
  var height = parseInt(style.height, 10);
  return blockSize > 0 &&
         inlineSize > 0 &&
         (vertical ? blockSize === width && inlineSize === height
                   : blockSize === height && inlineSize === width);
}
document.getElementById('result').textContent = [
  check('date-h', false),
  check('date-v', true)
].join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }
}
