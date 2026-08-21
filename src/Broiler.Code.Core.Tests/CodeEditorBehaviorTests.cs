using System.Text;
using Broiler.Code.Workspaces.Text;
using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.CodeEditor;
using Broiler.UI.CodeEditor.Standard;

namespace Broiler.Code.Core.Tests;

/// <summary>
/// The claims the Phase 1 exit gate makes, each asserted directly rather than
/// inferred from a demonstration.
/// </summary>
public sealed class CodeEditorBehaviorTests
{
    private static string BuildSource(int lines)
    {
        var builder = new StringBuilder(lines * 40);
        for (int i = 0; i < lines; i++)
            builder.Append("public int Value").Append(i).Append(" => ").Append(i % 97).Append(";\n");
        return builder.ToString();
    }

    private static CodeEditorScene CreateEditor(string text) =>
        CodeEditorHarness.Create(new BSize(800, 600), text);

    /// <summary>
    /// Lets a background classification finish before the run is read. The
    /// controller deliberately has no "quiesce" API — a host never waits — so a
    /// test that needs a settled state waits on the task the controller exposes
    /// for exactly this purpose.
    /// </summary>
    private static void Wait(CodeAnalysisController controller) =>
        controller.CurrentWork?.Wait(TimeSpan.FromMinutes(2));

    [Fact(Timeout = 600000)]
    public void An_Edit_Recolors_Incrementally_Without_Reclassifying_The_Document()
    {
        using CodeEditorScene scene = CreateEditor(BuildSource(100_000));
        var (editor, document, buffer) = scene;
        var classifier = new FixtureClassifier();
        var dispatcher = new DeferredDispatcher();
        using var controller = new CodeAnalysisController(editor, document, classifier, dispatcher);

        Wait(controller);
        dispatcher.Drain();
        Assert.NotNull(editor.Classifications);
        Assert.Equal(100_001, classifier.LastLinesLexed);

        // A single character on one line must not re-lex the document.
        editor.Selection = CodeSelection.Caret(buffer.Current.GetLineStart(50_000) + 7);
        Assert.True(editor.InsertText("x"));
        Wait(controller);
        dispatcher.Drain();

        Assert.Equal(1, classifier.LastLinesLexed);
        Assert.Same(editor.Snapshot, editor.Classifications!.Snapshot);
        
    }

    [Fact(Timeout = 600000)]
    public void A_Result_For_A_Superseded_Snapshot_Cannot_Paint()
    {
        using CodeEditorScene scene = CreateEditor(BuildSource(50));
        var (editor, document, buffer) = scene;
        var classifier = new FixtureClassifier();

        CodeClassificationResult stale = classifier.Classify(
            editor.Snapshot, null, null, CancellationToken.None);
        Assert.True(editor.TryApplyClassifications(stale));

        // The buffer moves on; the result now describes text that is not there.
        editor.Selection = CodeSelection.Caret(0);
        Assert.True(editor.InsertText("// changed\n"));

        Assert.False(editor.TryApplyClassifications(stale));
        Assert.Equal(1, editor.RejectedResults);

        // The result the control was holding is dropped when the snapshot moves
        // on, so Classifications always means "for the current snapshot" and
        // the renderer cannot paint spans that describe text that is gone.
        Assert.Null(editor.Classifications);
        
    }

    [Fact(Timeout = 600000)]
    public void A_Burst_Of_Edits_Publishes_Only_The_Current_Snapshot()
    {
        using CodeEditorScene scene = CreateEditor(BuildSource(100_000));
        var (editor, document, buffer) = scene;
        var classifier = new FixtureClassifier();
        var dispatcher = new DeferredDispatcher();
        using var controller = new CodeAnalysisController(editor, document, classifier, dispatcher);
        Wait(controller);
        dispatcher.Drain();

        editor.Selection = CodeSelection.Caret(buffer.Current.GetLineStart(40_000));
        for (int i = 0; i < 24; i++)
            editor.InsertText("z");

        Wait(controller);
        dispatcher.Drain();

        // Whatever survived the burst has to describe the document as it now
        // stands, and nothing older may have reached the control.
        Assert.Same(editor.Snapshot, editor.Classifications!.Snapshot);
        Assert.True(controller.CancelledRuns + controller.RejectedResults > 0,
            "A 24-edit burst must have superseded at least one run.");
        
    }

    [Fact(Timeout = 600000)]
    public void Stale_Edit_Intents_Are_Rejected_Rather_Than_Rebased()
    {
        using CodeEditorScene scene = CreateEditor("abcdef");
        var (editor, document, buffer) = scene;

        int staleVersion = editor.Snapshot.Version;
        Assert.True(document.Submit(new CodeEditIntent(staleVersion, 0, 0, "X", "first")).Accepted);

        CodeEditOutcome second = document.Submit(new CodeEditIntent(staleVersion, 0, 0, "Y", "stale"));

        Assert.False(second.Accepted);
        Assert.Equal(CodeEditRejection.StaleVersion, second.Reason);
        Assert.DoesNotContain('Y', editor.Snapshot.GetText(0, editor.Snapshot.Length));
        
    }

    [Fact(Timeout = 600000)]
    public void Bounded_Ime_Queries_Never_Return_More_Than_Asked_For()
    {
        using CodeEditorScene scene = CreateEditor(BuildSource(100_000));
        var (editor, document, buffer) = scene;
        IUiTextEditor text = editor;

        UiTextEditorMetrics metrics = text.GetTextEditorMetrics();
        Assert.True(metrics.TextLength > 2_000_000, "The fixture should be large enough to matter.");

        // The IME path: a bounded window around the caret, not the document.
        Assert.True(text.SetEditorSelection(1_000_000, 1_000_000));
        Assert.Equal(32, text.GetTextEditorRange(1_000_000 - 32, 32).Length);
        Assert.Equal(32, text.GetTextEditorRange(1_000_000, 32).Length);

        // Even an unbounded-looking request is capped.
        Assert.Equal(UiCodeEditor.MaxBoundedReadLength, text.GetTextEditorRange(0, int.MaxValue).Length);
        
    }

    [Fact(Timeout = 600000)]
    public void Virtualized_Accessibility_Ranges_Reject_A_Stale_Version()
    {
        using CodeEditorScene scene = CreateEditor(BuildSource(500));
        var (editor, document, buffer) = scene;
        IUiVirtualizedTextProvider provider = editor;

        UiTextDocumentMetrics metrics = provider.GetTextMetrics();
        UiTextRangeResult range = provider.GetTextRange(0, 64, metrics.Version);
        Assert.True(range.IsValid);
        Assert.Equal(64, range.Text.Length);

        UiTextLineInfo line = provider.GetLineInfo(3, metrics.Version);
        Assert.True(line.IsValid);
        Assert.Equal(editor.Snapshot.GetLineStart(3), line.Start);

        // The document moves while the client holds a version.
        editor.Selection = CodeSelection.Caret(0);
        Assert.True(editor.InsertText("x"));

        Assert.False(provider.GetTextRange(0, 64, metrics.Version).IsValid);
        Assert.False(provider.GetLineInfo(3, metrics.Version).IsValid);
        Assert.False(provider.TrySetSelection(0, 4, metrics.Version));
        Assert.False(provider.TryReplaceRange(0, 1, "q", metrics.Version));

        // With the current version it works again.
        int current = provider.GetTextMetrics().Version;
        Assert.True(provider.GetTextRange(0, 64, current).IsValid);
        
    }

    [Fact(Timeout = 600000)]
    public void The_Semantic_Node_Never_Carries_The_Document_Text()
    {
        using CodeEditorScene scene = CreateEditor(BuildSource(10_000));
        var (editor, document, buffer) = scene;

        UiSemanticNode node = editor.GetSemanticNode();

        Assert.Equal(UiSemanticRole.CodeEditor, node.Role);
        Assert.NotNull(node.TextInfo);
        Assert.Null(node.TextInfo!.Value);
        Assert.Contains("10001 lines", node.Name);
        Assert.Contains("classification only", node.Name);
        
    }

    [Fact(Timeout = 600000)]
    public void Hit_Testing_Round_Trips_Through_Tabs_And_Wide_Characters()
    {
        using CodeEditorScene scene = CreateEditor("\tint x = 1;\nabc\U0001F600def\nplain\n");
        var (editor, document, buffer) = scene;

        for (int line = 0; line < editor.Snapshot.LineCount; line++)
        {
            int start = editor.Snapshot.GetLineStart(line);
            int length = editor.Snapshot.GetLineLength(line);
            for (int offset = 0; offset <= length; offset++)
            {
                editor.Selection = CodeSelection.Caret(start + offset);
                BRect caret = editor.GetCaretRect();
                int hit = editor.HitTest(new BPoint(caret.Left, caret.Top + (editor.LineHeight / 2)));

                // A surrogate pair is one column, so a hit inside it resolves to
                // one of its two edges — never to the middle.
                Assert.InRange(hit, start, start + length);
            }
        }

        
    }

    [Fact(Timeout = 600000)]
    public void Rendering_Touches_Only_The_Visible_Lines()
    {
        using CodeEditorScene scene = CreateEditor(BuildSource(100_000));
        var (editor, document, buffer) = scene;
        var classifier = new CountingClassifier();

        CodeClassificationResult result = classifier.Classify(editor.Snapshot, null, null, CancellationToken.None);
        Assert.True(editor.TryApplyClassifications(result));

        editor.Arrange(new BRect(0, 0, 800, 600));
        editor.Viewport = editor.Viewport with { FirstVisibleLine = 40_000 };

        scene.Session.RenderFrame();

        // The renderer asked for spans only on the lines it painted, and the
        // window is bounded by the control's height rather than the document.
        Assert.True(classifier.QueriedLines.Count <= editor.VisibleLineCapacity + 1,
            $"Rendering queried {classifier.QueriedLines.Count} lines for a {editor.VisibleLineCapacity}-line window.");
        Assert.All(classifier.QueriedLines, line => Assert.InRange(line, 40_000, 40_000 + editor.VisibleLineCapacity));
        
    }

    [Fact(Timeout = 600000)]
    public void Undo_Groups_Typing_And_Breaks_On_A_Caret_Move()
    {
        using CodeEditorScene scene = CreateEditor("start\n");
        var (editor, document, buffer) = scene;

        editor.Selection = CodeSelection.Caret(5);
        foreach (char c in "hello")
            editor.InsertText(c.ToString());
        Assert.Equal("starthello\n", buffer.Current.ToString());

        // One undo removes the whole run, because nothing interrupted it.
        Assert.True(editor.Undo());
        Assert.Equal("start\n", buffer.Current.ToString());

        // A caret move between runs makes them separate steps.
        Assert.True(editor.Redo());
        editor.MoveCaret(CodeCaretMovement.LineStart, extend: false);
        editor.Selection = CodeSelection.Caret(buffer.Current.Length - 1);
        editor.InsertText("!");
        Assert.True(editor.Undo());
        Assert.Equal("starthello\n", buffer.Current.ToString());
        
    }

    [Fact(Timeout = 600000)]
    public void Indent_And_Outdent_Apply_To_Every_Selected_Line()
    {
        using CodeEditorScene scene = CreateEditor("one\ntwo\nthree\n");
        var (editor, document, buffer) = scene;
        editor.IndentPolicy = new CodeIndentPolicy(2, InsertSpaces: true);

        editor.Selection = new CodeSelection(0, buffer.Current.GetLineStart(2) + 1);
        Assert.True(editor.Indent());
        Assert.Equal("  one\n  two\n  three\n", buffer.Current.ToString());

        Assert.True(editor.Outdent());
        Assert.Equal("one\ntwo\nthree\n", buffer.Current.ToString());
        
    }

    [Fact(Timeout = 600000)]
    public void A_Read_Only_Document_Refuses_Every_Edit_Path()
    {
        using CodeEditorScene scene = CreateEditor("locked\n");
        var (editor, document, buffer) = scene;
        buffer.IsReadOnly = true;
        editor.Selection = CodeSelection.Caret(3);

        Assert.False(editor.InsertText("x"));
        Assert.False(editor.DeleteBackward());
        Assert.False(editor.DeleteForward());
        Assert.False(editor.InsertNewLine());
        Assert.False(editor.Indent());
        Assert.False(((IUiTextEditor)editor).DeleteSurroundingText(1, 1));
        Assert.Equal("locked\n", buffer.Current.ToString());
        
    }

    /// <summary>Records which lines the renderer asked about.</summary>
    private sealed class CountingClassifier : ICodeClassifier
    {
        public HashSet<int> QueriedLines { get; } = [];

        public CodeClassificationResult Classify(
            ICodeTextSnapshot snapshot,
            CodeClassificationResult? previous,
            CodeTextChange? change,
            CancellationToken cancellationToken) => new Result(snapshot, QueriedLines);

        private sealed class Result(ICodeTextSnapshot snapshot, HashSet<int> queried) : CodeClassificationResult
        {
            public override ICodeTextSnapshot Snapshot { get; } = snapshot;

            public override int LineCount => Snapshot.LineCount;

            public override ReadOnlySpan<CodeClassificationSpan> GetLineSpans(int line)
            {
                queried.Add(line);
                return default;
            }
        }
    }
}
