using System.Text;
using Broiler.Code.Workspaces.Text;

namespace Broiler.Code.Workspaces.Tests;

/// <summary>
/// The snapshot is a balanced tree whose shape changes as it rebalances, so
/// every property here is checked against the obvious string implementation
/// rather than against a hand-written expectation. A tree that agrees with
/// <see cref="string"/> for every input is correct; one that agrees with a
/// hand-picked example proves nothing about the shapes it has not been shown.
/// </summary>
public sealed class TextSnapshotTests
{
    public static IEnumerable<object[]> LineEndingSamples() => new[]
    {
        new object[] { string.Empty },
        new object[] { "a" },
        new object[] { "a\n" },
        new object[] { "\n" },
        new object[] { "\r" },
        new object[] { "\r\n" },
        new object[] { "a\nb\nc" },
        new object[] { "a\r\nb\r\nc" },
        new object[] { "a\rb\rc" },
        new object[] { "a\r\n\r\nb" },
        new object[] { "\r\n\n\r" },
        new object[] { "mixed\r\nline\nendings\rhere\r\n" },
        new object[] { "trailing\n\n\n" },
    };

    [Theory]
    [MemberData(nameof(LineEndingSamples))]
    public void Line_Index_Agrees_With_Splitting_The_String(string text)
    {
        TextSnapshot snapshot = TextSnapshot.Create(text);
        string[] expected = SplitLines(text);

        Assert.Equal(expected.Length, snapshot.LineCount);
        Assert.Equal(text.Length, snapshot.Length);
        Assert.Equal(text, snapshot.ToString());

        int offset = 0;
        for (int line = 0; line < expected.Length; line++)
        {
            Assert.Equal(offset, snapshot.GetLineStart(line));
            Assert.Equal(expected[line], snapshot.GetLineText(line));
            Assert.Equal(expected[line].Length, snapshot.GetLineLength(line));
            offset += expected[line].Length + TerminatorLength(text, offset + expected[line].Length);
        }
    }

    [Theory]
    [MemberData(nameof(LineEndingSamples))]
    public void Every_Position_Maps_To_The_Line_That_Contains_It(string text)
    {
        TextSnapshot snapshot = TextSnapshot.Create(text);

        for (int position = 0; position <= text.Length; position++)
        {
            int line = snapshot.GetLineFromPosition(position);
            int start = snapshot.GetLineStart(line);
            int end = line + 1 < snapshot.LineCount ? snapshot.GetLineStart(line + 1) : snapshot.Length;
            Assert.InRange(position, start, end);
        }
    }

    /// <summary>
    /// The case the tree makes easy to get wrong: a CR ending one node and an LF
    /// starting the next are one line break, and which node holds which
    /// character depends on where the tree happens to have split.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Crlf_Split_Across_Nodes_Counts_As_One_Break()
    {
        // Long enough to be many leaves, so the CR and LF land in different ones.
        var builder = new StringBuilder();
        for (int i = 0; i < 4_000; i++)
            builder.Append("line").Append(i).Append("\r\n");
        string text = builder.ToString();

        TextSnapshot snapshot = TextSnapshot.Create(text);
        Assert.Equal(4_001, snapshot.LineCount);

        // Now force the split to fall exactly between a CR and its LF by
        // rebuilding the document through an edit at that position.
        int crPosition = text.IndexOf('\r');
        var buffer = new SourceBuffer(text);
        buffer.Apply(EditTransaction.Insert(buffer.Current, crPosition + 1, string.Empty, "no-op"));
        Assert.Equal(4_001, buffer.Current.LineCount);
    }

    [Fact(Timeout = 600000)]
    public void Inserting_Between_Cr_And_Lf_Creates_A_Second_Line_Break()
    {
        var buffer = new SourceBuffer("a\r\nb");
        Assert.Equal(2, buffer.Current.LineCount);

        buffer.Apply(EditTransaction.Insert(buffer.Current, 2, "X", "split-crlf"));

        Assert.Equal("a\rX\nb", buffer.Current.ToString());
        Assert.Equal(3, buffer.Current.LineCount);
        Assert.Equal("a", buffer.Current.GetLineText(0));
        Assert.Equal("X", buffer.Current.GetLineText(1));
        Assert.Equal("b", buffer.Current.GetLineText(2));
    }

    [Fact(Timeout = 600000)]
    public void Deleting_Between_Cr_And_Lf_Rejoins_Them_Into_One_Break()
    {
        var buffer = new SourceBuffer("a\rX\nb");
        Assert.Equal(3, buffer.Current.LineCount);

        buffer.Apply(EditTransaction.Delete(buffer.Current, 2, 1, "rejoin-crlf"));

        Assert.Equal("a\r\nb", buffer.Current.ToString());
        Assert.Equal(2, buffer.Current.LineCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(1024)]
    [InlineData(4096)]
    public void Random_Edits_Track_The_Equivalent_String_Operations(int editSize)
    {
        // A deterministic seed: a fuzz test that cannot be replayed is a report
        // of a failure rather than a way to fix one.
        var random = new Random(20260805 + editSize);
        var reference = new StringBuilder(BuildSource(600));
        var buffer = new SourceBuffer(reference.ToString());

        for (int step = 0; step < 300; step++)
        {
            int start = random.Next(reference.Length + 1);
            bool insert = reference.Length == 0 || random.Next(3) != 0;

            if (insert)
            {
                string inserted = RandomText(random, random.Next(1, editSize + 1));
                reference.Insert(start, inserted);
                Assert.True(buffer.Apply(
                    EditTransaction.Insert(buffer.Current, start, inserted, "fuzz")).Accepted);
            }
            else
            {
                int length = random.Next(1, Math.Min(editSize, reference.Length - start) + 1);
                reference.Remove(start, length);
                Assert.True(buffer.Apply(
                    EditTransaction.Delete(buffer.Current, start, length, "fuzz")).Accepted);
            }

            if (step % 25 != 0)
                continue;

            string expected = reference.ToString();
            Assert.Equal(expected, buffer.Current.ToString());
            Assert.Equal(SplitLines(expected).Length, buffer.Current.LineCount);
        }

        Assert.Equal(reference.ToString(), buffer.Current.ToString());
    }

    [Fact(Timeout = 600000)]
    public void Get_Text_And_Copy_To_Agree_With_Substring_At_Every_Range()
    {
        string text = BuildSource(400);
        TextSnapshot snapshot = TextSnapshot.Create(text);
        var random = new Random(4242);

        for (int i = 0; i < 500; i++)
        {
            int start = random.Next(text.Length + 1);
            int length = random.Next(text.Length - start + 1);

            Assert.Equal(text.Substring(start, length), snapshot.GetText(start, length));

            var destination = new char[length];
            snapshot.CopyTo(start, length, destination);
            Assert.Equal(text.Substring(start, length), new string(destination));
        }
    }

    [Fact(Timeout = 600000)]
    public void For_Each_Chunk_Visits_The_Range_In_Order_And_Exactly_Once()
    {
        string text = BuildSource(500);
        TextSnapshot snapshot = TextSnapshot.Create(text);
        var random = new Random(99);

        for (int i = 0; i < 100; i++)
        {
            int start = random.Next(text.Length + 1);
            int length = random.Next(text.Length - start + 1);

            var seen = new StringBuilder();
            int expectedPosition = start;
            snapshot.ForEachChunk(start, length, (chunk, position) =>
            {
                Assert.Equal(expectedPosition, position);
                expectedPosition += chunk.Length;
                seen.Append(chunk);
            });

            Assert.Equal(text.Substring(start, length), seen.ToString());
            Assert.Equal(start + length, expectedPosition);
        }
    }

    [Fact(Timeout = 600000)]
    public void Indexer_Agrees_With_The_String_At_Every_Position()
    {
        string text = BuildSource(200);
        TextSnapshot snapshot = TextSnapshot.Create(text);
        for (int i = 0; i < text.Length; i++)
            Assert.Equal(text[i], snapshot[i]);
    }

    [Fact(Timeout = 600000)]
    public void Caret_Movement_Does_Not_Split_Surrogate_Pairs_Or_Combining_Marks()
    {
        // Emoji with a skin-tone modifier, a combining acute accent, and a
        // regional-indicator flag: all multi-char single caret stops.
        const string text = "a\U0001F44D\U0001F3FB é \U0001F1E9\U0001F1EA z";
        TextSnapshot snapshot = TextSnapshot.Create(text);

        var stops = new List<int>();
        int position = 0;
        while (position < snapshot.Length)
        {
            stops.Add(position);
            int next = snapshot.GetNextCaretPosition(position);
            Assert.True(next > position, "Caret movement must make progress.");
            position = next;
        }

        stops.Add(snapshot.Length);

        // Never lands between a high and a low surrogate.
        foreach (int stop in stops)
        {
            if (stop > 0 && stop < text.Length)
                Assert.False(char.IsLowSurrogate(text[stop]), $"Caret stopped inside a surrogate pair at {stop}.");
        }

        // Walking back reproduces the same stops.
        var backwards = new List<int>();
        position = snapshot.Length;
        while (position > 0)
        {
            backwards.Add(position);
            int previous = snapshot.GetPreviousCaretPosition(position);
            Assert.True(previous < position, "Backward caret movement must make progress.");
            position = previous;
        }

        backwards.Add(0);
        backwards.Reverse();
        Assert.Equal(stops, backwards);
    }

    [Fact(Timeout = 600000)]
    public void A_Large_Document_Indexes_Without_Materializing_It()
    {
        string text = BuildSource(100_000);
        TextSnapshot snapshot = TextSnapshot.Create(text);

        Assert.Equal(SplitLines(text).Length, snapshot.LineCount);
        Assert.Equal(text.Length, snapshot.Length);

        // Spot-check line starts against the reference, including the last line.
        string[] lines = SplitLines(text);
        int offset = 0;
        for (int line = 0; line < lines.Length; line++)
        {
            if (line % 997 == 0 || line == lines.Length - 1)
            {
                Assert.Equal(offset, snapshot.GetLineStart(line));
                Assert.Equal(lines[line], snapshot.GetLineText(line));
            }

            offset += lines[line].Length + TerminatorLength(text, offset + lines[line].Length);
        }
    }

    internal static string BuildSource(int lines)
    {
        var builder = new StringBuilder(lines * 40);
        for (int i = 0; i < lines; i++)
        {
            builder.Append("public int Value").Append(i).Append(" => ").Append(i % 97);
            builder.Append(";  // line ").Append(i).Append('\n');
        }

        return builder.ToString();
    }

    private static string RandomText(Random random, int length)
    {
        const string alphabet = "abcXYZ 0123\t{}();\n\r\n\ré中";
        var builder = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            builder.Append(alphabet[random.Next(alphabet.Length)]);
        return builder.ToString();
    }

    /// <summary>
    /// Splits on CR, LF, and CRLF, keeping a trailing empty line. This is the
    /// reference the snapshot is judged against; <c>string.Split</c> alone gets
    /// CRLF wrong by producing an extra empty line.
    /// </summary>
    internal static string[] SplitLines(string text)
    {
        var lines = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is not ('\n' or '\r'))
                continue;

            lines.Add(text[start..i]);
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                i++;
            start = i + 1;
        }

        lines.Add(text[start..]);
        return [.. lines];
    }

    private static int TerminatorLength(string text, int position)
    {
        if (position >= text.Length)
            return 0;
        if (text[position] == '\r')
            return position + 1 < text.Length && text[position + 1] == '\n' ? 2 : 1;
        return text[position] == '\n' ? 1 : 0;
    }
}
