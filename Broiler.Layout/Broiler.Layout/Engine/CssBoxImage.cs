using System.Drawing;
using CssConstants = Broiler.CSS.CssConstants;
using CssLength = Broiler.CSS.CssLength;
using CssUnit = Broiler.CSS.CssUnit;


namespace Broiler.Layout.Engine;

internal sealed class CssBoxImage : CssBox
{
    private readonly CssRectImage _imageWord;
    private ILayoutImageLoader _imageLoadHandler;
    private bool _imageLoadingComplete;

    public CssBoxImage(CssBox parent, HtmlTag tag, Uri baseUrl) : base(parent, tag, baseUrl)
    {
        _imageWord = new CssRectImage(this);
        Words.Add(_imageWord);

    }

    public object Image => _imageWord.Image;

    internal override void MeasureWordsSize(ILayoutEnvironment g)
    {
        if (!_wordsSizeMeasured)
        {
            // Load the CSS background image (if any) via the base class.
            // CssBoxImage overrides MeasureWordsSize entirely, so the base
            // class's background-image loading code would otherwise be skipped.
            LoadBackgroundImageIfNeeded();

            if (_imageLoadHandler == null && (LayoutEnvironment.AvoidAsyncImagesLoading || LayoutEnvironment.AvoidImagesLateLoading))
            {
                _imageLoadHandler = LayoutEnvironment.CreateImageLoader(OnLoadImageComplete);

                if (Content != null && Content != CssConstants.Normal)
                    _imageLoadHandler.LoadImage(Content, HtmlTag?.Attributes, base.BaseUrl);
                else
                {
                    var src = GetAttribute("src");
                    // <object data="..."> fallback: use 'data' attribute when 'src' is absent
                    if (string.IsNullOrEmpty(src))
                        src = GetAttribute("data");
                    _imageLoadHandler.LoadImage(src, HtmlTag?.Attributes, base.BaseUrl);
                }
            }

            MeasureWordSpacing(g);
            _wordsSizeMeasured = true;
        }

        CssLayoutEngine.MeasureImageSize(g, _imageWord);
    }

    public override void Dispose()
    {
        _imageLoadHandler?.Dispose();
        base.Dispose();
    }

    private void SetErrorBorder()
    {
        SetAllBorders(CssConstants.Solid, "2px", "#A0A0A0");
        BorderRightColor = BorderBottomColor = "#E3E3E3";
    }

    private void OnLoadImageComplete(object? image, RectangleF rectangle, bool async)
    {
        _imageWord.Image = image;
        _imageWord.ImageRectangle = rectangle;
        _imageLoadingComplete = true;
        _wordsSizeMeasured = false;

        if (_imageLoadingComplete && image == null)
            SetErrorBorder();

        if (!LayoutEnvironment.AvoidImagesLateLoading || async)
        {
            var width = new CssLength(Width);
            var height = new CssLength(Height);
            var layout = width.Number <= 0 || width.Unit != CssUnit.Px || height.Number <= 0 || height.Unit != CssUnit.Px;

            LayoutEnvironment.RequestRefresh(layout);
        }
    }
}