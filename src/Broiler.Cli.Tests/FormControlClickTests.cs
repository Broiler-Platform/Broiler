using Broiler.HTML.Dom;
using Broiler.Layout;
using Broiler.HTML.Dom.Utils;
using Broiler.HTML.Image;
using System.Drawing;

namespace Broiler.Cli.Tests;

/// <summary>
/// Regression tests for form control clickability.
/// Verifies that <c>&lt;input type="submit"&gt;</c>, <c>&lt;button&gt;</c>,
/// and <c>&lt;input type="button"&gt;</c> are recognized as clickable elements.
/// Previously, only <c>&lt;a&gt;</c> elements responded to clicks.
/// </summary>
public class FormControlClickTests
{
    /// <summary>
    /// Helper to get the layout root CssBox from HTML by using the Skia
    /// rendering engine (which exercises the same parsing and layout paths).
    /// </summary>
    private static CssBox GetLayoutRoot(string html, int width = 600, int height = 100)
    {
        using var container = new HtmlContainer();
        container.Location = new PointF(0, 0);
        container.MaxSize = new SizeF(width, height);
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html, null);

        using var bmp = new BBitmap(width, height);
        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(bmp, clip);

        return container.HtmlContainerInt.Root;
    }

    private static HtmlContainer CreateLaidOutContainer(string html, int width = 600, int height = 100)
    {
        var container = new HtmlContainer();
        container.Location = new PointF(0, 0);
        container.MaxSize = new SizeF(width, height);
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = true;
        container.SetHtml(html, null);

        using var bmp = new BBitmap(width, height);
        var clip = new RectangleF(0, 0, width, height);
        container.PerformLayout(bmp, clip);

        return container;
    }

    /// <summary>
    /// Finds the first CssBox whose HtmlTag matches the given tag name.
    /// </summary>
    private static CssBox FindBoxByTag(CssBox root, string tagName)
    {
        if (root.HtmlTag != null &&
            root.HtmlTag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (var child in root.Boxes)
        {
            var found = FindBoxByTag(child, tagName);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Finds the first CssBox whose HtmlTag matches the given tag name and
    /// has the specified attribute value.
    /// </summary>
    private static CssBox FindBoxByTagAndAttr(CssBox root, string tagName, string attr, string value)
    {
        if (root.HtmlTag != null &&
            root.HtmlTag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase) &&
            (root.HtmlTag.TryGetAttribute(attr) ?? "").Equals(value, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (var child in root.Boxes)
        {
            var found = FindBoxByTagAndAttr(child, tagName, attr, value);
            if (found != null)
                return found;
        }
        return null;
    }

    private static string CollectRenderedText(CssBox box)
    {
        var buffer = new System.Text.StringBuilder();
        AppendRenderedText(box, buffer);
        return buffer.ToString();
    }

    private static void AppendRenderedText(CssBox box, System.Text.StringBuilder buffer)
    {
        if (box.Text.Length > 0)
            buffer.Append(box.Text.ToString());

        foreach (var child in box.Boxes)
            AppendRenderedText(child, buffer);
    }

    [Fact]
    public void InputSubmit_IsClickable()
    {
        var root = GetLayoutRoot("<html><body><input type='submit' value='Go'></body></html>");
        var inputBox = FindBoxByTagAndAttr(root, "input", "type", "submit");
        Assert.NotNull(inputBox);
        Assert.True(inputBox.IsClickable, "input[type=submit] should be clickable");
    }

    [Fact]
    public void InputButton_IsClickable()
    {
        var root = GetLayoutRoot("<html><body><input type='button' value='Click'></body></html>");
        var inputBox = FindBoxByTagAndAttr(root, "input", "type", "button");
        Assert.NotNull(inputBox);
        Assert.True(inputBox.IsClickable, "input[type=button] should be clickable");
    }

    [Fact]
    public void InputReset_IsClickable()
    {
        var root = GetLayoutRoot("<html><body><form><input type='reset' value='Reset'></form></body></html>");
        var inputBox = FindBoxByTagAndAttr(root, "input", "type", "reset");
        Assert.NotNull(inputBox);
        Assert.True(inputBox.IsClickable, "input[type=reset] should be clickable");
    }

    [Fact]
    public void ButtonElement_IsClickable()
    {
        var root = GetLayoutRoot("<html><body><button>Click Me</button></body></html>");
        var buttonBox = FindBoxByTag(root, "button");
        Assert.NotNull(buttonBox);
        Assert.True(buttonBox.IsClickable, "<button> element should be clickable");
    }

    [Fact]
    public void InputText_IsNotClickable()
    {
        var root = GetLayoutRoot("<html><body><input type='text' value='Hello'></body></html>");
        var inputBox = FindBoxByTagAndAttr(root, "input", "type", "text");
        Assert.NotNull(inputBox);
        Assert.False(inputBox.IsClickable, "input[type=text] should NOT be clickable");
    }

    [Fact]
    public void EditableInputHitTest_FindsTextInput()
    {
        using var container = CreateLaidOutContainer("<html><body style='margin:0'><input id='q' name='query' type='text' value='Hello'></body></html>", 400, 40);
        var inputBox = FindBoxByTagAndAttr(container.HtmlContainerInt.Root, "input", "id", "q");
        Assert.NotNull(inputBox);

        var input = container.GetEditableInputAt(new PointF(inputBox.Location.X + 2, inputBox.Location.Y + 2));

        Assert.NotNull(input);
        Assert.Equal("q", input.Id);
        Assert.Equal("query", input.Name);
        Assert.Equal("text", input.Type);
        Assert.Equal("Hello", input.Value);
    }

    [Fact]
    public void EditableInputValueUpdate_UpdatesAttributeAndRenderedText()
    {
        using var container = CreateLaidOutContainer("<html><body style='margin:0'><input id='q' type='text'></body></html>", 400, 40);
        var inputBox = FindBoxByTagAndAttr(container.HtmlContainerInt.Root, "input", "id", "q");
        Assert.NotNull(inputBox);

        var editPoint = new PointF(inputBox.Location.X + 2, inputBox.Location.Y + 2);
        Assert.True(container.SetEditableInputValueAtDocumentPoint(editPoint, "Broiler"));

        Assert.Contains("value=\"Broiler\"", container.GetHtml());
        Assert.Contains("Broiler", CollectRenderedText(container.HtmlContainerInt.Root));
    }

    [Fact]
    public void AnchorLink_StillClickable()
    {
        var root = GetLayoutRoot("<html><body><a href='http://example.com'>Link</a></body></html>");
        var aBox = FindBoxByTag(root, "a");
        Assert.NotNull(aBox);
        Assert.True(aBox.IsClickable, "<a> link should still be clickable");
    }

    [Fact]
    public void GetLinkBox_FindsSubmitButton()
    {
        var html = @"<html><body style='margin:0'>
            <input type='submit' value='Search'>
        </body></html>";
        var root = GetLayoutRoot(html, 400, 40);
        var inputBox = FindBoxByTagAndAttr(root, "input", "type", "submit");
        Assert.NotNull(inputBox);

        // The submit button should be found when clicking in its area.
        // Get the box's center point.
        var loc = inputBox.Location;
        var clickPoint = new PointF(loc.X + 10, loc.Y + 5);
        var foundBox = DomUtils.GetLinkBox(root, clickPoint);
        Assert.NotNull(foundBox);
        Assert.True(foundBox.IsClickable);
    }

    [Fact]
    public void GetLinkBox_FindsButtonElement()
    {
        var html = @"<html><body style='margin:0'>
            <button>OK</button>
        </body></html>";
        var root = GetLayoutRoot(html, 400, 40);
        var buttonBox = FindBoxByTag(root, "button");
        Assert.NotNull(buttonBox);

        var loc = buttonBox.Location;
        var clickPoint = new PointF(loc.X + 10, loc.Y + 5);
        var foundBox = DomUtils.GetLinkBox(root, clickPoint);
        Assert.NotNull(foundBox);
        Assert.True(foundBox.IsClickable);
    }

    [Fact]
    public void Summary_In_Open_Details_Has_Open_Disclosure_Marker()
    {
        var root = GetLayoutRoot("""
<html><body>
  <details open>
    <summary>Summary</summary>
    <p>Details</p>
  </details>
</body></html>
""", 600, 160);

        var summaryBox = FindBoxByTag(root, "summary");
        Assert.NotNull(summaryBox);
        Assert.StartsWith("▾ ", CollectRenderedText(summaryBox));
    }

    [Fact]
    public void Summary_In_Closed_Details_Has_Closed_Disclosure_Marker()
    {
        var root = GetLayoutRoot("""
<html><body>
  <details>
    <summary>Summary</summary>
    <p>Details</p>
  </details>
</body></html>
""", 600, 160);

        var summaryBox = FindBoxByTag(root, "summary");
        Assert.NotNull(summaryBox);
        Assert.StartsWith("▸ ", CollectRenderedText(summaryBox));
    }
}
