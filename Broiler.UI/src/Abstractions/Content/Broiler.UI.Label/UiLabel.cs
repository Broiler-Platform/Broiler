using System;
using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.UI.Label;

public abstract class UiLabel : UiElement
{
    private string _text = string.Empty;
    private BFontStyle _font = BFontStyle.Default;
    private BColor _foreground = BColor.Black;
    private UiTextWrapping _wrapping;
    private UiTextTrimming _trimming;
    private UiTextDirection _direction;
    private char? _accessKey;
    private UiElement? _target;

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

    public string DisplayText => StripAccessMarkers(Text);

    public BFontStyle Font
    {
        get => _font;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_font == value)
                return;

            _font = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public BColor Foreground
    {
        get => _foreground;
        set
        {
            ThrowIfDisposed();
            if (_foreground == value)
                return;

            _foreground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public UiTextWrapping Wrapping
    {
        get => _wrapping;
        set
        {
            ThrowIfDisposed();
            if (_wrapping == value)
                return;

            _wrapping = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public UiTextTrimming Trimming
    {
        get => _trimming;
        set
        {
            ThrowIfDisposed();
            if (_trimming == value)
                return;

            _trimming = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public UiTextDirection Direction
    {
        get => _direction;
        set
        {
            ThrowIfDisposed();
            if (_direction == value)
                return;

            _direction = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public char? AccessKey
    {
        get => _accessKey;
        set
        {
            ThrowIfDisposed();
            if (_accessKey == value)
                return;

            _accessKey = value;
            Invalidate(UiInvalidationKind.Semantic | UiInvalidationKind.Render);
        }
    }

    public char? EffectiveAccessKey => AccessKey ?? FindAccessMarker(Text);

    public UiElement? Target
    {
        get => _target;
        set
        {
            ThrowIfDisposed();
            if (value is not null && Session is not null && value.Session is not null && value.Session != Session)
                throw new InvalidOperationException("A label target must belong to the same UI session.");
            if (ReferenceEquals(_target, value))
                return;

            _target = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Label,
            DisplayText,
            Bounds,
            CreateSemanticState(),
            []);

    protected static string StripAccessMarkers(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Contains('&', StringComparison.Ordinal))
            return text;

        var characters = new List<char>(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (current == '&')
            {
                if (index + 1 < text.Length && text[index + 1] == '&')
                {
                    characters.Add('&');
                    index++;
                }

                continue;
            }

            characters.Add(current);
        }

        return new string([.. characters]);
    }

    private UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        state |= UiSemanticState.ReadOnly;
        return state;
    }

    private static char? FindAccessMarker(string text)
    {
        for (int index = 0; index < text.Length - 1; index++)
        {
            if (text[index] == '&' && text[index + 1] != '&')
                return char.ToUpperInvariant(text[index + 1]);

            if (text[index] == '&' && text[index + 1] == '&')
                index++;
        }

        return null;
    }
}
