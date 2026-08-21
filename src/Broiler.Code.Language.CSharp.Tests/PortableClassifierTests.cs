using System.Text;
using Broiler.Code.Core;
using Broiler.Code.Language.CSharp.Syntax;
using Broiler.Code.Workspaces.Text;
using Broiler.UI.CodeEditor;

namespace Broiler.Code.Language.CSharp.Tests;

/// <summary>
/// The portable classifier, now over the real workspace buffer rather than the
/// Phase 0 prototype's own snapshot. The property that matters is unchanged and
/// is re-proved here against the new plumbing: an incremental result must be
/// indistinguishable from a full one. Speed without that is worthless.
/// </summary>
public sealed class PortableClassifierTests
{
    private static (SourceBufferDocument Document, SourceBuffer Buffer) Create(string text)
    {
        var buffer = new SourceBuffer(text);
        return (new SourceBufferDocument(buffer), buffer);
    }

    private static void AssertMatchesFull(
        PortableCSharpClassifier classifier, CodeClassificationResult incremental, ICodeTextSnapshot snapshot)
    {
        CodeClassificationResult full = new PortableCSharpClassifier()
            .Classify(snapshot, null, null, CancellationToken.None);

        Assert.Equal(full.LineCount, incremental.LineCount);
        for (int line = 0; line < full.LineCount; line++)
        {
            Assert.True(
                full.GetLineSpans(line).SequenceEqual(incremental.GetLineSpans(line)),
                $"Line {line} differs between the incremental and full results.");
        }
    }

    public static IEnumerable<object[]> MultiLineStateSources() => new[]
    {
        new object[] { "block-comment", "var a = 1;\n/* opened\nstill inside\n*/ var b = 2;\n" },
        new object[] { "doc-block", "/** doc\n * more\n */\nclass C { }\n" },
        new object[] { "verbatim", "var p = @\"C:\\one\ntwo\"\"three\n\";\nvar q = 3;\n" },
        new object[] { "interpolated-verbatim", "var p = $@\"a{{b}}\nnext {x}\n\";\nvar q = 3;\n" },
        new object[] { "raw", "var r = \"\"\"\nline one \"not closed\"\n\"\"\";\nvar s = 4;\n" },
        new object[] { "raw-interpolated", "var r = $$\"\"\"\n{{{value}}} and {{literal}}\n\"\"\";\n" },
        new object[] { "unterminated-string", "var s = \"open\nvar t = 2;\n" },
        new object[] { "directive", "#if DEBUG\nConsole.WriteLine(\"d\");\n#endif\n" },
        new object[] { "crlf-mixed", "var a = 1;\r\n/* c\r\nd */\r\nvar b = 2;\n" },
        new object[] { "no-trailing-newline", "var a = 1;" },
    };

    [Theory]
    [MemberData(nameof(MultiLineStateSources))]
    public void Incremental_Equals_Full_At_Every_Edit_Position(string name, string text)
    {
        _ = name;
        var classifier = new PortableCSharpClassifier();
        (SourceBufferDocument document, SourceBuffer buffer) = Create(text);

        CodeClassificationResult result = classifier.Classify(
            document.Snapshot, null, null, CancellationToken.None);

        // An edit walked across every position, because a line-state classifier
        // gets multi-line constructs wrong exactly where a boundary falls.
        for (int position = 0; position <= buffer.Current.Length; position++)
        {
            var insert = new CodeEditIntent(document.Snapshot.Version, position, 0, "\"/*", "probe");
            CodeEditOutcome applied = document.Submit(insert);
            Assert.True(applied.Accepted);

            result = classifier.Classify(
                document.Snapshot,
                result,
                new CodeTextChange(position, 0, 3),
                CancellationToken.None);
            AssertMatchesFull(classifier, result, document.Snapshot);

            var remove = new CodeEditIntent(document.Snapshot.Version, position, 3, string.Empty, "probe");
            Assert.True(document.Submit(remove).Accepted);
            result = classifier.Classify(
                document.Snapshot,
                result,
                new CodeTextChange(position, 3, 0),
                CancellationToken.None);
            AssertMatchesFull(classifier, result, document.Snapshot);
        }

        document.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Single_Character_Edit_Relexes_One_Line_In_A_Large_File()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < 100_000; i++)
            builder.Append("        int total").Append(i).Append(" = 0; // line ").Append(i).Append('\n');

        var classifier = new PortableCSharpClassifier();
        (SourceBufferDocument document, SourceBuffer buffer) = Create(builder.ToString());

        CodeClassificationResult result = classifier.Classify(
            document.Snapshot, null, null, CancellationToken.None);
        Assert.Equal(100_001, classifier.LastLinesLexed);

        int anchor = buffer.Current.GetLineStart(50_000) + 8;
        Assert.True(document.Submit(
            new CodeEditIntent(document.Snapshot.Version, anchor, 0, "x", "type")).Accepted);

        classifier.Classify(
            document.Snapshot, result, new CodeTextChange(anchor, 0, 1), CancellationToken.None);

        Assert.Equal(1, classifier.LastLinesLexed);
        document.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Unclosable_Raw_String_Relexes_To_The_End_And_Stays_Correct()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < 2_000; i++)
            builder.Append("var value").Append(i).Append(" = ").Append(i).Append(";\n");

        var classifier = new PortableCSharpClassifier();
        (SourceBufferDocument document, SourceBuffer buffer) = Create(builder.ToString());
        CodeClassificationResult result = classifier.Classify(
            document.Snapshot, null, null, CancellationToken.None);

        // Eleven quotes: a raw string closes only on a run of at least as many,
        // and no such run exists below. The state never reconverges.
        Assert.True(document.Submit(
            new CodeEditIntent(document.Snapshot.Version, 0, 0, "\"\"\"\"\"\"\"\"\"\"\"", "runaway")).Accepted);

        result = classifier.Classify(
            document.Snapshot, result, new CodeTextChange(0, 0, 11), CancellationToken.None);

        Assert.Equal(document.Snapshot.LineCount, classifier.LastLinesLexed);
        AssertMatchesFull(classifier, result, document.Snapshot);
        document.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Cancellation_Is_Observed_Rather_Than_Running_To_Completion()
    {
        var builder = new StringBuilder();
        for (int i = 0; i < 50_000; i++)
            builder.Append("var x").Append(i).Append(" = ").Append(i).Append(";\n");

        (SourceBufferDocument document, _) = Create(builder.ToString());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new PortableCSharpClassifier().Classify(
                document.Snapshot, null, null, cancellation.Token));

        document.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Keywords_Strings_And_Comments_Are_Classified_By_Neutral_Kind()
    {
        (SourceBufferDocument document, _) = Create("public int x = 1; // note\n");

        CodeClassificationResult result = new PortableCSharpClassifier()
            .Classify(document.Snapshot, null, null, CancellationToken.None);
        CodeClassificationSpan[] spans = [.. result.GetLineSpans(0)];

        Assert.Contains(spans, s => s.Kind == CodeClassificationKind.Keyword);
        Assert.Contains(spans, s => s.Kind == CodeClassificationKind.NumericLiteral);
        Assert.Contains(spans, s => s.Kind == CodeClassificationKind.Comment);

        // Identifiers get no span: distinguishing a type from a local needs
        // binding, which is the semantic service's job.
        Assert.DoesNotContain(spans, s => s.Start == 11 && s.Length == 1);
        document.Dispose();
    }
}
