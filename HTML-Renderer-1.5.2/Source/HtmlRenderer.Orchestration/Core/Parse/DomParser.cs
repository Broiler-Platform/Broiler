using System.Drawing;
using System;
using System.Collections.Generic;
using TheArtOfDev.HtmlRenderer.Core.Dom;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.Core.IR;
using TheArtOfDev.HtmlRenderer.Core.Utils;

namespace TheArtOfDev.HtmlRenderer.Core.Parse;

internal sealed class DomParser
{
    private readonly CssParser _cssParser;
    private readonly IStylesheetLoader _stylesheetLoader;

    public DomParser(CssParser cssParser, IStylesheetLoader stylesheetLoader)
    {
        ArgumentNullException.ThrowIfNull(cssParser);
        ArgumentNullException.ThrowIfNull(stylesheetLoader);

        _cssParser = cssParser;
        _stylesheetLoader = stylesheetLoader;
    }

    public CssBox GenerateCssTree(string html, HtmlContainerInt htmlContainer, ref CssData cssData)
    {
        var root = HtmlParser.ParseDocument(html);
        if (root == null)
            return root;

        root.ContainerInt = htmlContainer;

        bool cssDataChanged = false;
        CascadeParseStyles(root, htmlContainer, ref cssData, ref cssDataChanged);
        CascadeApplyStyles(root, cssData);
        SetTextSelectionStyle(htmlContainer, cssData);
        CorrectTextBoxes(root);
        CorrectImgBoxes(root);
        CorrectObjectBoxes(root);

        bool followingBlock = true;
        CorrectLineBreaksBlocks(root, ref followingBlock);
        CorrectInlineBoxesParent(root);
        CorrectBlockInsideInline(root);
        CorrectInlineBoxesParent(root);

        return root;
    }

    private void CascadeParseStyles(CssBox box, HtmlContainerInt htmlContainer, ref CssData cssData, ref bool cssDataChanged)
    {
        if (box.HtmlTag != null)
        {
            // Check for the <link rel=stylesheet> tag
            // Per CSS2.1 §6.4.1, the rel attribute is a space-separated list;
            // match if any token equals "stylesheet" (e.g. rel="appendix stylesheet").
            if (box.HtmlTag.Name.Equals("link", StringComparison.CurrentCultureIgnoreCase) &&
                ContainsStylesheetRel(box.GetAttribute("rel", string.Empty)))
            {
                CloneCssData(ref cssData, ref cssDataChanged);
                _stylesheetLoader.LoadStylesheet(box.GetAttribute("href", string.Empty), box.HtmlTag.Attributes, out string stylesheet, out CssData stylesheetData);
                if (stylesheet != null)
                    _cssParser.ParseStyleSheet(cssData, stylesheet);
                else if (stylesheetData != null)
                    cssData.Combine(stylesheetData);
            }

            // Check for the <style> tag
            if (box.HtmlTag.Name.Equals("style", StringComparison.CurrentCultureIgnoreCase) && box.Boxes.Count > 0)
            {
                CloneCssData(ref cssData, ref cssDataChanged);
                foreach (var child in box.Boxes)
                    _cssParser.ParseStyleSheet(cssData, child.Text.ToString());
            }
        }

        foreach (var childBox in box.Boxes)
            CascadeParseStyles(childBox, htmlContainer, ref cssData, ref cssDataChanged);
    }


    private void CascadeApplyStyles(CssBox box, CssData cssData)
    {
        box.InheritStyle();

        if (box.HtmlTag != null)
        {
            // CSS2.1 §6.4.3 specificity: Apply rules in increasing specificity.
            // Bare '*' = (0,0,0), tag = (0,0,1), '.class *' = (0,1,0), etc.
            // Universal rules with ancestor/sibling selectors (e.g. '.intro *')
            // have higher specificity than bare tag rules, so apply them after.
            AssignCssBlocks(box, cssData, "*", qualifiedOnly: false);
            AssignCssBlocks(box, cssData, box.HtmlTag.Name);
            AssignCssBlocks(box, cssData, "*", qualifiedOnly: true);

            if (box.HtmlTag.HasAttribute("class"))
                AssignClassCssBlocks(box, cssData);

            if (box.HtmlTag.HasAttribute("id"))
            {
                var id = box.HtmlTag.TryGetAttribute("id");
                AssignCssBlocks(box, cssData, "#" + id);
            }

            TranslateAttributes(box.HtmlTag, box);

            if (box.HtmlTag.HasAttribute("style"))
            {
                var block = _cssParser.ParseCssBlock(box.HtmlTag.Name, box.HtmlTag.TryGetAttribute("style"));
                if (block != null)
                    AssignCssBlock(box, block);
            }

            // Phase 2: Populate BoxKind and DOM-attribute properties on the box
            // so layout code can use these instead of accessing HtmlTag directly.
            AssignBoxKindAndAttributes(box);
        }

        // CSS2.1 §9.7: Relationships between 'display', 'position', and 'float'.
        // When 'float' is not 'none', the computed value of 'display' is adjusted
        // so that inline-level elements become block-level.  This must happen
        // after all CSS properties are resolved (including 'inherit') and before
        // child style cascading so children see the correct parent display value.
        if (box.Display != CssConstants.None && box.Float != CssConstants.None)
        {
            if (box.Display == CssConstants.Inline || box.Display == CssConstants.InlineBlock)
                box.Display = CssConstants.Block;
        }

        if (box.TextDecoration != String.Empty && box.Text.IsEmpty)
        {
            foreach (var childBox in box.Boxes)
                childBox.TextDecoration = box.TextDecoration;

            box.TextDecoration = string.Empty;
        }

        foreach (var childBox in box.Boxes)
            CascadeApplyStyles(childBox, cssData);

        // CSS2.1 §12.1: Generate ::before and ::after pseudo-element boxes
        // after child style cascading to avoid modifying the child list
        // during iteration.
        if (box.HtmlTag != null)
            ApplyPseudoElementBoxes(box, cssData);
    }

    private void SetTextSelectionStyle(HtmlContainerInt htmlContainer, CssData cssData)
    {
        htmlContainer.SelectionForeColor = Color.Empty;
        htmlContainer.SelectionBackColor = Color.Empty;

        if (!cssData.ContainsCssBlock("::selection"))
            return;

        var blocks = cssData.GetCssBlock("::selection");
        foreach (var block in blocks)
        {
            if (block.Properties.TryGetValue("color", out string value))
                htmlContainer.SelectionForeColor = _cssParser.ParseColor(value);

            if (block.Properties.TryGetValue("background-color", out string value1))
                htmlContainer.SelectionBackColor = _cssParser.ParseColor(value1);
        }
    }

    private static void AssignClassCssBlocks(CssBox box, CssData cssData)
    {
        var classes = box.HtmlTag.TryGetAttribute("class");
        var classList = new List<string>();
        var startIdx = 0;

        while (startIdx < classes.Length)
        {
            while (startIdx < classes.Length && classes[startIdx] == ' ')
                startIdx++;

            if (startIdx >= classes.Length)
                continue;

            var endIdx = classes.IndexOf(' ', startIdx);

            if (endIdx < 0)
                endIdx = classes.Length;

            var cls = "." + classes.Substring(startIdx, endIdx - startIdx);
            classList.Add(cls);
            AssignCssBlocks(box, cssData, cls);
            AssignCssBlocks(box, cssData, box.HtmlTag.Name + cls);

            startIdx = endIdx + 1;
        }

        // CSS2.1 §5.8.3: compound class selectors like .first.one must
        // match elements that have ALL specified classes.  Generate lookup
        // keys for all 2-class combinations so that rules stored under
        // compound keys are found.
        if (classList.Count >= 2)
        {
            for (int i = 0; i < classList.Count; i++)
            {
                for (int j = 0; j < classList.Count; j++)
                {
                    if (i == j) continue;
                    var compound = classList[i] + classList[j];
                    AssignCssBlocks(box, cssData, compound);
                    AssignCssBlocks(box, cssData, box.HtmlTag.Name + compound);
                }
            }
        }
    }

    private static void AssignCssBlocks(CssBox box, CssData cssData, string className, bool? qualifiedOnly = null)
    {
        var blocks = cssData.GetCssBlock(className);
        foreach (var block in blocks)
        {
            // When qualifiedOnly is specified, filter by whether the block has
            // ancestor/sibling selectors (which increase specificity).
            if (qualifiedOnly.HasValue)
            {
                bool hasSelectors = block.Selectors != null;
                if (qualifiedOnly.Value != hasSelectors)
                    continue;
            }

            if (IsBlockAssignableToBox(box, block))
                AssignCssBlock(box, block);
        }
    }

    private static bool IsBlockAssignableToBox(CssBox box, CssBlock block)
    {
        bool assignable = true;
        if (block.Selectors != null)
        {
            assignable = IsBlockAssignableToBoxWithSelector(box, block);
        }
        else if (box.HtmlTag.Name.Equals("a", StringComparison.OrdinalIgnoreCase) && block.Class.Equals("a", StringComparison.OrdinalIgnoreCase) && !box.HtmlTag.HasAttribute("href"))
        {
            assignable = false;
        }

        if (assignable && block.Hover)
        {
            box.ContainerInt.AddHoverBox(box, block);
            assignable = false;
        }

        return assignable;
    }

    private static bool IsBlockAssignableToBoxWithSelector(CssBox box, CssBlock block)
    {
        foreach (var selector in block.Selectors)
        {
            if (selector.AdjacentSibling)
            {
                // Adjacent sibling combinator: the immediately preceding element
                // sibling of the current box must match the selector.
                box = GetPreviousElementSibling(box);
                if (box == null)
                    return false;

                if (!MatchesSelectorItem(box, selector.Class))
                    return false;
            }
            else
            {
                bool matched = false;
                while (!matched)
                {
                    box = box.ParentBox;
                    while (box != null && box.HtmlTag == null)
                        box = box.ParentBox;

                    if (box == null)
                        return false;

                    matched = MatchesSelectorItem(box, selector.Class);

                    if (!matched && selector.DirectParent)
                        return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="box"/> matches
    /// the given CSS selector item (tag name, .class, #id, or compound .c1.c2).
    /// </summary>
    private static bool MatchesSelectorItem(CssBox box, string selectorClass)
    {
        if (box.HtmlTag == null)
            return false;

        // CSS2.1 §5.3: The universal selector '*' matches any element type.
        if (selectorClass == "*")
            return true;

        if (box.HtmlTag.Name.Equals(selectorClass, StringComparison.InvariantCultureIgnoreCase))
            return true;

        if (box.HtmlTag.HasAttribute("class"))
        {
            var className = box.HtmlTag.TryGetAttribute("class");

            // Single class match: ".foo" matches class="foo"
            if (selectorClass.Equals("." + className, StringComparison.InvariantCultureIgnoreCase)
                || selectorClass.Equals(box.HtmlTag.Name + "." + className, StringComparison.InvariantCultureIgnoreCase))
                return true;

            // Compound class match: ".foo.bar" matches class="foo bar" or "bar foo"
            if (selectorClass.StartsWith(".") && selectorClass.IndexOf('.', 1) > 0)
            {
                var parts = selectorClass.Split('.');
                var classWords = (" " + className + " ").ToLower();
                bool allMatch = true;
                for (int i = 1; i < parts.Length; i++) // skip first empty part from leading "."
                {
                    if (string.IsNullOrEmpty(parts[i])) continue;
                    if (!classWords.Contains(" " + parts[i] + " "))
                    {
                        allMatch = false;
                        break;
                    }
                }
                if (allMatch) return true;
            }
        }

        if (box.HtmlTag.HasAttribute("id"))
        {
            var id = box.HtmlTag.TryGetAttribute("id");
            if (selectorClass.Equals("#" + id, StringComparison.InvariantCultureIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the immediately preceding element sibling (a box with a
    /// non-null <see cref="CssBox.HtmlTag"/>) in the document tree,
    /// or <c>null</c> if there is none. Text-only and anonymous boxes
    /// are skipped.
    /// </summary>
    private static CssBox GetPreviousElementSibling(CssBox box)
    {
        if (box.ParentBox == null)
            return null;

        int index = box.ParentBox.Boxes.IndexOf(box);
        for (int i = index - 1; i >= 0; i--)
        {
            var sib = box.ParentBox.Boxes[i];
            if (sib.HtmlTag != null)
                return sib;
        }
        return null;
    }

    private static void AssignCssBlock(CssBox box, CssBlock block)
    {
        foreach (var prop in block.Properties)
        {
            var value = prop.Value;

            if (prop.Value == CssConstants.Inherit && box.ParentBox != null)
                value = CssUtils.GetPropertyValue(box.ParentBox, prop.Key);

            if (IsStyleOnElementAllowed(box, prop.Key, value))
                CssUtils.SetPropertyValue(box, prop.Key, value);
        }
    }

    // ── CSS2.1 §12.1: ::before / ::after pseudo-element generation ──

    /// <summary>
    /// Creates <c>::before</c> and <c>::after</c> pseudo-element child
    /// boxes when the CSS data contains matching pseudo-element blocks.
    /// </summary>
    private static void ApplyPseudoElementBoxes(CssBox box, CssData cssData)
    {
        var beforeBlock = FindPseudoElementBlock(box, cssData, "::before");
        if (beforeBlock != null)
            CreatePseudoElementBox(box, beforeBlock, isBefore: true);

        var afterBlock = FindPseudoElementBlock(box, cssData, "::after");
        if (afterBlock != null)
            CreatePseudoElementBox(box, afterBlock, isBefore: false);
    }

    /// <summary>
    /// Searches <paramref name="cssData"/> for a pseudo-element block
    /// matching <paramref name="box"/> and <paramref name="pseudoElement"/>
    /// (e.g. <c>"::before"</c>).
    /// </summary>
    private static CssBlock FindPseudoElementBlock(CssBox box, CssData cssData, string pseudoElement)
    {
        // Element-level: e.g. "p::before"
        var found = MatchPseudoBlock(box, cssData, box.HtmlTag.Name + pseudoElement);
        if (found != null) return found;

        // Class-level: e.g. ".nose::before", "p.nose::before"
        if (box.HtmlTag.HasAttribute("class"))
        {
            var classes = box.HtmlTag.TryGetAttribute("class");
            var startIdx = 0;

            while (startIdx < classes.Length)
            {
                while (startIdx < classes.Length && classes[startIdx] == ' ')
                    startIdx++;
                if (startIdx >= classes.Length) break;

                var endIdx = classes.IndexOf(' ', startIdx);
                if (endIdx < 0) endIdx = classes.Length;

                var cls = classes.Substring(startIdx, endIdx - startIdx);

                found = MatchPseudoBlock(box, cssData, "." + cls + pseudoElement);
                if (found != null) return found;

                found = MatchPseudoBlock(box, cssData, box.HtmlTag.Name + "." + cls + pseudoElement);
                if (found != null) return found;

                startIdx = endIdx + 1;
            }
        }

        // ID-level: e.g. "#myid::before"
        if (box.HtmlTag.HasAttribute("id"))
        {
            var id = box.HtmlTag.TryGetAttribute("id");
            found = MatchPseudoBlock(box, cssData, "#" + id + pseudoElement);
            if (found != null) return found;
        }

        // Universal: "*::before"
        found = MatchPseudoBlock(box, cssData, "*" + pseudoElement);
        return found;
    }

    private static CssBlock MatchPseudoBlock(CssBox box, CssData cssData, string key)
    {
        foreach (var block in cssData.GetCssBlock(key))
        {
            if (block.Selectors == null || IsBlockAssignableToBoxWithSelector(box, block))
                return block;
        }
        return null;
    }

    /// <summary>
    /// Creates a pseudo-element <see cref="CssBox"/> as a child of
    /// <paramref name="parentBox"/> with styles from <paramref name="block"/>.
    /// For <c>::before</c>, the box is inserted as the first child;
    /// for <c>::after</c>, it is appended as the last child.
    /// </summary>
    private static void CreatePseudoElementBox(CssBox parentBox, CssBlock block, bool isBefore)
    {
        // Determine content value — skip generation for "none" and "normal".
        string contentValue = null;
        if (block.Properties.TryGetValue("content", out string cv))
            contentValue = cv;

        if (contentValue == null || contentValue == "none" || contentValue == "normal")
            return;

        // Create the pseudo-element box and inherit from parent.
        CssBox pseudoBox;
        if (isBefore && parentBox.Boxes.Count > 0)
        {
            var firstChild = parentBox.Boxes[0];
            pseudoBox = CssBoxHelper.CreateBox(parentBox, before: firstChild);
        }
        else
        {
            pseudoBox = CssBoxHelper.CreateBox(parentBox);
        }

        // Apply pseudo-element CSS declarations.
        foreach (var prop in block.Properties)
        {
            var value = prop.Value;
            if (value == CssConstants.Inherit)
                value = CssUtils.GetPropertyValue(parentBox, prop.Key);
            CssUtils.SetPropertyValue(pseudoBox, prop.Key, value);
        }

        // Set text content (strip surrounding quotes from CSS content value).
        var text = contentValue.Trim('\'', '"');
        if (text.Length > 0)
            pseudoBox.Text = text.AsMemory();
    }

    private static bool IsStyleOnElementAllowed(CssBox box, string key, string value)
    {
        if (box.HtmlTag == null || key != HtmlConstants.Display)
            return true;

        return box.HtmlTag.Name switch
        {
            HtmlConstants.Table => value == CssConstants.Table,
            HtmlConstants.Tr => value == CssConstants.TableRow,
            HtmlConstants.Tbody => value == CssConstants.TableRowGroup,
            HtmlConstants.Thead => value == CssConstants.TableHeaderGroup,
            HtmlConstants.Tfoot => value == CssConstants.TableFooterGroup,
            HtmlConstants.Col => value == CssConstants.TableColumn,
            HtmlConstants.Colgroup => value == CssConstants.TableColumnGroup,
            HtmlConstants.Td or HtmlConstants.Th => value == CssConstants.TableCell,
            HtmlConstants.Caption => value == CssConstants.TableCaption,
            _ => true,
        };
    }

    private static void CloneCssData(ref CssData cssData, ref bool cssDataChanged)
    {
        if (cssDataChanged)
            return;

        cssDataChanged = true;
        cssData = cssData.Clone();
    }

    /// <summary>
    /// Returns <c>true</c> when the space-separated <c>rel</c> attribute value
    /// contains the token <c>stylesheet</c> (case-insensitive).
    /// This allows <c>&lt;link rel="appendix stylesheet"&gt;</c> to be recognised
    /// as a stylesheet link, as required by CSS2.1 §6.4.1 and the Acid2 test.
    /// </summary>
    private static bool ContainsStylesheetRel(string relValue)
    {
        if (string.IsNullOrEmpty(relValue))
            return false;

        foreach (var token in relValue.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("stylesheet", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Phase 2: Sets <see cref="CssBoxProperties.Kind"/>, list attributes
    /// (<see cref="CssBoxProperties.ListStart"/>, <see cref="CssBoxProperties.ListReversed"/>),
    /// and <see cref="CssBoxProperties.ImageSource"/> based on the HTML tag.
    /// This allows layout code to consume these properties instead of reading
    /// <see cref="HtmlTag"/> attributes directly.
    /// </summary>
    private static void AssignBoxKindAndAttributes(CssBox box)
    {
        var tag = box.HtmlTag;
        if (tag == null)
            return;

        box.Kind = tag.Name.ToLowerInvariant() switch
        {
            HtmlConstants.Img => BoxKind.ReplacedImage,
            HtmlConstants.Iframe => BoxKind.ReplacedIframe,
            HtmlConstants.Table => BoxKind.Table,
            HtmlConstants.Tr => BoxKind.TableRow,
            HtmlConstants.Td or HtmlConstants.Th => BoxKind.TableCell,
            HtmlConstants.Li => BoxKind.ListItem,
            HtmlConstants.Ol => BoxKind.OrderedList,
            HtmlConstants.Ul => BoxKind.UnorderedList,
            HtmlConstants.Hr => BoxKind.HorizontalRule,
            HtmlConstants.Br => BoxKind.LineBreak,
            HtmlConstants.A => BoxKind.Anchor,
            HtmlConstants.Font => BoxKind.Font,
            HtmlConstants.Input => BoxKind.Input,
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => BoxKind.Heading,
            "object" when box is CssBoxImage => BoxKind.ReplacedImage,
            _ => BoxKind.Anonymous,
        };

        // Populate list attributes for <ol> elements
        if (box.Kind == BoxKind.OrderedList)
        {
            box.ListReversed = tag.HasAttribute("reversed");
            if (int.TryParse(tag.TryGetAttribute("start"), out int start))
                box.ListStart = start;
        }

        // Populate image source for <img> and <object> image elements
        if (box.Kind == BoxKind.ReplacedImage)
            box.ImageSource = tag.TryGetAttribute("src") ?? tag.TryGetAttribute("data");
    }

    private void TranslateAttributes(HtmlTag tag, CssBox box)
    {
        if (!tag.HasAttributes())
            return;

        foreach (string att in tag.Attributes.Keys)
        {
            string value = tag.Attributes[att];

            switch (att)
            {
                case HtmlConstants.Align:
                    if (value == HtmlConstants.Left || value == HtmlConstants.Center || value == HtmlConstants.Right || value == HtmlConstants.Justify)
                        box.TextAlign = value.ToLower();
                    else
                        box.VerticalAlign = value.ToLower();
                    break;
                case HtmlConstants.Background:
                    box.BackgroundImage = value.ToLower();
                    break;
                case HtmlConstants.Bgcolor:
                    box.BackgroundColor = value.ToLower();
                    break;
                case HtmlConstants.Border:
                    if (!string.IsNullOrEmpty(value) && value != "0")
                        box.BorderLeftStyle = box.BorderTopStyle = box.BorderRightStyle = box.BorderBottomStyle = CssConstants.Solid;
                    box.BorderLeftWidth = box.BorderTopWidth = box.BorderRightWidth = box.BorderBottomWidth = TranslateLength(value);

                    if (tag.Name == HtmlConstants.Table)
                    {
                        if (value != "0")
                            ApplyTableBorder(box, "1px");
                    }
                    else
                    {
                        box.BorderTopStyle = box.BorderLeftStyle = box.BorderRightStyle = box.BorderBottomStyle = CssConstants.Solid;
                    }
                    break;
                case HtmlConstants.Bordercolor:
                    box.BorderLeftColor = box.BorderTopColor = box.BorderRightColor = box.BorderBottomColor = value.ToLower();
                    break;
                case HtmlConstants.Cellspacing:
                    box.BorderSpacing = TranslateLength(value);
                    break;
                case HtmlConstants.Cellpadding:
                    ApplyTablePadding(box, value);
                    break;
                case HtmlConstants.Color:
                    box.Color = value.ToLower();
                    break;
                case HtmlConstants.Dir:
                    box.Direction = value.ToLower();
                    break;
                case HtmlConstants.Face:
                    box.FontFamily = _cssParser.ParseFontFamily(value);
                    break;
                case HtmlConstants.Height:
                    box.Height = TranslateLength(value);
                    break;
                case HtmlConstants.Hspace:
                    box.MarginRight = box.MarginLeft = TranslateLength(value);
                    break;
                case HtmlConstants.Nowrap:
                    box.WhiteSpace = CssConstants.NoWrap;
                    break;
                case HtmlConstants.Size:
                    if (tag.Name.Equals(HtmlConstants.Hr, StringComparison.OrdinalIgnoreCase))
                        box.Height = TranslateLength(value);
                    else if (tag.Name.Equals(HtmlConstants.Font, StringComparison.OrdinalIgnoreCase))
                        box.FontSize = value;
                    break;
                case HtmlConstants.Valign:
                    box.VerticalAlign = value.ToLower();
                    break;
                case HtmlConstants.Vspace:
                    box.MarginTop = box.MarginBottom = TranslateLength(value);
                    break;
                case HtmlConstants.Width:
                    box.Width = TranslateLength(value);
                    break;
            }
        }
    }

    private static string TranslateLength(string htmlLength)
    {
        CssLength len = new(htmlLength);

        if (len.HasError)
            return $"{htmlLength}px";

        return htmlLength;
    }

    private static void ApplyTableBorder(CssBox table, string border) => SetForAllCells(table, cell =>
    {
        cell.BorderLeftStyle = cell.BorderTopStyle = cell.BorderRightStyle = cell.BorderBottomStyle = CssConstants.Solid;
        cell.BorderLeftWidth = cell.BorderTopWidth = cell.BorderRightWidth = cell.BorderBottomWidth = border;
    });

    private static void ApplyTablePadding(CssBox table, string padding)
    {
        var length = TranslateLength(padding);
        SetForAllCells(table, cell => cell.PaddingLeft = cell.PaddingTop = cell.PaddingRight = cell.PaddingBottom = length);
    }

    private static void SetForAllCells(CssBox table, ActionInt<CssBox> action)
    {
        foreach (var l1 in table.Boxes)
        {
            foreach (var l2 in l1.Boxes)
            {
                if (l2.HtmlTag != null && l2.HtmlTag.Name == "td")
                {
                    action(l2);
                }
                else
                {
                    foreach (var l3 in l2.Boxes)
                    {
                        action(l3);
                    }
                }
            }
        }
    }

    private static void CorrectTextBoxes(CssBox box)
    {
        for (int i = box.Boxes.Count - 1; i >= 0; i--)
        {
            var childBox = box.Boxes[i];
            if (!childBox.Text.IsEmpty)
            {
                // is the box has text
                var keepBox = !childBox.Text.Span.IsWhiteSpace();

                // is the box is pre-formatted
                keepBox = keepBox || childBox.WhiteSpace == CssConstants.Pre || childBox.WhiteSpace == CssConstants.PreWrap;

                // is the box is only one in the parent
                keepBox = keepBox || box.Boxes.Count == 1;

                // is it a whitespace between two inline boxes
                keepBox = keepBox || (i > 0 && i < box.Boxes.Count - 1 && box.Boxes[i - 1].IsInline && box.Boxes[i + 1].IsInline);

                // is first/last box where is in inline box and it's next/previous box is inline
                keepBox = keepBox || (i == 0 && box.Boxes.Count > 1 && box.Boxes[1].IsInline && box.IsInline) || (i == box.Boxes.Count - 1 && box.Boxes.Count > 1 && box.Boxes[i - 1].IsInline && box.IsInline);

                if (keepBox)
                {
                    // valid text box, parse it to words
                    childBox.ParseToWords();
                }
                else
                {
                    // remove text box that has no 
                    childBox.ParentBox.Boxes.RemoveAt(i);
                }
            }
            else
            {
                // recursive
                CorrectTextBoxes(childBox);
            }
        }
    }

    private static void CorrectImgBoxes(CssBox box)
    {
        for (int i = box.Boxes.Count - 1; i >= 0; i--)
        {
            var childBox = box.Boxes[i];
            if (childBox is CssBoxImage && childBox.Display == CssConstants.Block)
            {
                var block = CssBoxHelper.CreateBlock(childBox.ParentBox, null, childBox);
                childBox.ParentBox = block;
                childBox.Display = CssConstants.Inline;
            }
            else
            {
                // recursive
                CorrectImgBoxes(childBox);
            }
        }
    }

    /// <summary>
    /// Implements the <c>&lt;object&gt;</c> fallback chain (HTML4 §13.3):
    /// when an <c>&lt;object&gt;</c> element's <c>data</c> attribute points to a
    /// supported image (<c>data:image/…</c>), it is rendered as a replaced image
    /// and its children (fallback content) are removed.  Otherwise, children
    /// are kept as fallback content.
    /// </summary>
    private static void CorrectObjectBoxes(CssBox box)
    {
        for (int i = box.Boxes.Count - 1; i >= 0; i--)
        {
            var childBox = box.Boxes[i];

            if (childBox is CssBoxImage &&
                childBox.HtmlTag != null &&
                childBox.HtmlTag.Name.Equals("object", StringComparison.OrdinalIgnoreCase))
            {
                // This <object> was promoted to CssBoxImage because its data
                // attribute contains a data:image URI.  Remove fallback children
                // so only the image renders.
                childBox.Boxes.Clear();
            }

            // Recurse into all children (including non-object boxes)
            CorrectObjectBoxes(childBox);
        }
    }

    private static void CorrectLineBreaksBlocks(CssBox box, ref bool followingBlock)
    {
        followingBlock = followingBlock || box.IsBlock;

        foreach (var childBox in box.Boxes)
        {
            CorrectLineBreaksBlocks(childBox, ref followingBlock);
            followingBlock = childBox.Words.Count == 0 && (followingBlock || childBox.IsBlock);
        }

        int lastBr = -1;
        CssBox brBox;

        do
        {
            brBox = null;
            for (int i = 0; i < box.Boxes.Count && brBox == null; i++)
            {
                if (i > lastBr && box.Boxes[i].IsBrElement)
                {
                    brBox = box.Boxes[i];
                    lastBr = i;
                }
                else if (box.Boxes[i].Words.Count > 0)
                {
                    followingBlock = false;
                }
                else if (box.Boxes[i].IsBlock)
                {
                    followingBlock = true;
                }
            }

            if (brBox != null)
            {
                brBox.Display = CssConstants.Block;
                if (followingBlock)
                    brBox.Height = ".95em"; // TODO:a check the height to min-height when it is supported
            }
        } while (brBox != null);
    }

    private static void CorrectBlockInsideInline(CssBox box)
    {
        try
        {
            if (DomUtils.ContainsInlinesOnly(box) && !ContainsInlinesOnlyDeep(box))
            {
                var tempRightBox = CorrectBlockInsideInlineImp(box);
                while (tempRightBox != null)
                {
                    // loop on the created temp right box for the fixed box until no more need (optimization remove recursion)
                    CssBox newTempRightBox = null;
                    if (DomUtils.ContainsInlinesOnly(tempRightBox) && !ContainsInlinesOnlyDeep(tempRightBox))
                        newTempRightBox = CorrectBlockInsideInlineImp(tempRightBox);

                    tempRightBox.ParentBox.SetAllBoxes(tempRightBox);
                    tempRightBox.ParentBox = null;
                    tempRightBox = newTempRightBox;
                }
            }

            if (!DomUtils.ContainsInlinesOnly(box))
            {
                foreach (var childBox in box.Boxes)
                    CorrectBlockInsideInline(childBox);
            }
        }
        catch (Exception ex)
        {
            box.ContainerInt.ReportError(HtmlRenderErrorType.HtmlParsing, "Failed in block inside inline box correction", ex);
        }
    }

    /// <summary>
    /// Rearrange the DOM of the box to have block box with boxes before the inner block box and after.
    /// </summary>
    /// <param name="box">the box that has the problem</param>
    private static CssBox CorrectBlockInsideInlineImp(CssBox box)
    {
        if (box.Display == CssConstants.Inline)
            box.Display = CssConstants.Block;

        if (box.Boxes.Count > 1 || box.Boxes[0].Boxes.Count > 1)
        {
            var leftBlock = CssBoxHelper.CreateBlock(box);

            while (ContainsInlinesOnlyDeep(box.Boxes[0]))
                box.Boxes[0].ParentBox = leftBlock;
            leftBlock.SetBeforeBox(box.Boxes[0]);

            var splitBox = box.Boxes[1];
            splitBox.ParentBox = null;

            CorrectBlockSplitBadBox(box, splitBox, leftBlock);

            // remove block that did not get any inner elements
            if (leftBlock.Boxes.Count < 1)
                leftBlock.ParentBox = null;

            int minBoxes = leftBlock.ParentBox != null ? 2 : 1;
            if (box.Boxes.Count > minBoxes)
            {
                // create temp box to handle the tail elements and then get them back so no deep hierarchy is created
                var tempRightBox = CssBoxHelper.CreateBox(box, null, box.Boxes[minBoxes]);
                while (box.Boxes.Count > minBoxes + 1)
                    box.Boxes[minBoxes + 1].ParentBox = tempRightBox;

                return tempRightBox;
            }
        }
        else if (box.Boxes[0].Display == CssConstants.Inline)
        {
            box.Boxes[0].Display = CssConstants.Block;
        }

        return null;
    }

    private static void CorrectBlockSplitBadBox(CssBox parentBox, CssBox badBox, CssBox leftBlock)
    {
        CssBox leftbox = null;
        while (badBox.Boxes[0].IsInline && ContainsInlinesOnlyDeep(badBox.Boxes[0]))
        {
            if (leftbox == null)
            {
                // if there is no elements in the left box there is no reason to keep it
                leftbox = CssBoxHelper.CreateBox(leftBlock, badBox.HtmlTag);
                leftbox.InheritStyle(badBox, true);
            }
            badBox.Boxes[0].ParentBox = leftbox;
        }

        var splitBox = badBox.Boxes[0];
        if (!ContainsInlinesOnlyDeep(splitBox))
        {
            CorrectBlockSplitBadBox(parentBox, splitBox, leftBlock);
            splitBox.ParentBox = null;
        }
        else
        {
            splitBox.ParentBox = parentBox;
        }

        if (badBox.Boxes.Count > 0)
        {
            CssBox rightBox;
            if (splitBox.ParentBox != null || parentBox.Boxes.Count < 3)
            {
                rightBox = CssBoxHelper.CreateBox(parentBox, badBox.HtmlTag);
                rightBox.InheritStyle(badBox, true);

                if (parentBox.Boxes.Count > 2)
                    rightBox.SetBeforeBox(parentBox.Boxes[1]);

                if (splitBox.ParentBox != null)
                    splitBox.SetBeforeBox(rightBox);
            }
            else
            {
                rightBox = parentBox.Boxes[2];
            }

            rightBox.SetAllBoxes(badBox);
        }
        else if (splitBox.ParentBox != null && parentBox.Boxes.Count > 1)
        {
            splitBox.SetBeforeBox(parentBox.Boxes[1]);
            if (splitBox.HtmlTag != null && splitBox.HtmlTag.Name == "br" && (leftbox != null || leftBlock.Boxes.Count > 1))
                splitBox.Display = CssConstants.Inline;
        }
    }

    private static void CorrectInlineBoxesParent(CssBox box)
    {
        if (ContainsVariantBoxes(box))
        {
            for (int i = 0; i < box.Boxes.Count; i++)
            {
                if (box.Boxes[i].IsInline)
                {
                    var newbox = CssBoxHelper.CreateBlock(box, null, box.Boxes[i++]);
                    while (i < box.Boxes.Count && box.Boxes[i].IsInline)
                        box.Boxes[i].ParentBox = newbox;
                }
            }
        }

        if (!DomUtils.ContainsInlinesOnly(box))
        {
            foreach (var childBox in box.Boxes)
                CorrectInlineBoxesParent(childBox);
        }
    }

    private static bool ContainsInlinesOnlyDeep(CssBox box)
    {
        foreach (var childBox in box.Boxes)
        {
            if (!childBox.IsInline || !ContainsInlinesOnlyDeep(childBox))
                return false;
        }

        return true;
    }

    private static bool ContainsVariantBoxes(CssBox box)
    {
        bool hasBlock = false;
        bool hasInline = false;

        for (int i = 0; i < box.Boxes.Count && (!hasBlock || !hasInline); i++)
        {
            var isBlock = !box.Boxes[i].IsInline;
            hasBlock = hasBlock || isBlock;
            hasInline = hasInline || !isBlock;
        }

        return hasBlock && hasInline;
    }
}