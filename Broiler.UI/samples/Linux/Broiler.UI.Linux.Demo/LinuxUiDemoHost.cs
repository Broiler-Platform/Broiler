using Broiler.Graphics;

namespace Broiler.UI.Linux.Demo;

internal sealed class LinuxUiDemoHost : IUiHost, IUiClipboardHost, IUiTextInputHost, System.IDisposable
{
    private readonly IBroilerRenderer _renderer;
    private readonly IBroilerSurface _surface;
    private string _clipboardText = string.Empty;
    private long _frameIndex;

    public LinuxUiDemoHost(IBroilerRenderer renderer, IBroilerSurface surface)
    {
        _renderer = renderer;
        _surface = surface;
    }

    public BSize ViewportSize => _surface.Size;

    public double Scale => _surface.DpiScale;

    public bool IsInvalidated { get; private set; } = true;

    public BRenderList? LastRenderList { get; private set; }

    public BFrameContext LastFrameContext { get; private set; } = BFrameContext.Default;

    public UiTextCaretInfo? LastCaret { get; private set; }

    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

    public void Invalidate(UiInvalidation invalidation) => IsInvalidated = true;

    public void Present(BRenderList renderList)
    {
        LastFrameContext = new BFrameContext(BColor.White, _frameIndex++, BRenderOptions.Default);
        _renderer.Render(_surface, renderList, LastFrameContext);
        LastRenderList = renderList;
        IsInvalidated = false;
    }

    public bool TryGetText(out string text)
    {
        text = _clipboardText;
        return _clipboardText.Length > 0;
    }

    public void SetText(string text) => _clipboardText = text ?? string.Empty;

    public void PublishCaret(UiTextCaretInfo caret) => LastCaret = caret;

    public void ClearCaret(UiElement owner)
    {
        if (LastCaret?.Owner == owner)
            LastCaret = null;
    }

    public void Dispose()
    {
    }
}
