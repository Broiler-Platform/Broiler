using Broiler.Browser;
using Broiler.Dom;
using Broiler.Dom.Html;

namespace Broiler.Browser.Core.Tests;

/// <summary>
/// Covers the form data set the browser sends. The renderer resolves a submit
/// control to its form's <c>action</c> and navigates there verbatim, so without
/// this a search form went to <c>/search</c> instead of <c>/search?q=…</c>.
/// </summary>
public class HtmlFormSerializerTests
{
    private static DomElement Parse(string html) =>
        new HtmlDocumentParser().ParseDocument(html).Document.DocumentElement
        ?? throw new InvalidOperationException("No document element.");

    private static DomElement Form(string html) =>
        FindFirst(Parse(html), "form") ?? throw new InvalidOperationException("No form.");

    private static DomElement? FindFirst(DomNode root, string tag)
    {
        foreach (DomNode child in root.ChildNodes)
        {
            if (child is DomElement element &&
                string.Equals(element.TagName, tag, StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }

            if (FindFirst(child, tag) is { } nested)
                return nested;
        }

        return null;
    }

    [Fact]
    public void TextInputsContributeNameAndValue()
    {
        DomElement form = Form("<form action='/search'><input name='q' value='broiler'></form>");

        Assert.Equal("q=broiler", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void SpacesAndReservedCharactersAreFormUrlEncoded()
    {
        DomElement form = Form("<form><input name='q' value=\"a b&c=d\"></form>");

        // application/x-www-form-urlencoded: space is +, and & = are escaped.
        Assert.Equal("q=a+b%26c%3Dd", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void HiddenInputsAreSubmitted()
    {
        DomElement form = Form(
            "<form><input type='hidden' name='src' value='hp'><input name='q' value='x'></form>");

        Assert.Equal("src=hp&q=x", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void DisabledControlsAreNotSuccessful()
    {
        DomElement form = Form("<form><input name='a' value='1' disabled><input name='b' value='2'></form>");

        Assert.Equal("b=2", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void UnnamedControlsAreNotSuccessful()
    {
        DomElement form = Form("<form><input value='1'><input name='b' value='2'></form>");

        Assert.Equal("b=2", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void OnlyCheckedCheckboxesAreSubmitted()
    {
        DomElement form = Form(
            "<form><input type='checkbox' name='a' value='1' checked>" +
            "<input type='checkbox' name='b' value='2'></form>");

        Assert.Equal("a=1", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void CheckboxWithoutValueSubmitsOn()
    {
        DomElement form = Form("<form><input type='checkbox' name='agree' checked></form>");

        Assert.Equal("agree=on", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void CheckedStateCanBeOverriddenByTheHost()
    {
        DomElement form = Form(
            "<form><input type='checkbox' name='a' value='1'>" +
            "<input type='checkbox' name='b' value='2' checked></form>");

        string body = HtmlFormSerializer.BuildFormData(form, submitter: null, control =>
            control.GetAttribute("name") switch
            {
                "a" => HtmlFormSerializer.CheckedOverride,
                "b" => HtmlFormSerializer.UncheckedOverride,
                _ => null,
            });

        Assert.Equal("a=1", body);
    }

    [Fact]
    public void ResetAndFileControlsAreNeverSubmitted()
    {
        DomElement form = Form(
            "<form><input type='reset' name='r' value='Reset'>" +
            "<input type='file' name='f'><input name='q' value='x'></form>");

        Assert.Equal("q=x", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void OnlyTheSubmitterContributesItsOwnNameAndValue()
    {
        DomElement form = Form(
            "<form><input name='q' value='x'>" +
            "<input type='submit' name='go' value='Search'>" +
            "<input type='submit' name='lucky' value='Lucky'></form>");

        DomElement lucky = FindSubmit(form, "lucky");

        Assert.Equal("q=x&lucky=Lucky", HtmlFormSerializer.BuildFormData(form, lucky));
        // With no submitter — Enter pressed in a field — no button contributes.
        Assert.Equal("q=x", HtmlFormSerializer.BuildFormData(form));
    }

    private static DomElement FindSubmit(DomElement form, string name)
    {
        foreach (DomNode child in form.ChildNodes)
        {
            if (child is DomElement element &&
                string.Equals(element.GetAttribute("name"), name, StringComparison.Ordinal))
            {
                return element;
            }
        }

        throw new InvalidOperationException($"No control named {name}.");
    }

    [Fact]
    public void TextAreaSubmitsItsTextContent()
    {
        DomElement form = Form("<form><textarea name='q'>typed text</textarea></form>");

        Assert.Equal("q=typed+text", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void SelectSubmitsTheSelectedOption()
    {
        DomElement form = Form(
            "<form><select name='s'><option value='a'>A</option>" +
            "<option value='b' selected>B</option></select></form>");

        Assert.Equal("s=b", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void SingleSelectWithNoSelectionSubmitsItsFirstOption()
    {
        DomElement form = Form(
            "<form><select name='s'><option value='a'>A</option><option value='b'>B</option></select></form>");

        Assert.Equal("s=a", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void MultiSelectSubmitsEverySelectedOption()
    {
        DomElement form = Form(
            "<form><select name='s' multiple><option value='a' selected>A</option>" +
            "<option value='b'>B</option><option value='c' selected>C</option></select></form>");

        Assert.Equal("s=a&s=c", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void OptionWithoutValueSubmitsItsText()
    {
        DomElement form = Form("<form><select name='s'><option selected>Plain</option></select></form>");

        Assert.Equal("s=Plain", HtmlFormSerializer.BuildFormData(form));
    }

    [Fact]
    public void ApplyQueryReplacesAnExistingQueryAndDropsTheFragment()
    {
        Assert.Equal("https://x/search?q=1", HtmlFormSerializer.ApplyQuery("https://x/search", "q=1"));
        Assert.Equal("https://x/search?q=1", HtmlFormSerializer.ApplyQuery("https://x/search?old=9", "q=1"));
        Assert.Equal("https://x/search?q=1", HtmlFormSerializer.ApplyQuery("https://x/search#frag", "q=1"));
        Assert.Equal("https://x/search", HtmlFormSerializer.ApplyQuery("https://x/search?old=9", string.Empty));
    }

    [Fact]
    public void PostFormsAreNotTreatedAsGetSubmissions()
    {
        Assert.True(HtmlFormSerializer.IsGetSubmission(Form("<form action='/a'></form>")));
        Assert.True(HtmlFormSerializer.IsGetSubmission(Form("<form action='/a' method='GET'></form>")));
        Assert.False(HtmlFormSerializer.IsGetSubmission(Form("<form action='/a' method='post'></form>")));
    }

    [Fact]
    public void FindEnclosingFormWalksUpThroughWrappers()
    {
        DomElement root = Parse("<form action='/a'><div><span><input id='q' name='q'></span></div></form>");
        DomElement input = HtmlFormSerializer.FindControl(root, new Dictionary<string, string> { ["id"] = "q" })!;

        DomElement? form = HtmlFormSerializer.FindEnclosingForm(input);

        Assert.NotNull(form);
        Assert.Equal("/a", form.GetAttribute("action"));
    }

    [Fact]
    public void FindEnclosingFormReturnsNullOutsideAnyForm()
    {
        DomElement root = Parse("<div><input id='q' name='q'></div>");
        DomElement input = HtmlFormSerializer.FindControl(root, new Dictionary<string, string> { ["id"] = "q" })!;

        Assert.Null(HtmlFormSerializer.FindEnclosingForm(input));
    }

    [Fact]
    public void ControlsAreFoundByTypeNameAndValueWhenTheyHaveNoId()
    {
        DomElement root = Parse(
            "<form><input type='submit' name='go' value='Search'>" +
            "<input type='submit' name='lucky' value='Lucky'></form>");

        DomElement? found = HtmlFormSerializer.FindControl(root, new Dictionary<string, string>
        {
            ["type"] = "submit",
            ["name"] = "lucky",
            ["value"] = "Lucky",
        });

        Assert.NotNull(found);
        Assert.Equal("lucky", found.GetAttribute("name"));
    }

    [Fact]
    public void EncodingIsTakenFromEnctypeWithUrlEncodedAsTheDefault()
    {
        Assert.Equal(HtmlFormSerializer.UrlEncoded, HtmlFormSerializer.ResolveEncoding(Form("<form></form>")));
        Assert.Equal(
            HtmlFormSerializer.MultipartFormData,
            HtmlFormSerializer.ResolveEncoding(Form("<form enctype='multipart/form-data'></form>")));
        Assert.Equal(
            HtmlFormSerializer.TextPlain,
            HtmlFormSerializer.ResolveEncoding(Form("<form enctype='TEXT/PLAIN'></form>")));
        // An unrecognised enctype falls back to the spec's missing-value default.
        Assert.Equal(
            HtmlFormSerializer.UrlEncoded,
            HtmlFormSerializer.ResolveEncoding(Form("<form enctype='application/json'></form>")));
    }

    [Fact]
    public void EveryEncodingIsBuiltFromTheSameEntryList()
    {
        DomElement form = Form(
            "<form><input name='a' value='1'><input name='b' value='two words'>" +
            "<input name='skip' value='x' disabled></form>");

        IReadOnlyList<HtmlFormSerializer.FormEntry> entries = HtmlFormSerializer.BuildEntryList(form);

        Assert.Equal(["a", "b"], entries.Select(e => e.Name));
        Assert.Equal("a=1&b=two+words", HtmlFormSerializer.EncodeUrlEncoded(entries));
        Assert.Equal("a=1\r\nb=two words\r\n", HtmlFormSerializer.EncodeTextPlain(entries));
    }

    [Fact]
    public void MultipartWrapsEachEntryInItsOwnPart()
    {
        DomElement form = Form("<form><input name='a' value='1'></form>");
        IReadOnlyList<HtmlFormSerializer.FormEntry> entries = HtmlFormSerializer.BuildEntryList(form);

        string body = HtmlFormSerializer.EncodeMultipart(entries, "BOUNDARY");

        Assert.Equal(
            "--BOUNDARY\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n1\r\n--BOUNDARY--\r\n",
            body);
    }

    [Fact]
    public void MultipartBoundariesAreUnique()
    {
        Assert.NotEqual(
            HtmlFormSerializer.CreateMultipartBoundary(),
            HtmlFormSerializer.CreateMultipartBoundary());
    }
}
