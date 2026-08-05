using Broiler.Code.Workspaces.Text;
using Broiler.Graphics;
using Broiler.UI;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.Standard;

namespace Broiler.Code.Core.Tests;

internal sealed class CodeEditorTestHost(BSize size) : IUiHost, IUiClipboardHost
{
    public BSize ViewportSize { get; } = size;

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

/// <summary>
/// An editor attached to a real session, so rendering, input routing, and
/// clipboard access go through the same path a host uses.
/// </summary>
internal sealed class CodeEditorScene : IDisposable
{
    public required UiSession Session { get; init; }

    public required StandardCodeEditor Editor { get; init; }

    public required SourceBufferDocument Document { get; init; }

    public required SourceBuffer Buffer { get; init; }

    public required CodeEditorTestHost Host { get; init; }

    public void Deconstruct(
        out StandardCodeEditor editor, out SourceBufferDocument document, out SourceBuffer buffer)
    {
        editor = Editor;
        document = Document;
        buffer = Buffer;
    }

    public void Dispose()
    {
        Session.Dispose();
        Document.Dispose();
    }
}

internal static class CodeEditorHarness
{
    public static CodeEditorScene Create(BSize size, string text)
    {
        var host = new CodeEditorTestHost(size);
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);

        var buffer = new SourceBuffer(text);
        var document = new SourceBufferDocument(buffer);
        var editor = new StandardCodeEditor
        {
            PreferredSize = size,
            Document = document,
            HasFocus = true,
        };

        session.AddRoot(editor);
        editor.Arrange(new BRect(0, 0, size.Width, size.Height));

        return new CodeEditorScene
        {
            Session = session,
            Editor = editor,
            Document = document,
            Buffer = buffer,
            Host = host,
        };
    }
}
