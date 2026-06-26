
using Broiler.HTML.Dom;
using Broiler.Layout;

namespace Broiler.DevConsole;

/// <summary>
/// Provides read-only helper methods for querying the CSS box tree
/// without modifying rendering state.
/// </summary>
internal static class DomQueryHelper
{
    /// <summary>
    /// Returns all descendant boxes that have an HTML tag matching
    /// <paramref name="tagName"/> (case-insensitive).
    /// </summary>
    public static IEnumerable<CssBox> FindByTag(CssBox root, string tagName)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.HtmlTag is not null &&
            root.HtmlTag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase))
        {
            yield return root;
        }

        foreach (var child in root.Boxes)
        {
            foreach (var match in FindByTag(child, tagName))
                yield return match;
        }
    }

    /// <summary>
    /// Returns all descendant boxes whose HTML tag has an <c>id</c>
    /// attribute equal to <paramref name="id"/>.
    /// </summary>
    public static CssBox? FindById(CssBox root, string id)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.HtmlTag is not null)
        {
            var val = root.HtmlTag.TryGetAttribute("id");
            if (string.Equals(val, id, StringComparison.Ordinal))
                return root;
        }

        foreach (var child in root.Boxes)
        {
            var found = FindById(child, id);
            if (found is not null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Returns the total number of boxes in the tree rooted at
    /// <paramref name="root"/>, including <paramref name="root"/> itself.
    /// </summary>
    public static int CountBoxes(CssBox root)
    {
        ArgumentNullException.ThrowIfNull(root);

        int count = 1;
        foreach (var child in root.Boxes)
            count += CountBoxes(child);
        return count;
    }
}
