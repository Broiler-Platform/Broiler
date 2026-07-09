namespace Broiler.UI.RichEdit.Tests;

/// <summary>
/// Test helpers. Positions are constructed through the internal constructor
/// (the test assembly has <c>InternalsVisibleTo</c> access) so tests can address
/// arbitrary locations; production code only obtains positions from the document.
/// </summary>
internal static class Doc
{
    public static RichTextPosition Pos(int paragraph, int offset) => new(paragraph, offset);

    public static RichTextRange Range(int startParagraph, int startOffset, int endParagraph, int endOffset) =>
        new(new RichTextPosition(startParagraph, startOffset), new RichTextPosition(endParagraph, endOffset));

    public static int TotalRunLength(RichTextParagraph paragraph)
    {
        int total = 0;
        foreach (StyleRun run in paragraph.Runs)
            total += run.Length;
        return total;
    }

    /// <summary>Asserts a paragraph's runs are normalized and cover its text exactly.</summary>
    public static void AssertNormalized(RichTextParagraph paragraph)
    {
        for (int i = 0; i < paragraph.Runs.Count; i++)
        {
            Assert.True(paragraph.Runs[i].Length > 0, "run has non-positive length");
            if (i > 0)
                Assert.False(
                    paragraph.Runs[i - 1].Style.Equals(paragraph.Runs[i].Style),
                    "adjacent runs share a style and should have been merged");
        }

        Assert.Equal(paragraph.Text.Length, TotalRunLength(paragraph));
    }

    /// <summary>Asserts every paragraph in the document is normalized.</summary>
    public static void AssertNormalized(RichTextDocument document)
    {
        foreach (RichTextParagraph paragraph in document.Paragraphs)
            AssertNormalized(paragraph);
    }
}
