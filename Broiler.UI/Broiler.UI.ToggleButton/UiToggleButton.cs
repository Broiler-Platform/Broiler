using System;
using Broiler.UI.Button;

namespace Broiler.UI.ToggleButton;

public abstract class UiToggleButton : UiButton
{
    private bool _isThreeState;
    private UiToggleState _toggleState;

    public event EventHandler<UiToggleStateChangedEventArgs>? ToggleStateChanged;

    public bool IsThreeState
    {
        get => _isThreeState;
        set
        {
            ThrowIfDisposed();
            if (_isThreeState == value)
                return;

            _isThreeState = value;
            if (!_isThreeState && _toggleState == UiToggleState.Indeterminate)
                SetToggleState(UiToggleState.Off);
            else
                Invalidate(UiInvalidationKind.Semantic);
        }
    }

    public UiToggleState ToggleState
    {
        get => _toggleState;
        set
        {
            ThrowIfDisposed();
            SetToggleState(value);
        }
    }

    public bool? IsChecked
    {
        get => ToggleState switch
        {
            UiToggleState.On => true,
            UiToggleState.Indeterminate => null,
            _ => false,
        };
        set => ToggleState = value switch
        {
            true => UiToggleState.On,
            null => UiToggleState.Indeterminate,
            _ => UiToggleState.Off,
        };
    }

    public bool Toggle()
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return false;

        ToggleState = ToggleState switch
        {
            UiToggleState.Off => UiToggleState.On,
            UiToggleState.On when IsThreeState => UiToggleState.Indeterminate,
            UiToggleState.On => UiToggleState.Off,
            _ => UiToggleState.Off,
        };
        return true;
    }

    protected override bool OnClicking(UiButtonActivationReason reason)
    {
        Toggle();
        return true;
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ToggleButton,
            Text,
            Bounds,
            CreateToggleSemanticState(),
            []);

    protected UiSemanticState CreateToggleSemanticState()
    {
        UiSemanticState state = CreateSemanticState();
        if (ToggleState == UiToggleState.On)
            state |= UiSemanticState.Checked;
        if (ToggleState == UiToggleState.Indeterminate)
            state |= UiSemanticState.Indeterminate;
        return state;
    }

    private void SetToggleState(UiToggleState value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));

        if (value == UiToggleState.Indeterminate && !IsThreeState)
            value = UiToggleState.Off;
        if (_toggleState == value)
            return;

        UiToggleState oldState = _toggleState;
        _toggleState = value;
        ToggleStateChanged?.Invoke(this, new UiToggleStateChangedEventArgs(oldState, _toggleState));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }
}
