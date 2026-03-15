using Broiler.HTML.Adapters.Adapters;
using Broiler.HTML.Core.Core;
using Broiler.HTML.Dom.Core.Dom;
using Broiler.HTML.Utils.Core.Utils;
using System.Drawing;

namespace Broiler.HTML.Dom.Core.Utils;

internal static class RenderUtils
{
    public static bool IsColorVisible(Color color) => color.A > 0;

    public static bool ClipGraphicsByOverflow(RGraphics g, CssBox box)
    {
        var containingBlock = box.ContainingBlock;

        while (true)
        {
            if (containingBlock.Overflow == CssConstants.Hidden)
            {
                var prevClip = g.GetClip();

                // CSS2.1 §11.1.1: Content is clipped to the padding edge
                // (inside borders, but including the padding area).
                var cb = box.ContainingBlock;
                var rect = new RectangleF(
                    (float)(cb.Location.X + cb.ActualBorderLeftWidth),
                    (float)(cb.Location.Y + cb.ActualBorderTopWidth),
                    (float)(cb.Size.Width - cb.ActualBorderLeftWidth - cb.ActualBorderRightWidth),
                    (float)(cb.Size.Height - cb.ActualBorderTopWidth - cb.ActualBorderBottomWidth));

                if (rect.Width < 0) rect.Width = 0;
                if (rect.Height < 0) rect.Height = 0;

                if (!box.IsFixed)
                    rect.Offset(box.ContainerInt.ScrollOffset);

                rect.Intersect(prevClip);
                g.PushClip(rect);
                return true;
            }
            else
            {
                var cBlock = containingBlock.ContainingBlock;
                if (cBlock == containingBlock)
                    return false;
                containingBlock = cBlock;
            }
        }
    }

    public static void DrawImageLoadingIcon(RGraphics g, IHtmlContainerInt htmlContainer, RectangleF r)
    {
        g.DrawRectangle(g.GetPen(Color.LightGray), r.Left + 3, r.Top + 3, 13, 14);
        var image = htmlContainer.GetLoadingImage();
        g.DrawImage(image, new RectangleF(r.Left + 4, r.Top + 4, (float)image.Width, (float)image.Height));
    }

    public static void DrawImageErrorIcon(RGraphics g, IHtmlContainerInt htmlContainer, RectangleF r)
    {
        g.DrawRectangle(g.GetPen(Color.LightGray), r.Left + 2, r.Top + 2, 15, 15);
        var image = htmlContainer.GetLoadingFailedImage();
        g.DrawImage(image, new RectangleF(r.Left + 3, r.Top + 3, (float)image.Width, (float)image.Height));
    }

    public static RGraphicsPath GetRoundRect(RGraphics g, RectangleF rect, double nwRadius, double neRadius, double seRadius, double swRadius)
    {
        var path = g.GetGraphicsPath();

        path.Start(rect.Left + nwRadius, rect.Top);

        path.LineTo(rect.Right - neRadius, rect.Y);

        if (neRadius > 0f)
            path.ArcTo(rect.Right, rect.Top + neRadius, neRadius, RGraphicsPath.Corner.TopRight);

        path.LineTo(rect.Right, rect.Bottom - seRadius);

        if (seRadius > 0f)
            path.ArcTo(rect.Right - seRadius, rect.Bottom, seRadius, RGraphicsPath.Corner.BottomRight);

        path.LineTo(rect.Left + swRadius, rect.Bottom);

        if (swRadius > 0f)
            path.ArcTo(rect.Left, rect.Bottom - swRadius, swRadius, RGraphicsPath.Corner.BottomLeft);

        path.LineTo(rect.Left, rect.Top + nwRadius);

        if (nwRadius > 0f)
            path.ArcTo(rect.Left + nwRadius, rect.Top, nwRadius, RGraphicsPath.Corner.TopLeft);

        return path;
    }
}