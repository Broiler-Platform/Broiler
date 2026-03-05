using System;
using System.Drawing;
using TheArtOfDev.HtmlRenderer.Adapters;
using TheArtOfDev.HtmlRenderer.Core.Dom;
using TheArtOfDev.HtmlRenderer.Core.Parse;

namespace TheArtOfDev.HtmlRenderer.Core.Utils;

internal static class CssUtils
{
    public static Color DefaultSelectionBackcolor { get; } = Color.FromArgb(0xa9, 0x33, 0x99, 0xFF);

    public static double WhiteSpace(RGraphics g, CssBoxProperties box)
    {
        double w = box.ActualFont.GetWhitespaceWidth(g);

        if (!(String.IsNullOrEmpty(box.WordSpacing) || box.WordSpacing == CssConstants.Normal))
            w += CssValueParser.ParseLength(box.WordSpacing, 0, box.GetEmHeight(), true);

        return w;
    }

    public static string GetPropertyValue(CssBox cssBox, string propName)
    {
        return propName switch
        {
            "border-bottom-width" => cssBox.BorderBottomWidth,
            "border-left-width" => cssBox.BorderLeftWidth,
            "border-right-width" => cssBox.BorderRightWidth,
            "border-top-width" => cssBox.BorderTopWidth,
            "border-bottom-style" => cssBox.BorderBottomStyle,
            "border-left-style" => cssBox.BorderLeftStyle,
            "border-right-style" => cssBox.BorderRightStyle,
            "border-top-style" => cssBox.BorderTopStyle,
            "border-bottom-color" => cssBox.BorderBottomColor,
            "border-left-color" => cssBox.BorderLeftColor,
            "border-right-color" => cssBox.BorderRightColor,
            "border-top-color" => cssBox.BorderTopColor,
            "border-spacing" => cssBox.BorderSpacing,
            "border-collapse" => cssBox.BorderCollapse,
            "corner-radius" => cssBox.CornerRadius,
            "border-radius" => cssBox.CornerRadius,
            "opacity" => cssBox.Opacity,
            "box-shadow" => cssBox.BoxShadow,
            "flex-direction" => cssBox.FlexDirection,
            "justify-content" => cssBox.JustifyContent,
            "align-items" => cssBox.AlignItems,
            "corner-nw-radius" => cssBox.CornerNwRadius,
            "corner-ne-radius" => cssBox.CornerNeRadius,
            "corner-se-radius" => cssBox.CornerSeRadius,
            "corner-sw-radius" => cssBox.CornerSwRadius,
            "margin-bottom" => cssBox.MarginBottom,
            "margin-left" => cssBox.MarginLeft,
            "margin-right" => cssBox.MarginRight,
            "margin-top" => cssBox.MarginTop,
            "padding-bottom" => cssBox.PaddingBottom,
            "padding-left" => cssBox.PaddingLeft,
            "padding-right" => cssBox.PaddingRight,
            "padding-top" => cssBox.PaddingTop,
            "page-break-inside" => cssBox.PageBreakInside,
            "left" => cssBox.Left,
            "top" => cssBox.Top,
            "width" => cssBox.Width,
            "max-width" => cssBox.MaxWidth,
            "min-width" => cssBox.MinWidth,
            "height" => cssBox.Height,
            "max-height" => cssBox.MaxHeight,
            "min-height" => cssBox.MinHeight,
            "background-color" => cssBox.BackgroundColor,
            "background-image" => cssBox.BackgroundImage,
            "background-position" => cssBox.BackgroundPosition,
            "background-repeat" => cssBox.BackgroundRepeat,
            "background-attachment" => cssBox.BackgroundAttachment,
            "background-size" => cssBox.BackgroundSize,
            "background-gradient" => cssBox.BackgroundGradient,
            "background-gradient-angle" => cssBox.BackgroundGradientAngle,
            "content" => cssBox.Content,
            "color" => cssBox.Color,
            "display" => cssBox.Display,
            "direction" => cssBox.Direction,
            "empty-cells" => cssBox.EmptyCells,
            "float" => cssBox.Float,
            "clear" => cssBox.Clear,
            "position" => cssBox.Position,
            "line-height" => cssBox.LineHeight,
            "vertical-align" => cssBox.VerticalAlign,
            "text-indent" => cssBox.TextIndent,
            "text-align" => cssBox.TextAlign,
            "text-decoration" => cssBox.TextDecoration,
            "white-space" => cssBox.WhiteSpace,
            "word-break" => cssBox.WordBreak,
            "visibility" => cssBox.Visibility,
            "word-spacing" => cssBox.WordSpacing,
            "font-family" => cssBox.FontFamily,
            "font-size" => cssBox.FontSize,
            "font-style" => cssBox.FontStyle,
            "font-variant" => cssBox.FontVariant,
            "font-weight" => cssBox.FontWeight,
            "list-style" => cssBox.ListStyle,
            "list-style-position" => cssBox.ListStylePosition,
            "list-style-image" => cssBox.ListStyleImage,
            "list-style-type" => cssBox.ListStyleType,
            "overflow" => cssBox.Overflow,
            _ => null,
        };
    }

    public static void SetPropertyValue(CssBox cssBox, string propName, string value)
    {
        switch (propName)
        {
            case "border-bottom-width":
                cssBox.BorderBottomWidth = value;
                break;
            case "border-left-width":
                cssBox.BorderLeftWidth = value;
                break;
            case "border-right-width":
                cssBox.BorderRightWidth = value;
                break;
            case "border-top-width":
                cssBox.BorderTopWidth = value;
                break;
            case "border-bottom-style":
                cssBox.BorderBottomStyle = value;
                break;
            case "border-left-style":
                cssBox.BorderLeftStyle = value;
                break;
            case "border-right-style":
                cssBox.BorderRightStyle = value;
                break;
            case "border-top-style":
                cssBox.BorderTopStyle = value;
                break;
            case "border-bottom-color":
                cssBox.BorderBottomColor = value;
                break;
            case "border-left-color":
                cssBox.BorderLeftColor = value;
                break;
            case "border-right-color":
                cssBox.BorderRightColor = value;
                break;
            case "border-top-color":
                cssBox.BorderTopColor = value;
                break;
            case "border-spacing":
                cssBox.BorderSpacing = value;
                break;
            case "border-collapse":
                cssBox.BorderCollapse = value;
                break;
            case "corner-radius":
                cssBox.CornerRadius = value;
                break;
            case "border-radius":
                cssBox.CornerRadius = value;
                break;
            case "opacity":
                cssBox.Opacity = value;
                break;
            case "box-shadow":
                cssBox.BoxShadow = value;
                break;
            case "flex-direction":
                cssBox.FlexDirection = value;
                break;
            case "justify-content":
                cssBox.JustifyContent = value;
                break;
            case "align-items":
                cssBox.AlignItems = value;
                break;
            case "corner-nw-radius":
                cssBox.CornerNwRadius = value;
                break;
            case "corner-ne-radius":
                cssBox.CornerNeRadius = value;
                break;
            case "corner-se-radius":
                cssBox.CornerSeRadius = value;
                break;
            case "corner-sw-radius":
                cssBox.CornerSwRadius = value;
                break;
            case "margin-bottom":
                cssBox.MarginBottom = value;
                break;
            case "margin-left":
                cssBox.MarginLeft = value;
                break;
            case "margin-right":
                cssBox.MarginRight = value;
                break;
            case "margin-top":
                cssBox.MarginTop = value;
                break;
            case "padding-bottom":
                cssBox.PaddingBottom = value;
                break;
            case "padding-left":
                cssBox.PaddingLeft = value;
                break;
            case "padding-right":
                cssBox.PaddingRight = value;
                break;
            case "padding-top":
                cssBox.PaddingTop = value;
                break;
            case "page-break-inside":
                cssBox.PageBreakInside = value;
                break;
            case "left":
                cssBox.Left = value;
                break;
            case "top":
                cssBox.Top = value;
                break;
            case "right":
                cssBox.Right = value;
                break;
            case "bottom":
                cssBox.Bottom = value;
                break;
            case "width":
                cssBox.Width = value;
                break;
            case "max-width":
                cssBox.MaxWidth = value;
                break;
            case "min-width":
                cssBox.MinWidth = value;
                break;
            case "height":
                cssBox.Height = value;
                break;
            case "max-height":
                cssBox.MaxHeight = value;
                break;
            case "min-height":
                cssBox.MinHeight = value;
                break;
            case "background-color":
                cssBox.BackgroundColor = value;
                break;
            case "background-image":
                cssBox.BackgroundImage = value;
                break;
            case "background-position":
                cssBox.BackgroundPosition = value;
                break;
            case "background-repeat":
                cssBox.BackgroundRepeat = value;
                break;
            case "background-attachment":
                cssBox.BackgroundAttachment = value;
                break;
            case "background-size":
                cssBox.BackgroundSize = value;
                break;
            case "background-gradient":
                cssBox.BackgroundGradient = value;
                break;
            case "background-gradient-angle":
                cssBox.BackgroundGradientAngle = value;
                break;
            case "color":
                cssBox.Color = value;
                break;
            case "content":
                cssBox.Content = value;
                break;
            case "display":
                cssBox.Display = value;
                break;
            case "direction":
                cssBox.Direction = value;
                break;
            case "empty-cells":
                cssBox.EmptyCells = value;
                break;
            case "float":
                cssBox.Float = value;
                break;
            case "clear":
                cssBox.Clear = value;
                break;
            case "position":
                cssBox.Position = value;
                break;
            case "line-height":
                cssBox.LineHeight = value;
                break;
            case "vertical-align":
                cssBox.VerticalAlign = value;
                break;
            case "text-indent":
                cssBox.TextIndent = value;
                break;
            case "text-align":
                cssBox.TextAlign = value;
                break;
            case "text-decoration":
                cssBox.TextDecoration = value;
                break;
            case "white-space":
                cssBox.WhiteSpace = value;
                break;
            case "word-break":
                cssBox.WordBreak = value;
                break;
            case "visibility":
                cssBox.Visibility = value;
                break;
            case "word-spacing":
                cssBox.WordSpacing = value;
                break;
            case "font-family":
                cssBox.FontFamily = value;
                break;
            case "font-size":
                cssBox.FontSize = value;
                break;
            case "font-style":
                cssBox.FontStyle = value;
                break;
            case "font-variant":
                cssBox.FontVariant = value;
                break;
            case "font-weight":
                cssBox.FontWeight = value;
                break;
            case "list-style":
                cssBox.ListStyle = value;
                break;
            case "list-style-position":
                cssBox.ListStylePosition = value;
                break;
            case "list-style-image":
                cssBox.ListStyleImage = value;
                break;
            case "list-style-type":
                cssBox.ListStyleType = value;
                break;
            case "overflow":
                cssBox.Overflow = value;
                break;
        }
    }
}