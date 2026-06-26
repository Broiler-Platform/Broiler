using System.Drawing;
using System.Net;

namespace Broiler.Layout;

/// <summary>
/// HTML element/attribute name constants used by layout (tag dispatch, link
/// detection). Ported from the renderer's <c>Broiler.HTML.Utils.HtmlConstants</c>
/// for the layout extraction (see <c>docs/roadmap/broiler-layout-component.md</c>);
/// the renderer keeps its own copy until the Phase 7 cleanup dedups.
/// </summary>
public static class HtmlConstants
{
    public const string A = "a";
    public const string Hr = "hr";
    public const string Iframe = "iframe";
    public const string Img = "img";
    public const string Href = "href";
}

/// <summary>
/// Small layout helpers ported from the renderer's
/// <c>Broiler.HTML.Utils.CommonUtils</c>. List-marker number formatting
/// (<c>ConvertToAlphaNumber</c>) stays host-side and is reached via
/// <see cref="ILayoutEnvironment.FormatListMarker"/>.
/// </summary>
public static class CommonUtils
{
    /// <summary>True for CJK-range characters (used as inter-character line-break opportunities).</summary>
    public static bool IsAsianCharecter(char ch) => ch >= 0x4e00 && ch <= 0xFA2D;

    /// <summary>Component-wise maximum of two sizes.</summary>
    public static SizeF Max(SizeF size, SizeF other)
        => new(System.Math.Max(size.Width, other.Width), System.Math.Max(size.Height, other.Height));
}

/// <summary>
/// HTML text helpers ported from the renderer's <c>Broiler.HTML.Utils.HtmlUtils</c>.
/// </summary>
public static class HtmlUtils
{
    /// <summary>Decodes HTML character entities in text content.</summary>
    public static string DecodeHtml(string str) => WebUtility.HtmlDecode(str);
}
