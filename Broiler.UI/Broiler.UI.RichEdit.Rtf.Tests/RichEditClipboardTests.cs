using System.Linq;

namespace Broiler.UI.RichEdit.Rtf.Tests;

/// <summary>A concrete <see cref="UiRichEdit"/> with no rendering — enough to exercise the adapter.</summary>
internal sealed class TestRichEdit : UiRichEdit
{
}

/// <summary>An in-memory rich clipboard.</summary>
internal sealed class FakeRichClipboard : IUiRichClipboardHost
{
    private string? _text;
    private string? _rtf;

    public void SetRichContent(string plainText, string rtf)
    {
        _text = plainText;
        _rtf = rtf;
    }

    public void SetTextOnly(string text)
    {
        _text = text;
        _rtf = null;
    }

    public bool TryGetRtf(out string rtf)
    {
        rtf = _rtf ?? string.Empty;
        return _rtf is not null;
    }

    public bool TryGetPlainText(out string text)
    {
        text = _text ?? string.Empty;
        return _text is not null;
    }
}

public sealed class RichEditClipboardTests
{
    private static readonly InlineStyle Bold = new() { Bold = true };
    private static readonly InlineStyle Red = new() { Foreground = new BColor(255, 0, 0) };

    private static RichTextParagraph Para(params (string Text, InlineStyle Style)[] runs)
    {
        RichTextParagraph paragraph = RichTextParagraph.Create(string.Empty, InlineStyle.Default);
        foreach ((string text, InlineStyle style) in runs)
            paragraph = paragraph.InsertText(paragraph.Length, text, style);
        return paragraph;
    }

    private static TestRichEdit Editor(RichTextDocument document)
    {
        var editor = new TestRichEdit { Document = document };
        editor.Selection = new RichTextRange(document.Start, document.End); // select all
        return editor;
    }

    [Fact]
    public void Copy_Then_Paste_Round_Trips_Formatting_Through_Rtf()
    {
        TestRichEdit source = Editor(RichTextDocument.FromParagraphs(new[]
        {
            Para(("plain ", InlineStyle.Default), ("bold", Bold), (" ", InlineStyle.Default), ("red", Red)),
        }));
        var host = new FakeRichClipboard();

        Assert.True(RichEditClipboard.Copy(source, host));
        Assert.True(host.TryGetRtf(out string rtf));
        Assert.StartsWith("{\\rtf1", rtf);

        var target = new TestRichEdit();
        Assert.True(RichEditClipboard.Paste(target, host));

        RichTextParagraph paragraph = target.Document.Paragraphs[0];
        Assert.Equal("plain bold red", paragraph.Text);
        Assert.True(paragraph.StyleAt(paragraph.Text.IndexOf("bold")).Bold);
        Assert.Equal(new BColor(255, 0, 0), paragraph.StyleAt(paragraph.Text.IndexOf("red")).Foreground);
    }

    [Fact]
    public void Copy_Returns_False_When_There_Is_No_Selection()
    {
        var editor = new TestRichEdit { Document = RichTextDocument.FromPlainText("text") };
        // Default selection is a caret at the start (empty).
        Assert.False(RichEditClipboard.Copy(editor, new FakeRichClipboard()));
    }

    [Fact]
    public void Paste_Falls_Back_To_Plain_Text_When_No_Rtf_Is_Present()
    {
        var host = new FakeRichClipboard();
        host.SetTextOnly("just text");
        var target = new TestRichEdit();

        Assert.True(RichEditClipboard.Paste(target, host));
        Assert.Equal("just text", target.Document.PlainText);
    }

    [Fact]
    public void Paste_Into_A_Read_Only_Editor_Does_Nothing()
    {
        var host = new FakeRichClipboard();
        host.SetRichContent("x", RichEditClipboard.DocumentToRtf(RichTextDocument.FromPlainText("x")));
        var target = new TestRichEdit { IsReadOnly = true };

        Assert.False(RichEditClipboard.Paste(target, host));
        Assert.Equal(string.Empty, target.Document.PlainText);
    }

    [Fact]
    public void Rich_Paste_Sanitizes_Malicious_Rtf()
    {
        // An embedded object and a javascript: hyperlink must not survive the paste.
        string malicious =
            "{\\rtf1 before {\\object\\objemb{\\*\\objdata deadbeef}}" +
            "{\\field{\\*\\fldinst{HYPERLINK \"javascript:evil\"}}{\\fldrslt link}} after}";
        var target = new TestRichEdit();

        Assert.True(RichEditClipboard.InsertRtf(target, malicious));

        RichTextParagraph paragraph = target.Document.Paragraphs[0];
        Assert.Equal("before link after", paragraph.Text);
        Assert.DoesNotContain(paragraph.Runs, run => run.Style.LinkHref is not null);
    }

    [Fact]
    public void SelectionToRtf_Serializes_Only_The_Selection()
    {
        var editor = new TestRichEdit
        {
            Document = RichTextDocument.FromParagraphs(new[] { Para(("keep", Bold), ("drop", InlineStyle.Default)) }),
        };

        // Select the first four characters ("keep").
        RichTextPosition end = editor.Document.Start;
        for (int i = 0; i < 4; i++)
            end = editor.Document.PositionRightOf(end);
        editor.Selection = new RichTextRange(editor.Document.Start, end);

        string rtf = RichEditClipboard.SelectionToRtf(editor);

        var fresh = new TestRichEdit();
        Assert.True(RichEditClipboard.InsertRtf(fresh, rtf));
        Assert.Equal("keep", fresh.Document.PlainText);
    }
}
