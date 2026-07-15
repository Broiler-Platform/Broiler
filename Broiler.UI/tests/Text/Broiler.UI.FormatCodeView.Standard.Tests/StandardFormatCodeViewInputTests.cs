using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Text;

namespace Broiler.UI.FormatCodeView.Standard.Tests;

public sealed class StandardFormatCodeViewInputTests
{
    [Fact]
    public void Committed_Ime_Text_Raises_One_Structured_Edit()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("hello"));
        scene.View.IsEditable = true;
        scene.Session.SetFocus(scene.View);
        scene.View.SetSelection(1, 4);
        var edits = new List<FormatCodeEditRequestedEventArgs>();
        scene.View.EditRequested += (_, args) => edits.Add(args);

        Assert.True(scene.Route.Dispatch(new TextCompositionEvent(
            FormatCodeViewStandardHarness.Header("ime"),
            "ni",
            TextCompositionState.Updated)));
        Assert.Equal("ni", scene.View.CompositionText);
        Assert.Empty(edits);

        Assert.True(scene.Route.Dispatch(new TextCompositionEvent(
            FormatCodeViewStandardHarness.Header("ime"),
            "你",
            TextCompositionState.Committed)));
        ReplaceFormatCodeTextIntent intent = Assert.IsType<ReplaceFormatCodeTextIntent>(
            Assert.Single(edits).Intent);
        Assert.Equal("你", intent.Text);
        Assert.Empty(scene.View.CompositionText);
    }

    [Fact]
    public void Backspace_On_Code_Requests_Semantic_Removal_Not_Bracket_Deletion()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("x", new InlineStyle { Bold = true })]);
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100), new FormatCodeProjector().Project(document));
        scene.View.IsEditable = true;
        scene.Session.SetFocus(scene.View);
        FormatCodeEditRequestedEventArgs? edit = null;
        scene.View.EditRequested += (_, args) => edit = args;
        int afterOpenCode = scene.View.Projection!.Tokens[0].ProjectedLength;
        scene.View.SetSelection(afterOpenCode, afterOpenCode);

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Key(
            "Backspace", BVirtualKey.Back)));
        Assert.Equal(FormatCodeProperty.Bold, edit?.Token?.EditDescriptor?.Property);
        Assert.IsType<ApplyFormatCodeInlineIntent>(edit?.Intent);
    }
    [Fact]
    public void Pointer_Click_Requests_Typed_Navigation_And_Drag_Selects()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("hello", new InlineStyle { Bold = true })]);
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            new FormatCodeProjector().Project(document));
        scene.Session.RenderFrame();
        FormatCodeNavigationRequestedEventArgs? navigation = null;
        scene.View.NavigationRequested += (_, args) => navigation = args;

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.MouseDown(12, 12)));
        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.MouseMove(100, 12)));
        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.MouseUp(100, 12)));

        Assert.Same(scene.View, scene.Session.FocusedElement);
        Assert.NotNull(navigation);
        Assert.True(scene.View.HasSelection);
        Assert.True(document.IsValid(navigation.Mapping.DocumentPosition));
    }

    [Fact]
    public void Keyboard_Navigation_Preserves_Directional_Selection_And_Copies()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("abcdef"));
        scene.Session.SetFocus(scene.View);
        scene.View.SetSelection(5, 5);

        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("Left", BVirtualKey.Left, KeyboardModifierState.Shift));
        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("Left", BVirtualKey.Left, KeyboardModifierState.Shift));

        Assert.Equal(5, scene.View.SelectionAnchor);
        Assert.Equal(3, scene.View.SelectionFocus);
        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("C", BVirtualKey.C, KeyboardModifierState.Control)));
        Assert.Equal("de", scene.Host.ClipboardText);

        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("A", BVirtualKey.A, KeyboardModifierState.Control));
        Assert.Equal(scene.View.Text.Length, scene.View.SelectionLength);
    }

    [Fact]
    public void Search_Exit_And_Activation_Are_Exposed_To_The_Host()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("one two one"));
        scene.Session.SetFocus(scene.View);
        int searchRequests = 0;
        int exitRequests = 0;
        int navigationRequests = 0;
        scene.View.SearchRequested += (_, _) => searchRequests++;
        scene.View.ExitRequested += (_, _) => exitRequests++;
        scene.View.NavigationRequested += (_, _) => navigationRequests++;

        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("F", 0x46, KeyboardModifierState.Control));
        scene.View.Find("one");
        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("F3", 0x72));
        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("Enter", BVirtualKey.Enter));
        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("Escape", BVirtualKey.Escape));

        Assert.Equal(1, searchRequests);
        Assert.Equal(1, exitRequests);
        Assert.Equal(1, navigationRequests);
        Assert.Equal(8, scene.View.SelectionStart);
    }

    [Fact]
    public void Wheel_Scrolls_Overflowing_Content()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(160, 70),
            FormatCodeViewStandardHarness.Project(string.Join('\n', Enumerable.Repeat("line", 30))));
        scene.Session.RenderFrame();

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Wheel(20, 20, -2)));
        Assert.True(scene.View.VerticalScrollOffset > 0);
    }
}
