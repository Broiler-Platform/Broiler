using Broiler.Graphics;

namespace Broiler.UI.RichEdit.Tests;

/// <summary>A recording host that also provides a plain-text clipboard.</summary>
internal sealed class FakeRichEditHost : IUiHost, IUiClipboardHost
{
    private string? _clipboardText;

    public BSize ViewportSize { get; set; } = new(800, 600);

    public double Scale { get; set; } = 1.0;

    public List<UiInvalidation> Invalidations { get; } = [];

    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

    public void Invalidate(UiInvalidation invalidation) => Invalidations.Add(invalidation);

    public void Present(BRenderList renderList)
    {
    }

    public bool TryGetText(out string text)
    {
        text = _clipboardText ?? string.Empty;
        return _clipboardText is not null;
    }

    public void SetText(string text) => _clipboardText = text;
}

internal sealed class InlineDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public void Post(Action callback) => callback();
}

internal sealed class ZeroClock : IUiClock
{
    public UiTimestamp Now => default;
}

/// <summary>A concrete <see cref="UiRichEdit"/> that records raised events in order.</summary>
internal sealed class FakeRichEdit : UiRichEdit
{
    public FakeRichEdit()
    {
        DocumentChanged += (_, _) => Events.Add("DocumentChanged");
        SelectionChanged += (_, _) => Events.Add("SelectionChanged");
        CommandExecuted += (_, e) => Events.Add("CommandExecuted:" + e.Command);
        Submitted += (_, _) => Events.Add("Submitted");
    }

    public List<string> Events { get; } = [];
}

internal static class RichEditHarness
{
    public static UiSession CreateSession(out FakeRichEditHost host)
    {
        host = new FakeRichEditHost();
        return new UiSession(host, new InlineDispatcher(), new ZeroClock());
    }

    /// <summary>Creates a session, attaches a fresh control, and clears setup noise.</summary>
    public static (UiSession Session, FakeRichEdit Edit, FakeRichEditHost Host) Attach()
    {
        UiSession session = CreateSession(out FakeRichEditHost host);
        var edit = new FakeRichEdit();
        session.AddRoot(edit);
        host.Invalidations.Clear();
        edit.Events.Clear();
        return (session, edit, host);
    }
}
