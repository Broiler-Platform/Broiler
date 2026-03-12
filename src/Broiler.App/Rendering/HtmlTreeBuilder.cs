using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.App.Rendering;

/// <summary>
/// A simplified WHATWG-aligned HTML tree builder that converts a stream of
/// <see cref="HtmlToken"/> objects into a DOM tree of <see cref="DomElement"/> nodes.
/// </summary>
public sealed class HtmlTreeBuilder
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr"
    };

    private static readonly HashSet<string> StructuralTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "head", "body"
    };

    // Metadata elements that belong in <head> when encountered before an
    // explicit <body> tag (WHATWG "in head" insertion mode).
    private static readonly HashSet<string> HeadMetadataElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "style", "link", "meta", "base"
    };

    // Elements that auto-close a current <p>.
    private static readonly HashSet<string> PClosers = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "details", "dialog",
        "dd", "div", "dl", "dt", "fieldset", "figcaption", "figure",
        "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header",
        "hgroup", "hr", "li", "main", "nav", "ol", "p", "pre", "section",
        "table", "ul"
    };

    // Table-related elements that form the table scope boundary.
    private static readonly HashSet<string> TableElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "table", "thead", "tbody", "tfoot", "tr"
    };

    // Elements that are valid direct children of table-related contexts.
    private static readonly HashSet<string> TableChildElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "caption", "colgroup", "col", "thead", "tbody", "tfoot", "tr",
        "td", "th", "style", "script", "template"
    };

    // Formatting elements for the adoption agency algorithm.
    private static readonly HashSet<string> FormattingElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "b", "big", "code", "em", "font", "i", "nobr", "s",
        "small", "strike", "strong", "tt", "u"
    };

    /// <summary>
    /// Parses the supplied HTML string and returns the constructed DOM tree.
    /// </summary>
    /// <param name="html">Raw HTML markup.</param>
    /// <returns>
    /// A tuple containing the root <c>&lt;html&gt;</c> element, a flat list of all
    /// non-structural elements, and the extracted document title.
    /// </returns>
    public (DomElement DocumentElement, List<DomElement> AllElements, string Title) Build(string html)
    {
        var tokenizer = new HtmlTokenizer();
        var tokens = tokenizer.Tokenize(html);

        var root = new DomElement("html", null, null, string.Empty);
        var head = new DomElement("head", null, null, string.Empty);
        var body = new DomElement("body", null, null, string.Empty);
        AppendChild(root, head);
        AppendChild(root, body);

        var allElements = new List<DomElement>();
        var openElements = new Stack<DomElement>();
        openElements.Push(body);

        // Track whether an explicit <body> tag has been seen; metadata
        // elements encountered before <body> belong in <head>.
        var bodyOpened = false;

        // Active formatting elements list for the adoption agency algorithm
        var activeFormatting = new List<DomElement>();

        var title = string.Empty;
        var inTitle = false;

        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case TokenType.StartTag:
                {
                    var tag = token.Name;

                    if (string.Equals(tag, "html", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "head", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tag, "body", StringComparison.OrdinalIgnoreCase))
                    {
                        // Merge attributes from the token onto the pre-created element
                        var target = string.Equals(tag, "html", StringComparison.OrdinalIgnoreCase) ? root
                            : string.Equals(tag, "head", StringComparison.OrdinalIgnoreCase) ? head
                            : body;
                        if (string.Equals(tag, "body", StringComparison.OrdinalIgnoreCase))
                            bodyOpened = true;
                        if (token.Attributes != null)
                        {
                            foreach (var kvp in token.Attributes)
                            {
                                if (string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase))
                                    target.Id = kvp.Value;
                                else if (string.Equals(kvp.Key, "class", StringComparison.OrdinalIgnoreCase))
                                    target.ClassName = kvp.Value;
                                else
                                    target.Attributes[kvp.Key] = kvp.Value;
                            }
                        }
                        break;
                    }

                    if (string.Equals(tag, "title", StringComparison.OrdinalIgnoreCase))
                    {
                        inTitle = true;
                        // Create a <title> element and add to <head>
                        var titleEl = new DomElement("title", null, null, string.Empty);
                        AppendChild(head, titleEl);
                        allElements.Add(titleEl);
                        openElements.Push(titleEl);
                        break;
                    }

                    // Metadata elements (<style>, <link>, <meta>, <base>) that
                    // appear before an explicit <body> tag are placed in <head>
                    // per the WHATWG "in head" insertion mode.
                    if (!bodyOpened && HeadMetadataElements.Contains(tag))
                    {
                        var metaEl = CreateElement(token);
                        AppendChild(head, metaEl);
                        allElements.Add(metaEl);
                        if (!VoidElements.Contains(tag) && !token.SelfClosing)
                            openElements.Push(metaEl);
                        break;
                    }

                    AutoCloseCurrent(openElements, tag);

                    var element = CreateElement(token);
                    var parent = openElements.Count > 0 ? openElements.Peek() : body;

                    // Implicit <tbody>: per the HTML spec, when <tr> is encountered
                    // inside <table> without an explicit section element (thead/tbody/tfoot),
                    // an implied <tbody> is created to wrap the row.
                    if (string.Equals(tag, "tr", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(parent.TagName, "table", StringComparison.OrdinalIgnoreCase))
                    {
                        var implicitTbody = new DomElement("tbody", null, null, string.Empty);
                        AppendChild(parent, implicitTbody);
                        allElements.Add(implicitTbody);
                        openElements.Push(implicitTbody);
                        parent = implicitTbody;
                    }

                    // Foster parenting: text/elements inside table scope that
                    // are not valid table children get foster-parented.
                    if (TableElements.Contains(parent.TagName) &&
                        !TableChildElements.Contains(tag))
                    {
                        parent = FosterParent(openElements, body);
                    }

                    AppendChild(parent, element);
                    allElements.Add(element);

                    if (!VoidElements.Contains(tag) && !token.SelfClosing)
                    {
                        openElements.Push(element);

                        // Track formatting elements for adoption agency
                        if (FormattingElements.Contains(tag))
                            activeFormatting.Add(element);
                    }

                    break;
                }

                case TokenType.EndTag:
                {
                    var tag = token.Name;

                    if (string.Equals(tag, "title", StringComparison.OrdinalIgnoreCase))
                    {
                        inTitle = false;
                        // Pop the title element from the stack
                        if (openElements.Count > 0 &&
                            string.Equals(openElements.Peek().TagName, "title", StringComparison.OrdinalIgnoreCase))
                            openElements.Pop();
                        break;
                    }

                    if (StructuralTags.Contains(tag) || VoidElements.Contains(tag))
                        break;

                    // Adoption agency for formatting elements
                    if (FormattingElements.Contains(tag))
                    {
                        RunAdoptionAgency(openElements, activeFormatting, tag, allElements, body);
                        break;
                    }

                    PopToTag(openElements, tag);
                    break;
                }

                case TokenType.Character:
                {
                    if (string.IsNullOrEmpty(token.Data))
                        break;

                    if (inTitle)
                    {
                        title += token.Data;
                        // Also add a text node to the <title> element
                        var titleText = new DomElement("#text", null, null, string.Empty, isTextNode: true);
                        titleText.TextContent = token.Data;
                        var titleParent = openElements.Count > 0 ? openElements.Peek() : head;
                        AppendChild(titleParent, titleText);
                        allElements.Add(titleText);
                        break;
                    }

                    var text = new DomElement("#text", null, null, string.Empty,
                        isTextNode: true);
                    text.TextContent = token.Data;

                    var parent = openElements.Count > 0 ? openElements.Peek() : body;

                    // Foster parenting for text nodes inside table scope.
                    // Per HTML spec, only non-whitespace text is foster-parented;
                    // whitespace text nodes are kept in the table element.
                    if (TableElements.Contains(parent.TagName) &&
                        !string.IsNullOrWhiteSpace(text.TextContent))
                        parent = FosterParent(openElements, body);

                    AppendChild(parent, text);
                    allElements.Add(text);
                    break;
                }

                case TokenType.Comment:
                {
                    var commentNode = new DomElement("#comment", null, null, string.Empty, isTextNode: false);
                    commentNode.TextContent = token.Data ?? string.Empty;
                    var commentParent = openElements.Count > 0 ? openElements.Peek() : body;
                    AppendChild(commentParent, commentNode);
                    allElements.Add(commentNode);
                    break;
                }

                case TokenType.EndOfFile:
                    break;
            }
        }

        return (root, allElements, title.Trim());
    }

    /// <summary>
    /// Creates a <see cref="DomElement"/> from a start-tag token, extracting
    /// <c>id</c>, <c>class</c>, and inline <c>style</c> attributes.
    /// </summary>
    private static DomElement CreateElement(HtmlToken token)
    {
        string id = null;
        string className = null;
        Dictionary<string, string> style = null;
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (token.Attributes != null)
        {
            foreach (var kvp in token.Attributes)
            {
                attrs[kvp.Key] = kvp.Value;

                if (string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase))
                    id = kvp.Value;
                else if (string.Equals(kvp.Key, "class", StringComparison.OrdinalIgnoreCase))
                    className = kvp.Value;
                else if (string.Equals(kvp.Key, "style", StringComparison.OrdinalIgnoreCase))
                    style = ParseStyle(kvp.Value);
            }
        }

        return new DomElement(token.Name, id, className, string.Empty, style, attrs);
    }

    /// <summary>
    /// Parses a CSS inline style string into a property→value dictionary.
    /// </summary>
    private static Dictionary<string, string> ParseStyle(string styleValue)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(styleValue))
            return result;
        foreach (var declaration in styleValue.Split(';'))
        {
            var colonIdx = declaration.IndexOf(':');
            if (colonIdx > 0)
            {
                var prop = declaration[..colonIdx].Trim();
                var val = declaration[(colonIdx + 1)..].Trim();
                if (!string.IsNullOrEmpty(prop))
                    result[prop] = val;
            }
        }
        return result;
    }

    /// <summary>
    /// Auto-closes the current element when the incoming tag requires it
    /// (e.g. opening a <c>&lt;p&gt;</c> while already inside a <c>&lt;p&gt;</c>).
    /// </summary>
    private static void AutoCloseCurrent(Stack<DomElement> openElements, string incomingTag)
    {
        if (openElements.Count == 0)
            return;

        var current = openElements.Peek();

        if (string.Equals(current.TagName, "p", StringComparison.OrdinalIgnoreCase) &&
            PClosers.Contains(incomingTag))
        {
            openElements.Pop();
            return;
        }

        // Auto-close <li> when another <li> arrives.
        if (string.Equals(current.TagName, "li", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(incomingTag, "li", StringComparison.OrdinalIgnoreCase))
        {
            openElements.Pop();
            return;
        }

        // Auto-close <dd>/<dt> when a sibling arrives.
        if ((string.Equals(current.TagName, "dd", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(current.TagName, "dt", StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(incomingTag, "dd", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(incomingTag, "dt", StringComparison.OrdinalIgnoreCase)))
        {
            openElements.Pop();
            return;
        }

        // Auto-close <td>/<th> when another <td>, <th>, or <tr> arrives.
        if ((string.Equals(current.TagName, "td", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(current.TagName, "th", StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(incomingTag, "td", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(incomingTag, "th", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(incomingTag, "tr", StringComparison.OrdinalIgnoreCase)))
        {
            openElements.Pop();
            return;
        }

        // Auto-close <tr> when another <tr> arrives.
        if (string.Equals(current.TagName, "tr", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(incomingTag, "tr", StringComparison.OrdinalIgnoreCase))
        {
            openElements.Pop();
            return;
        }

        // Auto-close <thead>/<tbody>/<tfoot> when a sibling section arrives.
        if ((string.Equals(current.TagName, "thead", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(current.TagName, "tbody", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(current.TagName, "tfoot", StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(incomingTag, "thead", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(incomingTag, "tbody", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(incomingTag, "tfoot", StringComparison.OrdinalIgnoreCase)))
        {
            openElements.Pop();
            return;
        }

        // Auto-close <option> when another <option> or <optgroup> arrives.
        if (string.Equals(current.TagName, "option", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(incomingTag, "option", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(incomingTag, "optgroup", StringComparison.OrdinalIgnoreCase)))
        {
            openElements.Pop();
            return;
        }

        // Auto-close <optgroup> when another <optgroup> arrives.
        if (string.Equals(current.TagName, "optgroup", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(incomingTag, "optgroup", StringComparison.OrdinalIgnoreCase))
        {
            openElements.Pop();
        }
    }

    /// <summary>
    /// Pops the stack of open elements up to and including the element
    /// whose tag name matches <paramref name="tag"/>.
    /// </summary>
    private static void PopToTag(Stack<DomElement> openElements, string tag)
    {
        while (openElements.Count > 1)
        {
            var el = openElements.Pop();
            if (string.Equals(el.TagName, tag, StringComparison.OrdinalIgnoreCase))
                return;
        }
    }

    /// <summary>
    /// Returns the correct foster parent for an element that cannot be a
    /// direct child of a table-scope element. Per the WHATWG spec the
    /// foster parent is the parent of the nearest &lt;table&gt; ancestor,
    /// or the body element if no table is found.
    /// </summary>
    private static DomElement FosterParent(Stack<DomElement> openElements, DomElement body)
    {
        // Walk the stack to find the nearest table element
        foreach (var el in openElements)
        {
            if (string.Equals(el.TagName, "table", StringComparison.OrdinalIgnoreCase))
                return el.Parent ?? body;
        }
        return body;
    }

    /// <summary>
    /// Simplified adoption agency algorithm (WHATWG §13.2.6.4.7) for
    /// misnested formatting elements. When an end tag for a formatting
    /// element is encountered but there is an intervening non-formatting
    /// element, the algorithm reparents nodes to produce correct nesting.
    /// </summary>
    private static void RunAdoptionAgency(
        Stack<DomElement> openElements,
        List<DomElement> activeFormatting,
        string tag,
        List<DomElement> allElements,
        DomElement body)
    {
        // Step 1: If the current node matches the tag, just pop it
        if (openElements.Count > 0 &&
            string.Equals(openElements.Peek().TagName, tag, StringComparison.OrdinalIgnoreCase))
        {
            var popped = openElements.Pop();
            activeFormatting.Remove(popped);
            return;
        }

        // Step 2: Find the formatting element in the stack
        DomElement formattingEl = null;
        var stackList = openElements.ToList(); // top of stack = index 0
        int formattingIdx = -1;
        for (int i = 0; i < stackList.Count; i++)
        {
            if (string.Equals(stackList[i].TagName, tag, StringComparison.OrdinalIgnoreCase))
            {
                formattingEl = stackList[i];
                formattingIdx = i;
                break;
            }
        }

        if (formattingEl == null)
        {
            // No matching formatting element; just pop to tag as fallback
            PopToTag(openElements, tag);
            return;
        }

        // Step 3: Find the furthest block — the topmost non-formatting element
        // between the current node and the formatting element
        DomElement furthestBlock = null;
        int furthestBlockIdx = -1;
        for (int i = 0; i < formattingIdx; i++)
        {
            if (!FormattingElements.Contains(stackList[i].TagName) &&
                !VoidElements.Contains(stackList[i].TagName))
            {
                furthestBlock = stackList[i];
                furthestBlockIdx = i;
                break;
            }
        }

        if (furthestBlock == null)
        {
            // No intervening block; just pop up to and including the formatting element
            while (openElements.Count > 0)
            {
                var popped = openElements.Pop();
                activeFormatting.Remove(popped);
                if (popped == formattingEl)
                    break;
            }
            return;
        }

        // Step 4: No full reparenting needed in this simplified algorithm.
        // Just pop up to and including the formatting element to close it.
        while (openElements.Count > 0)
        {
            var popped = openElements.Pop();
            activeFormatting.Remove(popped);
            if (popped == formattingEl)
                break;
        }
    }

    private static void AppendChild(DomElement parent, DomElement child)
    {
        child.Parent = parent;
        parent.Children.Add(child);
    }
}
