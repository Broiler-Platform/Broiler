using Broiler.Graphics;
using Broiler.UI;

namespace Broiler.Browser;

internal sealed class BrowserUiHost : IUiHost, IUiClipboardHost, IUiTextInputHost, IDisposable
{
    private readonly Func<BSize> _getViewportSize;
    private readonly Func<double> _getScale;
    private readonly Action _invalidate;
    private readonly Action<BRenderList> _present;
    private readonly Func<Action, bool> _post;
    private string _clipboardText = string.Empty;

    public BrowserUiHost(
        Func<BSize> getViewportSize,
        Func<double> getScale,
        Action invalidate,
        Action<BRenderList> present,
        Func<Action, bool> post)
    {
        _getViewportSize = getViewportSize ?? throw new ArgumentNullException(nameof(getViewportSize));
        _getScale = getScale ?? throw new ArgumentNullException(nameof(getScale));
        _invalidate = invalidate ?? throw new ArgumentNullException(nameof(invalidate));
        _present = present ?? throw new ArgumentNullException(nameof(present));
        _post = post ?? throw new ArgumentNullException(nameof(post));
    }

    public BSize ViewportSize => _getViewportSize();

    public double Scale => _getScale();

    public bool IsInvalidated { get; private set; } = true;

    public BRenderList? LastRenderList { get; private set; }

    public UiTextCaretInfo? LastCaret { get; private set; }

    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

    public bool Post(Action action) => _post(action);

    public void RequestInvalidate()
    {
        IsInvalidated = true;
        _invalidate();
    }

    public void Invalidate(UiInvalidation invalidation) => RequestInvalidate();

    public void Present(BRenderList renderList)
    {
        _present(renderList);
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
