using System;
using System.Collections.Generic;
using TheArtOfDev.HtmlRenderer.Core.Utils;

namespace TheArtOfDev.HtmlRenderer.Core.Dom;

internal sealed class CssSpacingBox : CssBox
{
    public CssSpacingBox(CssBox tableBox, ref CssBox extendedBox, int startRow)
        : base(tableBox, new HtmlTag("none", false, new Dictionary<string, string> { { "colspan", "1" } }))
    {
        ExtendedBox = extendedBox;
        Display = CssConstants.None;

        StartRow = startRow;
        EndRow = startRow + Int32.Parse(extendedBox.GetAttribute("rowspan", "1")) - 1;
    }

    public CssBox ExtendedBox { get; }
    public int StartRow { get; }
    public int EndRow { get; }
}