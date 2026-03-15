using System;
using System.Collections.Generic;
using TheArtOfDev.HtmlRenderer.Adapters;
using System.Drawing;

namespace TheArtOfDev.HtmlRenderer.Core.Dom;

internal sealed class CssLineBox
{
    public CssLineBox(CssBox ownerBox)
    {
        Rectangles = [];
        RelatedBoxes = [];
        Words = [];
        OwnerBox = ownerBox;
        OwnerBox.LineBoxes.Add(this);
    }

    public List<CssBox> RelatedBoxes { get; }
    public List<CssRect> Words { get; }
    public CssBox OwnerBox { get; }
    public Dictionary<CssBox, RectangleF> Rectangles { get; }
    public double LineHeight
    {
        get
        {
            double height = 0;

            foreach (var rect in Rectangles)
                height = Math.Max(height, rect.Value.Height);

            return height;
        }
    }

    public double LineBottom
    {
        get
        {
            double bottom = 0;

            foreach (var rect in Rectangles)
                bottom = Math.Max(bottom, rect.Value.Bottom);

            return bottom;
        }
    }

    internal void ReportExistanceOf(CssRect word)
    {
        if (!Words.Contains(word))
            Words.Add(word);

        if (!RelatedBoxes.Contains(word.OwnerBox))
            RelatedBoxes.Add(word.OwnerBox);
    }

    internal List<CssRect> WordsOf(CssBox box)
    {
        List<CssRect> r = [];

        foreach (CssRect word in Words)
            if (word.OwnerBox.Equals(box))
                r.Add(word);

        return r;
    }

    internal void UpdateRectangle(CssBox box, double x, double y, double r, double b)
    {
        double leftspacing = box.ActualBorderLeftWidth + box.ActualPaddingLeft;
        double rightspacing = box.ActualBorderRightWidth + box.ActualPaddingRight;
        double topspacing = box.ActualBorderTopWidth + box.ActualPaddingTop;
        double bottomspacing = box.ActualBorderBottomWidth + box.ActualPaddingTop;

        if ((box.FirstHostingLineBox != null && box.FirstHostingLineBox.Equals(this)) || box.IsImage)
            x -= leftspacing;

        if ((box.LastHostingLineBox != null && box.LastHostingLineBox.Equals(this)) || box.IsImage)
            r += rightspacing;

        if (!box.IsImage)
        {
            y -= topspacing;
            b += bottomspacing;
        }

        if (!Rectangles.TryGetValue(box, out RectangleF f))
        {
            Rectangles.Add(box, RectangleF.FromLTRB((float)x, (float)y, (float)r, (float)b));
        }
        else
        {
            Rectangles[box] = RectangleF.FromLTRB(
                (float)Math.Min(f.X, x), (float)Math.Min(f.Y, y),
                (float)Math.Max(f.Right, r), (float)Math.Max(f.Bottom, b));
        }

        if (box.ParentBox != null && box.ParentBox.IsInline)
            UpdateRectangle(box.ParentBox, x, y, r, b);
    }

    internal void AssignRectanglesToBoxes()
    {
        foreach (CssBox b in Rectangles.Keys)
            b.Rectangles.Add(this, Rectangles[b]);
    }

    internal void SetBaseLine(RGraphics g, CssBox b, double baseline)
    {
        //TODO: Aqui me quede, checar poniendo "by the" con un font-size de 3em
        List<CssRect> ws = WordsOf(b);

        if (!Rectangles.TryGetValue(b, out RectangleF r))
            return;

        //Save top of words related to the top of rectangle
        double gap = 0f;

        if (ws.Count > 0)
        {
            gap = ws[0].Top - r.Top;
        }
        else
        {
            CssRect firstw = CssBoxHelper.FirstWordOccourence(b, this);

            if (firstw != null)
                gap = firstw.Top - r.Top;
        }

        //New top that words will have
        //float newtop = baseline - (Height - OwnerBox.FontDescent - 3); //OLD
        double newtop = baseline; // -GetBaseLineHeight(b, g); //OLD

        if (b.ParentBox != null && b.ParentBox.Rectangles.ContainsKey(this) && r.Height < b.ParentBox.Rectangles[this].Height)
        {
            //Do this only if rectangle is shorter than parent's
            double recttop = newtop - gap;
            RectangleF newr = new(r.X, (float)recttop, r.Width, r.Height);
            
            Rectangles[b] = newr;
            b.OffsetRectangle(this, gap);
        }

        foreach (var word in ws)
        {
            if (!word.IsImage)
                word.Top = newtop;
        }
    }

    public bool IsLastSelectedWord(CssRect word)
    {
        for (int i = 0; i < Words.Count - 1; i++)
        {
            if (Words[i] == word)
                return !Words[i + 1].Selected;
        }

        return true;
    }

    public override string ToString()
    {
        string[] ws = new string[Words.Count];

        for (int i = 0; i < ws.Length; i++)
            ws[i] = Words[i].Text;

        return string.Join(" ", ws);
    }
}