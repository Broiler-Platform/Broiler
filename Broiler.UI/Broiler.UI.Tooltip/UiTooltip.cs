using System;
using Broiler.Graphics;
using Broiler.UI.Window;

namespace Broiler.UI.Tooltip;

public abstract class UiTooltip : UiWindow
{
    private string _text = string.Empty;
    private BRect _targetBounds = BRect.Empty;
    private TimeSpan _initialDelay = TimeSpan.FromMilliseconds(500);
    private TimeSpan? _dismissAfter = TimeSpan.FromSeconds(8);
    private TimeSpan? _requestedAt;
    private bool _isTooltipOpen;
    private BRect _tooltipBounds = BRect.Empty;

    public string Text
    {
        get => _text;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_text, value))
                return;

            _text = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BRect TargetBounds
    {
        get => _targetBounds;
        private set
        {
            if (_targetBounds == value)
                return;

            _targetBounds = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public TimeSpan InitialDelay
    {
        get => _initialDelay;
        set
        {
            ThrowIfDisposed();
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "Tooltip delay must be non-negative.");
            _initialDelay = value;
        }
    }

    public TimeSpan? DismissAfter
    {
        get => _dismissAfter;
        set
        {
            ThrowIfDisposed();
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "Tooltip timeout must be non-negative.");
            _dismissAfter = value;
        }
    }

    public bool IsTooltipOpen
    {
        get => _isTooltipOpen;
        private set
        {
            if (_isTooltipOpen == value)
                return;

            _isTooltipOpen = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BRect TooltipBounds
    {
        get => _tooltipBounds;
        protected set => _tooltipBounds = value;
    }

    public void Start(BRect targetBounds)
    {
        ThrowIfDisposed();
        TargetBounds = targetBounds;
        _requestedAt = Session?.Clock.Now.Elapsed ?? TimeSpan.Zero;
        IsTooltipOpen = false;
    }

    public void Hide()
    {
        ThrowIfDisposed();
        _requestedAt = null;
        IsTooltipOpen = false;
    }

    public bool UpdateVisibility()
    {
        ThrowIfDisposed();
        if (_requestedAt is null)
            return false;

        TimeSpan elapsed = (Session?.Clock.Now.Elapsed ?? TimeSpan.Zero) - _requestedAt.Value;
        if (DismissAfter is not null && elapsed >= InitialDelay + DismissAfter.Value)
        {
            Hide();
            return true;
        }

        if (!IsTooltipOpen && elapsed >= InitialDelay)
        {
            IsTooltipOpen = true;
            return true;
        }

        return false;
    }

    protected override bool OnInput(UiInputEvent input)
    {
        Hide();
        return false;
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Tooltip,
            IsTooltipOpen ? Text : string.Empty,
            TooltipBounds,
            IsTooltipOpen && Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None,
            []);
}
