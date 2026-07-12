using Broiler.Graphics;

namespace Broiler.UI.FormatCodeView.Tests;

public sealed class UiFormatCodeViewTests
{
    [Fact]
    public void Projection_Selection_Is_Directional_And_Clamped()
    {
        var view = new TestFormatCodeView { Projection = Project("abcdef") };
        FormatCodeViewSelectionChangedEventArgs? raised = null;
        view.SelectionChanged += (_, args) => raised = args;

        view.SetSelection(5, 2);

        Assert.Equal(5, view.SelectionAnchor);
        Assert.Equal(2, view.SelectionFocus);
        Assert.Equal(2, view.SelectionStart);
        Assert.Equal(3, view.SelectionLength);
        Assert.Equal("cde", view.GetSelectedText());
        Assert.Equal(5, raised?.Anchor);
        Assert.Equal(2, raised?.Focus);

        view.Projection = Project("x");
        Assert.Equal(1, view.SelectionAnchor);
        Assert.Equal(1, view.SelectionFocus);
    }

    [Fact]
    public void Copy_Uses_Only_The_Selected_Canonical_Text()
    {
        var host = new TestHost();
        using UiSession session = CreateSession(host);
        var view = new TestFormatCodeView { Projection = ProjectBold("Hello") };
        session.AddRoot(view);
        int start = view.Text.IndexOf("Hello", StringComparison.Ordinal);
        view.SetSelection(start, start + 5);

        Assert.True(view.CopySelection());
        Assert.Equal("Hello", host.ClipboardText);
    }

    [Fact]
    public void Search_Is_Ordinal_Directional_And_Wrapping()
    {
        var view = new TestFormatCodeView { Projection = Project("One two ONE") };

        Assert.True(view.Find("one"));
        Assert.Equal(0, view.SelectionStart);
        Assert.True(view.FindNext());
        Assert.Equal(8, view.SelectionStart);
        Assert.True(view.FindNext());
        Assert.Equal(0, view.SelectionStart);
        Assert.True(view.FindPrevious());
        Assert.Equal(8, view.SelectionStart);
        Assert.False(view.Find("missing"));
    }

    [Fact]
    public void Navigation_Uses_Typed_Projector_Mapping()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("x", new InlineStyle { Bold = true })]);
        var view = new TestFormatCodeView
        {
            Projection = new FormatCodeProjector().Project(document),
        };
        FormatCodeNavigationRequestedEventArgs? raised = null;
        view.NavigationRequested += (_, args) => raised = args;

        view.Activate(2);

        Assert.NotNull(raised);
        Assert.Equal(FormatCodeTokenKind.InlineCode, raised.Token?.Kind);
        Assert.Equal(document.Start, raised.Mapping.DocumentPosition);
        Assert.NotNull(raised.Mapping.AffectedRange);
    }

    [Fact]
    public void Semantics_Expose_ReadOnly_Text_Selection_And_Paragraph_State_Diagnostic()
    {
        ParagraphStyle style = ParagraphStyle.Default with { Alignment = TextAlignment.Center };
        RichTextDocument document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("x", InlineStyle.Default, style)]);
        var view = new TestFormatCodeView
        {
            Projection = new FormatCodeProjector().Project(document),
        };
        view.SetSelection(0, 7);

        UiSemanticNode node = view.GetSemanticNode();

        Assert.Equal(UiSemanticRole.FormatCodeView, node.Role);
        Assert.True(node.State.HasFlag(UiSemanticState.ReadOnly));
        Assert.True(node.State.HasFlag(UiSemanticState.Selected));
        Assert.Contains("engine state; visual rendering pending", node.Name);
        Assert.Equal(view.Text, node.TextInfo?.Value);
        Assert.False(node.TextInfo?.IsEditable);
    }

    [Fact]
    public void Pending_Formatting_Has_A_Distinct_Accessible_Description()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("x");
        var view = new TestFormatCodeView
        {
            Projection = new FormatCodeProjector().Project(
                document,
                new FormatCodeProjectionOptions
                {
                    PendingStyle = new FormatCodePendingStyle(
                        document.Start,
                        new InlineStyle { Bold = true }),
                }),
        };

        Assert.Contains("pending formatting", view.GetAccessibleTokenDescription());
        Assert.DoesNotContain("Pending", view.Text, StringComparison.Ordinal);
    }

    private static FormatCodeProjection Project(string text) =>
        new FormatCodeProjector().Project(RichTextDocument.FromPlainText(text));

    private static FormatCodeProjection ProjectBold(string text) =>
        new FormatCodeProjector().Project(RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create(text, new InlineStyle { Bold = true })]));

    private static UiSession CreateSession(TestHost host) =>
        new(host, new TestDispatcher(), new TestClock());

    private sealed class TestFormatCodeView : UiFormatCodeView
    {
        public void Activate(int offset) => ActivateAt(offset);
    }

    private sealed class TestDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action callback) => callback();
    }

    private sealed class TestClock : IUiClock
    {
        public UiTimestamp Now => new(TimeSpan.Zero);
    }

    private sealed class TestHost : IUiHost, IUiClipboardHost
    {
        public BSize ViewportSize => new(320, 160);

        public double Scale => 1;

        public string ClipboardText { get; private set; } = string.Empty;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }

        public bool TryGetText(out string text)
        {
            text = ClipboardText;
            return true;
        }

        public void SetText(string text) => ClipboardText = text;
    }
}
