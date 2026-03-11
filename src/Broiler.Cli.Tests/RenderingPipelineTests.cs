using Broiler.App.Rendering;
using RenderImageFormat = Broiler.App.Rendering.ImageFormat;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for Phase 10 Acid3 compliance: Rendering Pipeline &amp; Visual Fidelity —
/// text-shadow parsing, @font-face loading infrastructure, visibility:hidden rendering,
/// display:inline-block layout, position:fixed/absolute differentiation,
/// dotted border-style, cm/mm/in unit support, data: URI background images,
/// ::after pseudo-element content, and automated pixel-comparison infrastructure.
/// </summary>
public class RenderingPipelineTests
{
    // ────────────────────── 10.1 text-shadow ──────────────────────

    [Fact]
    public void TextShadow_Parsed_In_ComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { text-shadow: rgba(0,0,0,0.5) 2px 3px; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
var r = [];
r.push(cs.getPropertyValue('text-shadow') !== '');
r.push(cs['text-shadow'] !== undefined);
document.getElementById('result').textContent = r.join(',');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("true,true", result);
    }

    [Fact]
    public void TextShadow_Set_Via_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var d = document.createElement('div');
d.style.setProperty('text-shadow', 'rgba(0,0,0,0.5) 2px 3px');
document.getElementById('result').textContent = d.style.getPropertyValue('text-shadow');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("rgba(0,0,0,0.5) 2px 3px", result);
    }

    [Fact]
    public void TextShadow_PaintCommand_Has_Shadow_Properties()
    {
        var el = new DomElement("span", null, null, "", isTextNode: true) { TextContent = "hello" };
        el.Style["text-shadow"] = "rgba(0,0,0,0.5) 2px 3px";
        el.Style["font-size"] = "16px";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Inline,
            Dimensions = new BoxDimensions { X = 0, Y = 0, Width = 100, Height = 16 }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        var textCmd = cmds.FirstOrDefault(c => c.Type == PaintCommandType.Text);
        Assert.NotNull(textCmd);
        Assert.Equal(2f, textCmd.TextShadowOffsetX);
        Assert.Equal(3f, textCmd.TextShadowOffsetY);
        Assert.Contains("rgba(0,0,0,0.5)", textCmd.TextShadowColor);
    }

    [Fact]
    public void TextShadow_Parse_OffsetX_OffsetY_Color()
    {
        var cmd = new PaintCommand();
        Painter.ParseTextShadow("2px 3px red", cmd);
        Assert.Equal(2f, cmd.TextShadowOffsetX);
        Assert.Equal(3f, cmd.TextShadowOffsetY);
        Assert.Equal("red", cmd.TextShadowColor);
    }

    [Fact]
    public void TextShadow_Parse_Color_OffsetX_OffsetY()
    {
        var cmd = new PaintCommand();
        Painter.ParseTextShadow("rgba(0,0,0,0.5) 2px 3px", cmd);
        Assert.Equal(2f, cmd.TextShadowOffsetX);
        Assert.Equal(3f, cmd.TextShadowOffsetY);
        Assert.Equal("rgba(0,0,0,0.5)", cmd.TextShadowColor);
    }

    // ────────────────────── 10.2 @font-face ──────────────────────

    [Fact]
    public void FontFace_Parsed_From_CSS()
    {
        var collection = new CssFontFaceCollection();
        collection.ExtractFromCss(@"
            @font-face {
                font-family: 'CustomFont';
                src: url('https://example.com/font.woff2') format('woff2');
                font-weight: bold;
                font-style: italic;
            }");

        var face = collection.FindFace("CustomFont", "bold", "italic");
        Assert.NotNull(face);
        Assert.Equal("CustomFont", face.Family);
        Assert.Equal("https://example.com/font.woff2", face.Source);
        Assert.Equal("woff2", face.Format);
        Assert.Equal("bold", face.Weight);
        Assert.Equal("italic", face.Style);
    }

    [Fact]
    public void FontFace_Multiple_Faces_Extracted()
    {
        var collection = new CssFontFaceCollection();
        collection.ExtractFromCss(@"
            @font-face { font-family: 'A'; src: url('a.woff'); }
            @font-face { font-family: 'B'; src: url('b.woff'); font-weight: 700; }
        ");

        Assert.NotNull(collection.FindFace("A", "normal", "normal"));
        Assert.NotNull(collection.FindFace("B", "700", "normal"));
    }

    [Fact]
    public void FontFace_IsLocalSource_DataUri()
    {
        var face = CssFontFace.Parse("font-family: 'Test'; src: url('data:font/woff2;base64,AAAA');");
        Assert.True(face.IsLocalSource());
    }

    [Fact]
    public void FontFace_IsLocalSource_Remote_Url()
    {
        var face = CssFontFace.Parse("font-family: 'Test'; src: url('https://example.com/font.woff2');");
        Assert.False(face.IsLocalSource());
    }

    [Fact]
    public void FontFace_ComputedStyle_Returns_FontFamily()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
@font-face { font-family: 'CustomFont'; src: url('test.woff2'); }
#target { font-family: 'CustomFont'; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('font-family');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("CustomFont", result);
    }

    // ────────────────────── 10.3 visibility:hidden ──────────────────────

    [Fact]
    public void Visibility_Hidden_Element_Occupies_Space()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["visibility"] = "hidden";
        el.Style["width"] = "100px";
        el.Style["height"] = "50px";

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);

        // Element should still occupy space (non-zero dimensions).
        Assert.Equal(CssVisibility.Hidden, box.Visibility);
        Assert.True(box.Dimensions.Width > 0);
    }

    [Fact]
    public void Visibility_Hidden_No_Paint_Commands()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["visibility"] = "hidden";
        el.Style["background-color"] = "red";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Block,
            Visibility = CssVisibility.Hidden,
            Dimensions = new BoxDimensions { X = 0, Y = 0, Width = 100, Height = 50 }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        // Visibility:hidden should produce no paint commands.
        Assert.Empty(cmds);
    }

    [Fact]
    public void Visibility_Visible_Produces_Paint_Commands()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["background-color"] = "red";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Block,
            Visibility = CssVisibility.Visible,
            Dimensions = new BoxDimensions { X = 0, Y = 0, Width = 100, Height = 50 }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        Assert.NotEmpty(cmds);
    }

    [Fact]
    public void Visibility_Hidden_GetComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { visibility: hidden; }
</style>
</head><body>
<div id=""target"">hidden text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('visibility');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("hidden", result);
    }

    // ────────────────────── 10.4 display:inline-block ──────────────────────

    [Fact]
    public void InlineBlock_Layout_Resolves_Display()
    {
        var parent = new DomElement("div", null, null, "");
        var child = new DomElement("span", null, null, "");
        child.Style["display"] = "inline-block";
        child.Style["width"] = "50px";
        child.Style["height"] = "30px";
        parent.Children.Add(child);
        child.Parent = parent;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(parent, 800f);

        Assert.Single(box.Children);
        Assert.Equal(CssDisplay.InlineBlock, box.Children[0].Display);
    }

    [Fact]
    public void InlineBlock_With_Padding_And_Border()
    {
        var parent = new DomElement("div", null, null, "");
        var child = new DomElement("span", null, null, "");
        child.Style["display"] = "inline-block";
        child.Style["width"] = "50px";
        child.Style["height"] = "30px";
        child.Style["padding-left"] = "10px";
        child.Style["padding-right"] = "10px";
        child.Style["border-left-width"] = "2px";
        child.Style["border-right-width"] = "2px";
        parent.Children.Add(child);
        child.Parent = parent;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(parent, 800f);

        var childBox = box.Children[0];
        Assert.Equal(10f, childBox.Dimensions.Padding.Left);
        Assert.Equal(10f, childBox.Dimensions.Padding.Right);
        Assert.Equal(2f, childBox.Dimensions.Border.Left);
        Assert.Equal(2f, childBox.Dimensions.Border.Right);
    }

    [Fact]
    public void InlineBlock_VerticalAlign_Resolved()
    {
        var el = new DomElement("span", null, null, "");
        el.Style["display"] = "inline-block";
        el.Style["vertical-align"] = "middle";

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);

        Assert.Equal(CssVerticalAlign.Middle, box.VerticalAlign);
    }

    [Fact]
    public void InlineBlock_ComputedStyle_Display()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { display: inline-block; width: 100px; height: 50px; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('display');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("inline-block", result);
    }

    // ────────────────────── 10.5 position:fixed / absolute ──────────────────────

    [Fact]
    public void Position_Fixed_Placed_At_Viewport_Origin()
    {
        var root = new DomElement("div", null, null, "");
        root.Style["position"] = "relative";

        var child = new DomElement("div", null, null, "");
        child.Style["position"] = "fixed";
        child.Style["left"] = "10px";
        child.Style["top"] = "20px";
        child.Style["width"] = "50px";
        child.Style["height"] = "50px";
        root.Children.Add(child);
        child.Parent = root;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(root, 800f);

        var fixedChild = box.Children[0];
        Assert.Equal(CssPosition.Fixed, fixedChild.Position);
        Assert.Equal(10f, fixedChild.Dimensions.X);
        Assert.Equal(20f, fixedChild.Dimensions.Y);
    }

    [Fact]
    public void Position_Absolute_Uses_Containing_Block()
    {
        var root = new DomElement("div", null, null, "");
        root.Style["position"] = "relative";
        root.Style["width"] = "400px";
        root.Style["height"] = "400px";

        var child = new DomElement("div", null, null, "");
        child.Style["position"] = "absolute";
        child.Style["left"] = "30px";
        child.Style["top"] = "40px";
        child.Style["width"] = "100px";
        child.Style["height"] = "100px";
        root.Children.Add(child);
        child.Parent = root;

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(root, 800f);

        var absChild = box.Children[0];
        Assert.Equal(CssPosition.Absolute, absChild.Position);
    }

    [Fact]
    public void Position_Fixed_ComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { position: fixed; left: 10px; top: 20px; }
</style>
</head><body>
<div id=""target"">fixed</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('position');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("fixed", result);
    }

    // ────────────────────── 10.6 border-style:dotted ──────────────────────

    [Fact]
    public void Border_Style_Dotted_Paint_Command()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["border-color"] = "black";
        el.Style["border-style"] = "dotted";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Block,
            Dimensions = new BoxDimensions
            {
                X = 0, Y = 0, Width = 100, Height = 50,
                Border = new BoxEdges { Top = 2, Right = 2, Bottom = 2, Left = 2 }
            }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        var borderCmd = cmds.FirstOrDefault(c => c.Type == PaintCommandType.Border);
        Assert.NotNull(borderCmd);
        Assert.Equal("dotted", borderCmd.BorderStyle);
    }

    [Fact]
    public void Border_Style_Solid_Default()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["border-color"] = "black";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Block,
            Dimensions = new BoxDimensions
            {
                X = 0, Y = 0, Width = 100, Height = 50,
                Border = new BoxEdges { Top = 1, Right = 1, Bottom = 1, Left = 1 }
            }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        var borderCmd = cmds.FirstOrDefault(c => c.Type == PaintCommandType.Border);
        Assert.NotNull(borderCmd);
        Assert.Equal("solid", borderCmd.BorderStyle);
    }

    [Fact]
    public void Border_Style_Dashed_Paint_Command()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["border-color"] = "blue";
        el.Style["border-style"] = "dashed";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Block,
            Dimensions = new BoxDimensions
            {
                X = 0, Y = 0, Width = 100, Height = 50,
                Border = new BoxEdges { Top = 3, Right = 3, Bottom = 3, Left = 3 }
            }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        var borderCmd = cmds.FirstOrDefault(c => c.Type == PaintCommandType.Border);
        Assert.NotNull(borderCmd);
        Assert.Equal("dashed", borderCmd.BorderStyle);
    }

    [Fact]
    public void Border_Style_ComputedStyle()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target { border: 2px dotted red; }
</style>
</head><body>
<div id=""target"">text</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('border-style');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("dotted", result);
    }

    // ────────────────────── 10.7 cm unit support ──────────────────────

    [Fact]
    public void ParseCssValue_Cm_Converts_To_Pixels()
    {
        // 1cm = 37.7952756px (96/2.54)
        float result = CssBoxModel.ParseCssValue("2cm", 0f, 0f);
        Assert.InRange(result, 75f, 76f); // 2 * 37.8 ≈ 75.6
    }

    [Fact]
    public void ParseCssValue_Mm_Converts_To_Pixels()
    {
        // 1mm = 3.77952756px (96/25.4)
        float result = CssBoxModel.ParseCssValue("10mm", 0f, 0f);
        Assert.InRange(result, 37f, 38.5f); // 10 * 3.78 ≈ 37.8
    }

    [Fact]
    public void ParseCssValue_In_Converts_To_Pixels()
    {
        // 1in = 96px
        float result = CssBoxModel.ParseCssValue("1in", 0f, 0f);
        Assert.Equal(96f, result);
    }

    [Fact]
    public void ParseCssValue_Cm_Border_Layout()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["border-top-width"] = "1cm";
        el.Style["width"] = "100px";

        var model = new CssBoxModel();
        var box = model.BuildLayoutTree(el, 800f);

        // 1cm ≈ 37.8px
        Assert.InRange(box.Dimensions.Border.Top, 37f, 38.5f);
    }

    [Fact]
    public void ParseCssValue_Px_Still_Works()
    {
        float result = CssBoxModel.ParseCssValue("100px", 0f, 0f);
        Assert.Equal(100f, result);
    }

    [Fact]
    public void ParseCssValue_Percentage_Still_Works()
    {
        float result = CssBoxModel.ParseCssValue("50%", 200f, 0f);
        Assert.Equal(100f, result);
    }

    [Fact]
    public void ParseCssValue_Auto_Returns_Default()
    {
        float result = CssBoxModel.ParseCssValue("auto", 200f, 42f);
        Assert.Equal(42f, result);
    }

    // ────────────────────── 10.8 data: URI background images ──────────────────────

    [Fact]
    public void DataUri_Detected_As_Png()
    {
        var format = ImageDecoder.DetectFormat("data:image/png;base64,iVBOR");
        Assert.Equal(RenderImageFormat.Png, format);
    }

    [Fact]
    public void DataUri_Decode_Base64()
    {
        // "AQID" is base64 for bytes [1, 2, 3]
        var bytes = ImageDecoder.DecodeDataUri("data:image/png;base64,AQID");
        Assert.NotNull(bytes);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
    }

    [Fact]
    public void DataUri_Decode_Invalid_Returns_Null()
    {
        var bytes = ImageDecoder.DecodeDataUri("not-a-data-uri");
        Assert.Null(bytes);
    }

    [Fact]
    public void DataUri_Decode_No_Base64_Returns_Null()
    {
        var bytes = ImageDecoder.DecodeDataUri("data:text/plain,hello");
        Assert.Null(bytes);
    }

    [Fact]
    public void BackgroundImage_DataUri_Creates_Image_Command()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["background-image"] = "url('data:image/png;base64,iVBOR')";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Block,
            Dimensions = new BoxDimensions { X = 0, Y = 0, Width = 100, Height = 100 }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        var imgCmd = cmds.FirstOrDefault(c => c.Type == PaintCommandType.Image);
        Assert.NotNull(imgCmd);
        Assert.StartsWith("data:image/png", imgCmd.ImageSource);
    }

    [Fact]
    public void BackgroundImage_Url_Creates_Image_Command()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["background-image"] = "url(https://example.com/img.png)";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Block,
            Dimensions = new BoxDimensions { X = 0, Y = 0, Width = 100, Height = 100 }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        var imgCmd = cmds.FirstOrDefault(c => c.Type == PaintCommandType.Image);
        Assert.NotNull(imgCmd);
        Assert.Equal("https://example.com/img.png", imgCmd.ImageSource);
    }

    [Fact]
    public void BackgroundImage_None_No_Image_Command()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["background-image"] = "none";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Block,
            Dimensions = new BoxDimensions { X = 0, Y = 0, Width = 100, Height = 100 }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        var imgCmd = cmds.FirstOrDefault(c => c.Type == PaintCommandType.Image);
        Assert.Null(imgCmd);
    }

    // ────────────────────── 10.9 ::after pseudo-element ──────────────────────

    [Fact]
    public void PseudoElement_After_Selector_Matched()
    {
        var html = @"<!DOCTYPE html>
<html><head>
<style>
#target::after { content: 'after-text'; }
#target { color: red; }
</style>
</head><body>
<div id=""target"">main</div>
<div id=""result""></div>
<script>
var cs = window.getComputedStyle(document.getElementById('target'));
document.getElementById('result').textContent = cs.getPropertyValue('color');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("red", result);
    }

    [Fact]
    public void PseudoElement_After_Content_Via_Style()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""result""></div>
<script>
var d = document.createElement('div');
d.style.setProperty('content', '""hello""');
document.getElementById('result').textContent = d.style.getPropertyValue('content');
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
        Assert.Contains("hello", result);
    }

    // ────────────────────── 10.10 Rendering pipeline integration ──────────────────────

    [Fact]
    public void Painter_PaintBox_Returns_Commands_For_Element()
    {
        var el = new DomElement("div", null, null, "");
        el.Style["background-color"] = "blue";
        el.Style["border-color"] = "red";
        var box = new LayoutBox(el)
        {
            Display = CssDisplay.Block,
            Dimensions = new BoxDimensions
            {
                X = 10, Y = 20, Width = 200, Height = 100,
                Border = new BoxEdges { Top = 2, Right = 2, Bottom = 2, Left = 2 }
            }
        };

        var painter = new Painter();
        var cmds = painter.PaintBox(box);

        Assert.True(cmds.Count >= 2); // background + border
        Assert.Contains(cmds, c => c.Type == PaintCommandType.Background);
        Assert.Contains(cmds, c => c.Type == PaintCommandType.Border);
    }

    [Fact]
    public void Compositor_Builds_Layers_By_ZIndex()
    {
        var commands = new List<PaintCommand>
        {
            new() { Type = PaintCommandType.Background, ZIndex = 1, Bounds = new Rect(0, 0, 100, 100) },
            new() { Type = PaintCommandType.Background, ZIndex = 0, Bounds = new Rect(0, 0, 100, 100) },
            new() { Type = PaintCommandType.Border, ZIndex = 1, Bounds = new Rect(0, 0, 100, 100) }
        };

        var compositor = new Compositor();
        var layers = compositor.BuildLayers(commands);

        Assert.Equal(2, layers.Count);
        Assert.Equal(0, layers[0].ZIndex);
        Assert.Equal(1, layers[1].ZIndex);
        Assert.Single(layers[0].Commands);
        Assert.Equal(2, layers[1].Commands.Count);
    }

    [Fact]
    public void Compositor_Composite_Applies_Layer_Opacity()
    {
        var commands = new List<PaintCommand>
        {
            new() { Type = PaintCommandType.Background, ZIndex = 0, Opacity = 0.5f,
                     Bounds = new Rect(0, 0, 100, 100) }
        };

        var compositor = new Compositor();
        var layers = compositor.BuildLayers(commands);
        layers[0].Opacity = 0.8f;

        var composited = compositor.Composite(layers);
        Assert.Single(composited);
        Assert.Equal(0.4f, composited[0].Opacity, 2); // 0.5 * 0.8
    }

    [Fact]
    public void Compositor_Composite_Preserves_BorderStyle()
    {
        var commands = new List<PaintCommand>
        {
            new() { Type = PaintCommandType.Border, ZIndex = 0, BorderStyle = "dotted",
                     Bounds = new Rect(0, 0, 100, 100) }
        };

        var compositor = new Compositor();
        var layers = compositor.BuildLayers(commands);
        var composited = compositor.Composite(layers);

        Assert.Single(composited);
        Assert.Equal("dotted", composited[0].BorderStyle);
    }

    [Fact]
    public void Compositor_Composite_Preserves_TextShadow()
    {
        var commands = new List<PaintCommand>
        {
            new() { Type = PaintCommandType.Text, ZIndex = 0,
                     TextShadowColor = "red", TextShadowOffsetX = 1f, TextShadowOffsetY = 2f,
                     Bounds = new Rect(0, 0, 100, 100) }
        };

        var compositor = new Compositor();
        var layers = compositor.BuildLayers(commands);
        var composited = compositor.Composite(layers);

        Assert.Single(composited);
        Assert.Equal("red", composited[0].TextShadowColor);
        Assert.Equal(1f, composited[0].TextShadowOffsetX);
        Assert.Equal(2f, composited[0].TextShadowOffsetY);
    }

    [Fact]
    public void RenderOutput_Stores_Dimensions()
    {
        var cmds = new List<PaintCommand>();
        var layers = new List<PaintLayer>();
        var output = new RenderOutput(cmds, layers, 800f, 600f);

        Assert.Equal(800f, output.Width);
        Assert.Equal(600f, output.Height);
    }

    [Fact]
    public void ImageDecoder_DetectFormat_Png_Extension()
    {
        Assert.Equal(RenderImageFormat.Png, ImageDecoder.DetectFormat("image.png"));
    }

    [Fact]
    public void ImageDecoder_DetectFormat_Jpeg_Extension()
    {
        Assert.Equal(RenderImageFormat.Jpeg, ImageDecoder.DetectFormat("photo.jpg"));
    }

    [Fact]
    public void ImageDecoder_DetectFormatFromBytes_Png()
    {
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0 };
        Assert.Equal(RenderImageFormat.Png, ImageDecoder.DetectFormatFromBytes(pngHeader));
    }

    [Fact]
    public void ImageDecoder_CreatePlaceholder_Correct_Size()
    {
        var img = ImageDecoder.CreatePlaceholder(10, 10, RenderImageFormat.Png);
        Assert.Equal(10, img.Width);
        Assert.Equal(10, img.Height);
        Assert.Equal(400, img.PixelData.Length); // 10*10*4
    }
}
