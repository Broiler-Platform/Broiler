using System.Text.RegularExpressions;

namespace Broiler.Layout.IR;

/// <summary>
/// The element nesting <see cref="SvgRenderer"/>'s per-element-type regexes cannot see: which
/// start tags are inside a container that does not paint, and which presentation attributes each
/// one inherits from its ancestors.
/// </summary>
/// <remarks>
/// <para>
/// The renderer matches one regex per shape type over the whole markup, so every <c>&lt;rect&gt;</c>
/// in the document is drawn wherever it sits — including the ones inside <c>&lt;defs&gt;</c>,
/// <c>&lt;symbol&gt;</c> and <c>&lt;pattern&gt;</c>, which SVG 1.1 §5.5/§12.3/§13.3 render only
/// through a reference. That is what made <c>conformance-checkers/html-svg/struct-symbol-01-b</c>
/// paint a symbol's contents across the whole canvas, and what painted a pattern's tile once at the
/// origin. It equally cannot see that <c>&lt;g font-size="32"&gt;</c> sets the size for the
/// <c>&lt;text&gt;</c> inside it.
/// </para>
/// <para>
/// This scans the tags once with a stack and records, per start-tag offset, whether the element
/// paints and what it inherits. The offsets are the ones <see cref="Regex"/> reports as
/// <see cref="Capture.Index"/> for the same element, so the existing loops stay as they are and
/// only consult this by index. Anything the scan does not reach — a commented-out element, markup
/// inside <c>&lt;style&gt;</c> — is absent, and absent means "does not paint", which is the point.
/// </para>
/// </remarks>
internal sealed partial class SvgStructure
{
    /// <summary>
    /// Containers whose children are rendered only when something references them (SVG 1.1 §5.5),
    /// plus the elements whose content is not graphics at all. A start tag inside one of these does
    /// not paint where it stands.
    /// </summary>
    private static readonly HashSet<string> NonRenderingContainers = new(StringComparer.OrdinalIgnoreCase)
    {
        "defs", "symbol", "pattern", "mask", "marker", "clipPath", "filter",
        "linearGradient", "radialGradient", "style", "script", "title", "desc", "metadata",
        "font", "font-face", "font-face-src", "font-face-uri", "glyph", "missing-glyph",
        "altGlyphDef", "view", "hatch", "animate", "animateMotion", "animateTransform", "set",
    };

    /// <summary>
    /// The presentation attributes SVG 1.1 §6.4 marks inheritable that this renderer models. Paint
    /// and font properties only: the geometric attributes (<c>x</c>, <c>width</c>, …) are element
    /// properties and never inherit.
    /// </summary>
    private static readonly string[] InheritableProperties =
    [
        "fill", "fill-opacity", "fill-rule",
        "stroke", "stroke-opacity", "stroke-width", "stroke-linecap", "stroke-linejoin",
        "stroke-dasharray", "stroke-dashoffset", "stroke-miterlimit",
        "color", "visibility", "text-anchor", "text-decoration", "direction", "writing-mode",
        "font-family", "font-size", "font-style", "font-weight", "font-variant", "font-stretch",
        "letter-spacing", "word-spacing",
    ];

    /// <summary>Start-tag offset → attributes inherited from that element's ancestors.</summary>
    private readonly Dictionary<int, IReadOnlyDictionary<string, string>> _rendered = [];

    /// <summary>
    /// Start-tag offset → the user-space transform in effect for that element: every ancestor's
    /// <c>transform</c> composed outermost-first, with the element's own applied last.
    /// </summary>
    private readonly Dictionary<int, SvgTransform> _transforms = [];

    /// <summary>
    /// Start-tag offset → the element's own attributes, for every element the scan reached,
    /// painting or not.
    /// </summary>
    private readonly Dictionary<int, IReadOnlyDictionary<string, string>> _attributes = [];

    /// <summary><c>id</c> → the element that declares it, for <c>&lt;use href="#id"&gt;</c>.</summary>
    private readonly Dictionary<string, Element> _byId = new(StringComparer.Ordinal);

    /// <summary>One element the scan reached, addressable by <c>id</c>.</summary>
    /// <param name="Name">Its tag name.</param>
    /// <param name="Attributes">Its own attributes.</param>
    /// <param name="Content">Its content markup, empty when it is self-closing.</param>
    public readonly record struct Element(
        string Name, IReadOnlyDictionary<string, string> Attributes, string Content);

    private SvgStructure() { }

    /// <summary>An empty scan: nothing paints. Used when the markup is empty.</summary>
    public static SvgStructure Empty { get; } = new();

    /// <summary>
    /// Whether the element starting at <paramref name="index"/> paints where it stands, and if so
    /// what it inherits.
    /// </summary>
    public bool TryGetRendered(int index, out IReadOnlyDictionary<string, string> inherited)
        => _rendered.TryGetValue(index, out inherited!);

    /// <summary>
    /// The user-space transform the element starting at <paramref name="index"/> is drawn under,
    /// or the identity when it is under none.
    /// </summary>
    public SvgTransform TransformAt(int index)
        => _transforms.TryGetValue(index, out var transform) ? transform : SvgTransform.Identity;

    /// <summary>The attributes of the element starting at <paramref name="index"/>, painting or not.</summary>
    public bool TryGetAttributes(int index, out IReadOnlyDictionary<string, string> attributes)
        => _attributes.TryGetValue(index, out attributes!);

    /// <summary>Resolves an <c>id</c> to the element that declares it.</summary>
    public bool TryGetById(string id, out Element element)
    {
        element = default;
        return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out element);
    }

    /// <summary>
    /// Walks every start and end tag, tracking the inherited presentation attributes and the depth
    /// of non-rendering containers.
    /// </summary>
    /// <remarks>
    /// End tags are matched by name rather than trusted blindly: SVG in an HTML document reaches
    /// here through the box tree, which is well-formed, but an SVG loaded as an image is raw bytes.
    /// An unmatched end tag pops nothing rather than unbalancing the stack for the rest of the file.
    /// </remarks>
    public static SvgStructure Scan(string svgXml)
    {
        if (string.IsNullOrEmpty(svgXml))
            return Empty;

        var structure = new SvgStructure();
        var open = new List<(string Name, IReadOnlyDictionary<string, string> Inherited,
            bool Suppresses, SvgTransform Transform, IReadOnlyDictionary<string, string> Attributes,
            int ContentStart)>();
        int nonRenderingDepth = 0;

        foreach (Match m in TagRegex().Matches(svgXml))
        {
            // A comment, CDATA section, processing instruction or doctype: not an element, and the
            // markup inside it is text. Matching them here is what keeps a commented-out
            // <g id="draft-watermark"> — which most of the SVG 1.1 test suite carries — unpainted.
            if (!m.Groups["name"].Success)
                continue;

            string name = m.Groups["name"].Value;
            bool isEnd = m.Groups["close"].Value.Length > 0;

            if (isEnd)
            {
                int last = open.FindLastIndex(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (last < 0)
                    continue;

                for (int i = open.Count - 1; i >= last; i--)
                {
                    if (open[i].Suppresses)
                        nonRenderingDepth--;
                }

                // The element is complete, so its content span is known: record it under its id.
                var closed = open[last];
                if (closed.Attributes.TryGetValue("id", out var closedId) && closedId.Length > 0)
                {
                    structure._byId[closedId] = new Element(
                        closed.Name, closed.Attributes,
                        svgXml[closed.ContentStart..m.Index]);
                }

                open.RemoveRange(last, open.Count - last);
                continue;
            }

            var inherited = open.Count > 0 ? open[^1].Inherited : EmptyAttributes;
            var ancestorTransform = open.Count > 0 ? open[^1].Transform : SvgTransform.Identity;
            var attributes = ParseAttributes(m.Groups["attrs"].Value);
            structure._attributes[m.Index] = attributes;

            // SVG 1.1 §7.4: an element's transform applies to it as well as to its children, so the
            // element itself is recorded under the composition that includes its own.
            var transform = ancestorTransform.Concat(
                SvgTransform.Parse(attributes.GetValueOrDefault("transform")));

            bool paints = nonRenderingDepth == 0 && !NonRenderingContainers.Contains(name)
                && !IsDisplayNone(attributes);
            if (paints)
            {
                structure._rendered[m.Index] = inherited;
                if (!transform.IsIdentity)
                    structure._transforms[m.Index] = transform;
            }

            if (m.Groups["empty"].Value.Length > 0)
            {
                if (attributes.TryGetValue("id", out var emptyId) && emptyId.Length > 0)
                    structure._byId[emptyId] = new Element(name, attributes, string.Empty);
                continue;
            }

            // Track the suppression on the open element rather than re-deriving it at the end tag:
            // an element nested inside a container that does not paint suppresses its own children
            // too, and matching that against the container list alone would decrement fewer times
            // than it incremented, leaving the rest of the document permanently suppressed.
            bool suppresses = !paints;
            if (suppresses)
                nonRenderingDepth++;

            open.Add((
                name, Extend(inherited, attributes), suppresses, transform,
                attributes, m.Index + m.Length));
        }

        return structure;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The inherited set an element's children see: its parent's, overridden by whichever
    /// inheritable properties this element declares. Returns the parent's own map unchanged when
    /// the element declares none, so a deep tree of plain <c>&lt;g&gt;</c>s allocates nothing.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Extend(
        IReadOnlyDictionary<string, string> inherited, IReadOnlyDictionary<string, string> attributes)
    {
        Dictionary<string, string>? extended = null;
        foreach (var property in InheritableProperties)
        {
            string? value = ReadPresentation(attributes, property);
            if (value == null || (inherited.TryGetValue(property, out var existing) && existing == value))
                continue;

            extended ??= new Dictionary<string, string>(inherited, StringComparer.OrdinalIgnoreCase);
            extended[property] = value;
        }

        return extended ?? inherited;
    }

    /// <summary>
    /// A property from the element's <c>style</c> declaration if it is there, and from the
    /// presentation attribute of the same name otherwise — the order SVG 1.1 §6.4 gives them.
    /// </summary>
    private static string? ReadPresentation(IReadOnlyDictionary<string, string> attributes, string property)
    {
        if (attributes.TryGetValue("style", out var style) && !string.IsNullOrWhiteSpace(style))
        {
            foreach (var declaration in style.Split(';'))
            {
                int colon = declaration.IndexOf(':');
                if (colon > 0
                    && declaration.AsSpan(0, colon).Trim().Equals(property, StringComparison.OrdinalIgnoreCase))
                {
                    var value = declaration.AsSpan(colon + 1).Trim();
                    if (!value.IsEmpty)
                        return value.ToString();
                }
            }
        }

        return attributes.TryGetValue(property, out var attribute) && !string.IsNullOrWhiteSpace(attribute)
            ? attribute
            : null;
    }

    private static bool IsDisplayNone(IReadOnlyDictionary<string, string> attributes)
    {
        string? display = ReadPresentation(attributes, "display");
        return display != null && display.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ParseAttributes(string attrStr)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in AttributeRegex().Matches(attrStr))
        {
            var quoted = m.Groups["dq"];
            dict[m.Groups["name"].Value] = quoted.Success ? quoted.Value : m.Groups["sq"].Value;
        }
        return dict;
    }

    /// <summary>
    /// One markup construct: a comment/CDATA/PI/doctype (no <c>name</c> group, so it is skipped),
    /// or a start or end tag. Attribute values are matched as quoted runs so a <c>&gt;</c> inside
    /// one does not end the tag.
    /// </summary>
    [GeneratedRegex(
        """<!--.*?-->|<!\[CDATA\[.*?\]\]>|<\?.*?\?>|<!DOCTYPE(?:[^>"']|"[^"]*"|'[^']*')*>|<(?<close>/?)(?<name>[A-Za-z_][\w:.\-]*)(?<attrs>(?:[^>"']|"[^"]*"|'[^']*')*?)(?<empty>/?)>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex("""(?<name>[\w:.\-]+)\s*=\s*(?:"(?<dq>[^"]*)"|'(?<sq>[^']*)')""")]
    private static partial Regex AttributeRegex();
}
