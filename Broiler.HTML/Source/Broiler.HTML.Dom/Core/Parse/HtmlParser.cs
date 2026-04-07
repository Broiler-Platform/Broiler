using Broiler.HTML.Dom.Core.Dom;
using Broiler.HTML.Dom.Core.Utils;
using Broiler.HTML.Utils.Core.Utils;
using System;
using System.Collections.Generic;

namespace Broiler.HTML.Dom.Core.Parse;

/// <summary>
/// Parses an HTML string into a <see cref="CssBox"/> tree for CSS layout.
/// Uses the shared WHATWG-aligned <see cref="HtmlTokenizer"/> (originally
/// from the DomBridge in Broiler.App) instead of a hand-rolled scanner,
/// giving more accurate tokenisation of tags, attributes, comments,
/// raw-text elements (<c>&lt;style&gt;</c> / <c>&lt;script&gt;</c>), and
/// processing instructions.
/// </summary>
internal static class HtmlParser
{
    /// <summary>
    /// Elements that implicitly close an open <c>&lt;p&gt;</c> element
    /// per HTML 4 DTD / HTML5 §12.2.6.4.7.
    /// </summary>
    private static readonly HashSet<string> _pClosingTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "details", "div", "dl",
        "fieldset", "figcaption", "figure", "footer", "form",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "header", "hgroup", "hr", "li", "main", "nav", "ol",
        "p", "pre", "section", "summary", "table", "ul"
    };

    public static CssBox ParseDocument(string source, Uri baseUrl)
    {
        var root = CssBoxHelper.CreateBlock(baseUrl);
        var curBox = root;

        var tokenizer = new HtmlTokenizer();
        foreach (var token in tokenizer.Tokenize(source))
        {
            switch (token.Type)
            {
                case TokenType.Character:
                {
                    // Add text content as anonymous css box (mirrors AddTextBox)
                    var data = token.Data;
                    if (!string.IsNullOrEmpty(data))
                    {
                        var abox = CssBoxHelper.CreateBox(curBox, baseUrl);
                        abox.Text = data.AsMemory();
                    }
                    break;
                }

                case TokenType.StartTag:
                {
                    var tagName = token.Name;
                    if (string.IsNullOrEmpty(tagName))
                        break;

                    var isSingle = HtmlUtils.IsSingleTag(tagName) || token.SelfClosing;

                    // Decode attribute values to match original ExtractAttributes
                    // behaviour (WebUtility.HtmlDecode on each value).
                    Dictionary<string, string> attrs = null;
                    if (token.Attributes.Count > 0)
                    {
                        attrs = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                        foreach (var kvp in token.Attributes)
                            attrs[kvp.Key] = HtmlUtils.DecodeHtml(kvp.Value);
                    }

                    var tag = new HtmlTag(tagName, isSingle, attrs);

                    // HTML 4 DTD / HTML5 §12.2.6.4.7: implicitly close an open <p>
                    // when a block-level element that cannot be a child of <p>
                    // is encountered.
                    if (!isSingle && curBox.HtmlTag != null
                        && curBox.HtmlTag.Name.Equals("p", StringComparison.OrdinalIgnoreCase)
                        && _pClosingTags.Contains(tagName)
                        && curBox.ParentBox != null)
                    {
                        curBox = curBox.ParentBox;
                    }

                    if (isSingle)
                    {
                        // the current box is not changed
                        var singleBox = CssBoxHelper.CreateBox(tag, baseUrl, curBox);

                        // Inject visible text content for <input> elements.
                        // Submit/button/reset: always show label (default to
                        //   "Submit"/"Reset" when value is absent).
                        // Text-like inputs (text, search, email, url, tel,
                        //   number, password): render the value attribute so
                        //   the text appears inside the input box.
                        if (tagName.Equals("input", StringComparison.OrdinalIgnoreCase))
                        {
                            var inputType = tag.TryGetAttribute("type")?.ToLowerInvariant() ?? "text";
                            if (inputType is "submit" or "button" or "reset")
                            {
                                var label = tag.TryGetAttribute("value");
                                if (string.IsNullOrEmpty(label))
                                    label = inputType == "submit" ? "Submit" : inputType == "reset" ? "Reset" : "";
                                if (!string.IsNullOrEmpty(label))
                                {
                                    var textBox = CssBoxHelper.CreateBox(singleBox, baseUrl);
                                    textBox.Text = label.AsMemory();
                                }
                            }
                            else if (inputType is "text" or "search" or "email"
                                     or "url" or "tel" or "number" or "password")
                            {
                                var val = tag.TryGetAttribute("value");
                                if (!string.IsNullOrEmpty(val))
                                {
                                    var textBox = CssBoxHelper.CreateBox(singleBox, baseUrl);
                                    textBox.Text = val.AsMemory();
                                }
                            }
                        }
                    }
                    else
                    {
                        // go one level down, make the new box the current box
                        curBox = CssBoxHelper.CreateBox(tag, baseUrl, curBox);
                    }
                    break;
                }

                case TokenType.EndTag:
                {
                    var tagName = token.Name;
                    if (!HtmlUtils.IsSingleTag(tagName) && curBox.ParentBox != null)
                    {
                        // need to find the parent tag to go one level up
                        curBox = DomUtils.FindParent(curBox.ParentBox, tagName, curBox);
                    }
                    break;
                }

                case TokenType.Comment:
                case TokenType.Doctype:
                    // Skip comments and doctype declarations
                    // (consistent with original behaviour)
                    break;

                case TokenType.EndOfFile:
                    break;
            }
        }

        return root;
    }
}