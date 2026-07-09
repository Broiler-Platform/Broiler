using System;
using Broiler.Graphics;

namespace Broiler.UI.Button;

public abstract class UiButton : UiElement
{
    private string _text = string.Empty;
    private string? _commandName;
    private bool _isEnabled = true;
    private bool _isDefault;
    private bool _isCancel;
    private BSize _preferredSize = new(96, 32);

    public event EventHandler<UiButtonClickEventArgs>? Clicked;

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

    public string? CommandName
    {
        get => _commandName;
        set
        {
            ThrowIfDisposed();
            if (StringComparer.Ordinal.Equals(_commandName, value))
                return;

            _commandName = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            ThrowIfDisposed();
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            ThrowIfDisposed();
            if (_isDefault == value)
                return;

            _isDefault = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsCancel
    {
        get => _isCancel;
        set
        {
            ThrowIfDisposed();
            if (_isCancel == value)
                return;

            _isCancel = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred button size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public void Click(UiButtonActivationReason reason = UiButtonActivationReason.Programmatic)
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return;

        if (!OnClicking(reason))
            return;

        Clicked?.Invoke(this, new UiButtonClickEventArgs(reason));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    protected virtual bool OnClicking(UiButtonActivationReason reason)
    {
        return true;
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Button,
            Text,
            Bounds,
            CreateSemanticState(),
            []);

    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        return state;
    }
}
