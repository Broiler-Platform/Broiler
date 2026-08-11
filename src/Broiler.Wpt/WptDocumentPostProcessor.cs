using Broiler.Dom;
using Broiler.HtmlBridge;

namespace Broiler.Wpt;

/// <summary>
/// The DOM equivalent of <c>HtmlPostProcessor.Process</c> — the render-preparation the runner has
/// always applied to the serialised page, applied to the document instead when
/// <see cref="WptTestRunner.DomRender"/> renders it directly.
/// </summary>
/// <remarks>
/// <para>
/// Three of that method's passes can reach a WPT document, and all three are here: already-executed
/// <c>&lt;script&gt;</c>s are removed, <c>&lt;iframe&gt;</c> fallback content is emptied, and
/// <c>&lt;map&gt;</c> elements are dropped (the renderer has no image-map support and would lay
/// their contents out as visible blocks — 32 files in the corpus contain one).
/// </para>
/// <para>
/// The rest of that method is Acid-shaped and matches nothing here, which was checked against the
/// checkout rather than assumed: <c>id="linktest"</c>, <c>id="remove-last-child-test"</c>,
/// <c>map::after</c> and the <c>&lt;div id=" "&gt;FAIL&lt;/div&gt;</c> artifact each occur in zero
/// WPT documents. Reproducing them on the DOM would be untestable code.
/// </para>
/// <para>
/// The <c>:root</c> → <c>html</c> rewrite is applied to <c>&lt;style&gt;</c> text through the
/// string helper both paths share, so the A/B compares the render and not two different rewrites.
/// It is a workaround the renderer no longer needs — and a lossy one, since it drops <c>:root</c>'s
/// pseudo-class specificity to a type selector's — but retiring it is a change of its own, not
/// something to smuggle in under this lever.
/// </para>
/// </remarks>
internal static class WptDocumentPostProcessor
{
    internal static void Process(DomDocument document)
    {
        foreach (var element in Descendants(document))
        {
            var tag = element.TagName;

            if (tag.Equals("script", StringComparison.OrdinalIgnoreCase)
                || tag.Equals("map", StringComparison.OrdinalIgnoreCase))
            {
                element.ParentNode?.RemoveChild(element);
            }
            else if (tag.Equals("iframe", StringComparison.OrdinalIgnoreCase))
            {
                RemoveChildren(element);
            }
            else if (tag.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                RewriteStyleText(element);
            }
        }
    }

    /// <summary>
    /// Every descendant element, collected before anything is removed — the walk must not be
    /// iterating the child lists it is about to mutate.
    /// </summary>
    private static List<DomElement> Descendants(DomNode root)
    {
        var found = new List<DomElement>();
        Collect(root, found);
        return found;

        static void Collect(DomNode node, List<DomElement> into)
        {
            foreach (var child in node.ChildNodes)
            {
                if (child is DomElement element)
                    into.Add(element);
                Collect(child, into);
            }
        }
    }

    /// <summary>
    /// Applies the <c>:root</c> rewrite to a <c>&lt;style&gt;</c>'s text. Written through the text
    /// nodes rather than <c>TextContent</c>, which is read-only on a node.
    /// </summary>
    private static void RewriteStyleText(DomElement element)
    {
        foreach (var child in element.ChildNodes)
        {
            if (child is not DomText text)
                continue;

            var rewritten = HtmlPostProcessor.RewriteRootSelector(text.Data);
            if (!string.Equals(text.Data, rewritten, StringComparison.Ordinal))
                text.Data = rewritten;
        }
    }

    private static void RemoveChildren(DomElement element)
    {
        for (var index = element.ChildNodes.Count - 1; index >= 0; index--)
            element.RemoveChild(element.ChildNodes[index]);
    }
}
