using System.Drawing;

namespace Broiler.Layout.Engine;

internal sealed class CssRectImage(CssBox owner) : CssRect(owner)
{
    private object _image;
    private RectangleF _imageRectangle;

    public override object Image
    {
        get { return _image; }
        set { _image = value; }
    }

    public override bool IsImage => true;

    public RectangleF ImageRectangle
    {
        get { return _imageRectangle; }
        set { _imageRectangle = value; }
    }

    public override string ToString() => "Image";
}