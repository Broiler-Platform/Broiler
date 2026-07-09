using System;
using Broiler.Graphics;

namespace Broiler.UI.ImageView;

public abstract class UiImageView : UiElement
{
    private BImageHandle _image = BImageHandle.Invalid;
    private BRect? _sourceRect;
    private string _altText = string.Empty;
    private UiImageStretch _stretch = UiImageStretch.Uniform;
    private double _opacity = 1;
    private BSize _preferredSize = BSize.Empty;

    public BImageHandle Image
    {
        get => _image;
        set
        {
            ThrowIfDisposed();
            if (_image == value)
                return;

            _image = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BRect? SourceRect
    {
        get => _sourceRect;
        set
        {
            ThrowIfDisposed();
            if (value.HasValue && (value.Value.Width < 0 || value.Value.Height < 0))
                throw new ArgumentOutOfRangeException(nameof(value), "Source rectangle size must be non-negative.");
            if (_sourceRect == value)
                return;

            _sourceRect = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public string AltText
    {
        get => _altText;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_altText, value))
                return;

            _altText = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    public UiImageStretch Stretch
    {
        get => _stretch;
        set
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_stretch == value)
                return;

            _stretch = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double Opacity
    {
        get => _opacity;
        set
        {
            ThrowIfDisposed();
            if (value is < 0 or > 1 || double.IsNaN(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Opacity must be within [0, 1].");
            if (_opacity.Equals(value))
                return;

            _opacity = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred image view size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    protected BSize NaturalImageSize => Image.IsValid ? Image.PixelSize : BSize.Empty;

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ImageView,
            AltText,
            Bounds,
            Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None,
            []);
}
