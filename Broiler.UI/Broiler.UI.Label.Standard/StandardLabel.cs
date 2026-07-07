using System;
using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.UI.Label.Standard;

public sealed class StandardLabel : UiLabel
{
    protected override BSize MeasureCore(BSize availableSize)
    {
        IReadOnlyList<LabelLine> lines = BuildLines(availableSize.Width);
        double width = 0;
        foreach (LabelLine line in lines)
            width = Math.Max(width, line.Width);

        double height = lines.Count * BTextMeasurer.GetLineHeight(Font);
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        IReadOnlyList<LabelLine> lines = BuildLines(Bounds.Width);
        double lineHeight = BTextMeasurer.GetLineHeight(Font);

        context.RenderList.PushClip(Bounds);
        for (int index = 0; index < lines.Count; index++)
        {
            LabelLine line = lines[index];
            if (line.Text.Length == 0)
                continue;

            double x = Direction == UiTextDirection.RightToLeft
                ? Bounds.Right - line.Width
                : Bounds.Left;
            double y = Bounds.Top + index * lineHeight;
            context.RenderList.DrawText(new BTextRun(line.Text, Font, Foreground), new BPoint(x, y));
        }

        context.RenderList.PopClip();
    }

    private IReadOnlyList<LabelLine> BuildLines(double availableWidth)
    {
        string text = DisplayText;
        double maxWidth = double.IsInfinity(availableWidth) || availableWidth <= 0
            ? double.PositiveInfinity
            : availableWidth;

        if (Wrapping == UiTextWrapping.NoWrap)
            return [CreateLine(ApplyTrimming(text, maxWidth))];

        var lines = new List<LabelLine>();
        foreach (string paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            AddWrappedParagraph(paragraph, maxWidth, lines);

        return lines.Count == 0 ? [CreateLine(string.Empty)] : lines;
    }

    private void AddWrappedParagraph(string paragraph, double maxWidth, List<LabelLine> lines)
    {
        if (paragraph.Length == 0)
        {
            lines.Add(CreateLine(string.Empty));
            return;
        }

        if (double.IsInfinity(maxWidth))
        {
            lines.Add(CreateLine(paragraph));
            return;
        }

        string current = string.Empty;
        foreach (string word in paragraph.Split(' ', StringSplitOptions.None))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (Measure(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(CreateLine(current));
                current = string.Empty;
            }

            if (Measure(word) <= maxWidth)
            {
                current = word;
                continue;
            }

            BreakWord(word, maxWidth, lines, ref current);
        }

        if (current.Length > 0)
            lines.Add(CreateLine(current));
    }

    private void BreakWord(string word, double maxWidth, List<LabelLine> lines, ref string current)
    {
        foreach (char character in word)
        {
            string candidate = current + character;
            if (candidate.Length > 1 && Measure(candidate) > maxWidth)
            {
                lines.Add(CreateLine(current));
                current = character.ToString();
            }
            else
            {
                current = candidate;
            }
        }
    }

    private string ApplyTrimming(string text, double maxWidth)
    {
        if (Trimming != UiTextTrimming.CharacterEllipsis || double.IsInfinity(maxWidth) || Measure(text) <= maxWidth)
            return text;

        const string ellipsis = "...";
        double ellipsisWidth = Measure(ellipsis);
        if (ellipsisWidth > maxWidth)
            return string.Empty;

        string result = string.Empty;
        foreach (char character in text)
        {
            string candidate = result + character;
            if (Measure(candidate) + ellipsisWidth > maxWidth)
                break;

            result = candidate;
        }

        return result + ellipsis;
    }

    private LabelLine CreateLine(string text) =>
        new(text, BTextMeasurer.MeasureAdvance(text, Font));

    private double Measure(string text) => BTextMeasurer.MeasureAdvance(text, Font);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    private readonly record struct LabelLine(string Text, double Width);
}
