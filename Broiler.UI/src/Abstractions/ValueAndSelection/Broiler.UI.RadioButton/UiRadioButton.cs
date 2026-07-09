using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;

namespace Broiler.UI.RadioButton;

public abstract class UiRadioButton : UiElement
{
    private string _text = string.Empty;
    private bool _isEnabled = true;
    private bool _isChecked;
    private bool _isApplyingGroup;
    private BSize _preferredSize = new(120, 32);
    private UiRadioGroupScope? _groupScope;
    private UiFlowDirection _flowDirection;

    public event EventHandler<UiRadioButtonCheckedChangedEventArgs>? CheckedChanged;

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

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            ThrowIfDisposed();
            SetChecked(value, updateGroup: true);
        }
    }

    public UiRadioGroupScope? GroupScope
    {
        get => _groupScope;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_groupScope, value))
                return;

            _groupScope = value;
            if (IsChecked)
                ApplyGroupSelection();
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred radio button size must be non-negative.");
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

    public bool Select()
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return false;

        IsChecked = true;
        return true;
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.RadioButton,
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
        if (IsChecked)
            state |= UiSemanticState.Checked | UiSemanticState.Selected;
        return state;
    }

    private void SetChecked(bool value, bool updateGroup)
    {
        if (_isChecked == value)
            return;

        bool oldValue = _isChecked;
        _isChecked = value;
        CheckedChanged?.Invoke(this, new UiRadioButtonCheckedChangedEventArgs(oldValue, _isChecked));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);

        if (_isChecked && updateGroup)
            ApplyGroupSelection();
    }

    private void ApplyGroupSelection()
    {
        if (_isApplyingGroup || GroupScope is null || Session is null)
            return;

        UiRadioButton[] peers = FindRadioButtons(Session.Roots)
            .Where(peer => !ReferenceEquals(peer, this) && ReferenceEquals(peer.GroupScope, GroupScope) && peer.IsChecked)
            .ToArray();

        _isApplyingGroup = true;
        try
        {
            foreach (UiRadioButton peer in peers)
                peer.SetChecked(false, updateGroup: false);
        }
        finally
        {
            _isApplyingGroup = false;
        }
    }

    private static IEnumerable<UiRadioButton> FindRadioButtons(IEnumerable<UiElement> roots)
    {
        foreach (UiElement root in roots)
        {
            if (root is UiRadioButton radio)
                yield return radio;

            foreach (UiRadioButton child in FindRadioButtons(root.Children))
                yield return child;
        }
    }
}
