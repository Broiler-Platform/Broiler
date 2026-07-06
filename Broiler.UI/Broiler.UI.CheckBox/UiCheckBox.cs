using System;
using Broiler.Graphics;

namespace Broiler.UI.CheckBox;

public abstract class UiCheckBox : UiElement
{
    private string _text = string.Empty;
    private bool _isEnabled = true;
    private bool _isThreeState;
    private UiCheckState _checkState;
    private BSize _preferredSize = new(120, 32);
    private UiFlowDirection _flowDirection;

    public event EventHandler<UiCheckStateChangedEventArgs>? CheckStateChanged;

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

    public bool IsThreeState
    {
        get => _isThreeState;
        set
        {
            ThrowIfDisposed();
            if (_isThreeState == value)
                return;

            _isThreeState = value;
            if (!_isThreeState && _checkState == UiCheckState.Indeterminate)
                SetCheckState(UiCheckState.Unchecked);
            else
                Invalidate(UiInvalidationKind.Semantic);
        }
    }

    public UiCheckState CheckState
    {
        get => _checkState;
        set
        {
            ThrowIfDisposed();
            SetCheckState(value);
        }
    }

    public bool? IsChecked
    {
        get => CheckState switch
        {
            UiCheckState.Checked => true,
            UiCheckState.Indeterminate => null,
            _ => false,
        };
        set => CheckState = value switch
        {
            true => UiCheckState.Checked,
            null => UiCheckState.Indeterminate,
            _ => UiCheckState.Unchecked,
        };
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred checkbox size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public UiFlowDirection FlowDirection
    {
        get => _flowDirection;
        set
        {
            ThrowIfDisposed();
            if (_flowDirection == value)
                return;

            _flowDirection = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool Toggle()
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return false;

        CheckState = CheckState switch
        {
            UiCheckState.Unchecked => UiCheckState.Checked,
            UiCheckState.Checked when IsThreeState => UiCheckState.Indeterminate,
            UiCheckState.Checked => UiCheckState.Unchecked,
            _ => UiCheckState.Unchecked,
        };
        return true;
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.CheckBox,
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
        if (CheckState == UiCheckState.Checked)
            state |= UiSemanticState.Checked;
        if (CheckState == UiCheckState.Indeterminate)
            state |= UiSemanticState.Indeterminate;
        return state;
    }

    private void SetCheckState(UiCheckState value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));

        if (value == UiCheckState.Indeterminate && !IsThreeState)
            value = UiCheckState.Unchecked;
        if (_checkState == value)
            return;

        UiCheckState oldState = _checkState;
        _checkState = value;
        CheckStateChanged?.Invoke(this, new UiCheckStateChangedEventArgs(oldState, _checkState));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }
}
