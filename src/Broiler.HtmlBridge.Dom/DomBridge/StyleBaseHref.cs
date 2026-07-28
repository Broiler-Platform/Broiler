using System.Linq;
using Broiler.Dom;

namespace Broiler.HtmlBridge;

/// <summary>
/// Render-time <c>&lt;base href&gt;</c> resolution for stylesheet <c>url()</c> references.
/// The renderer resolves a relative CSS <c>url()</c> against a single document base URL and
/// never consults the <c>&lt;base&gt;</c> element, so a page that sets
/// <c>&lt;base href="/images/"&gt;</c> and then references <c>url(green.png)</c> resolved the
/// image against the document's own directory instead of the base — the resource was not
/// found and the fallback colour showed through (the WPT <c>css/…/*base-uri*</c> family, e.g.
/// <c>css-values/inline-cache-base-uri-cssom</c>).
/// <para>
/// This transform rewrites relative <c>url()</c> references in every <c>&lt;style&gt;</c>
/// against the document's first <c>&lt;base href&gt;</c> so they reach the renderer already
/// resolved. A root-relative base (<c>/images/</c>) keeps the resolved URL root-relative, so a
/// host-relative resource mapper (the WPT <c>wptRoot</c> handler, which serves <c>/x</c> from
/// the test root) still recognises it; an absolute base resolves to an absolute URL.
/// </para>
/// <para>
/// Runs inside <see cref="ApplySerializationTransforms"/> after
/// <see cref="InlineStyleSheetImports"/> (so inlined <c>@import</c> content — already rebased
/// onto the imported sheet's own URL — carries absolute <c>url()</c>s this pass leaves alone).
/// Only the render-bound serialization is affected; the live CSSOM rule model and JS-visible
/// serialization are untouched. Only <c>&lt;style&gt;</c> <c>url()</c>s are rewritten today;
/// element <c>src</c>/<c>href</c> attributes still resolve without <c>&lt;base&gt;</c>.
/// </para>
/// </summary>
public sealed partial class DomBridge
{
    /// <summary>
    /// Rewrites relative <c>url()</c> references in every <c>&lt;style&gt;</c> in the tree
    /// against the document's first <c>&lt;base href&gt;</c>. A no-op (byte-identical output)
    /// when the document declares no usable <c>&lt;base href&gt;</c>.
    /// </summary>
    private void ApplyBaseHrefToStyleUrls(DomElement root)
    {
        if (!TryFindDocumentBaseHref(root, out var baseHref))
            return;

        RewriteStyleElementUrls(root, baseHref);
    }

    /// <summary>The first <c>&lt;base&gt;</c> in document order with a non-empty
    /// <c>href</c> — the element that sets the document base URL (HTML §4.2.3).</summary>
    private bool TryFindDocumentBaseHref(DomElement root, out string baseHref)
    {
        baseHref = string.Empty;
        foreach (var element in root.Descendants().OfType<DomElement>())
        {
            if (element.TagName.Equals("base", StringComparison.OrdinalIgnoreCase) &&
                TryGetAttribute(element, "href", out var href) &&
                !string.IsNullOrWhiteSpace(href))
            {
                baseHref = href.Trim();
                return true;
            }
        }

        return false;
    }

    private void RewriteStyleElementUrls(DomElement element, string baseHref)
    {
        if (!IsText(element) &&
            element.TagName.Equals("style", StringComparison.OrdinalIgnoreCase))
        {
            var original = GetStyleElementCssText(element);
            var rewritten = RewriteCssUrlsAgainstBaseHref(original, baseHref);
            if (!string.Equals(rewritten, original, StringComparison.Ordinal))
                SetElementTextContent(element, rewritten);
        }

        foreach (var child in ChildElements(element))
            RewriteStyleElementUrls(child, baseHref);
    }

    /// <summary>Rewrites each relative <c>url(...)</c> in <paramref name="css"/> to its
    /// resolution against <paramref name="baseHref"/>; absolute, <c>data:</c>, and fragment
    /// references are left byte-identical.</summary>
    private string RewriteCssUrlsAgainstBaseHref(string css, string baseHref)
        => UrlFunctionPattern.Replace(css, match =>
        {
            var resolved = ResolveUrlAgainstBaseHref(match.Groups[2].Value, baseHref);
            return resolved is null ? match.Value : $"url(\"{resolved}\")";
        });

    /// <summary>
    /// Resolves a CSS <c>url()</c> value against a <c>&lt;base href&gt;</c>, or returns
    /// <c>null</c> when it should be left untouched (empty / <c>data:</c> / fragment /
    /// already-absolute). A root-relative base yields a root-relative result (so the
    /// <c>wptRoot</c> host-relative mapper still resolves it); an absolute base yields an
    /// absolute URL; a document-relative base resolves through the page URL first.
    /// </summary>
    private string? ResolveUrlAgainstBaseHref(string rawUrl, string baseHref)
    {
        var url = rawUrl.Trim();
        if (url.Length == 0 ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("#", StringComparison.Ordinal) ||
            HasUrlScheme(url))
            return null;

        if (baseHref.StartsWith("/", StringComparison.Ordinal))
        {
            // Root-relative base: do the path math under a placeholder authority, then hand
            // back the host-relative path so a root-relative resource mapper still matches.
            var placeholder = new Uri("http://base.invalid", UriKind.Absolute);
            if (Uri.TryCreate(placeholder, baseHref, out var rootBase) &&
                Uri.TryCreate(rootBase, url, out var rootResolved))
            {
                var result = rootResolved.PathAndQuery + rootResolved.Fragment;
                return string.Equals(result, url, StringComparison.Ordinal) ? null : result;
            }

            return null;
        }

        if (Uri.TryCreate(baseHref, UriKind.Absolute, out var absoluteBase) &&
            Uri.TryCreate(absoluteBase, url, out var absoluteResolved))
        {
            var result = absoluteResolved.AbsoluteUri;
            return string.Equals(result, url, StringComparison.Ordinal) ? null : result;
        }

        if (!string.IsNullOrEmpty(_pageUrl) &&
            Uri.TryCreate(_pageUrl, UriKind.Absolute, out var pageBase) &&
            Uri.TryCreate(pageBase, baseHref, out var docBase) &&
            Uri.TryCreate(docBase, url, out var docResolved))
        {
            var result = docResolved.AbsoluteUri;
            return string.Equals(result, url, StringComparison.Ordinal) ? null : result;
        }

        return null;
    }
}
