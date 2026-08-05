using System;
using System.Text;
using Broiler.Graphics;
using Broiler.UI.Standard;

namespace Broiler.UI.CodeEditor.Standard;

/// <summary>
/// The Standard source editor.
///
/// It renders the visible range and nothing else. There is no child element per
/// line and none per token: a 100,000-line document paints the forty-odd lines
/// on screen, so cost is a function of the window rather than of the file. Every
/// text read is bounded by the visible range for the same reason.
/// </summary>
public sealed partial class StandardCodeEditor : UiCodeEditor, IStandardThemedControl
{
    private readonly StringBuilder _lineBuffer = new(256);
    private BFontStyle _font = new("monospace", 15);
    private double _characterAdvance = 8;
    private double _lineHeight = 18;
    private double _gutterWidth = 48;
    private BFontStyle? _measuredFont;

    /// <summary>
    /// The editor font. A proportional font would make column arithmetic a
    /// per-glyph measurement on every hit test, so the control assumes a
    /// monospace advance and measures it once per font change.
    /// </summary>
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
            _measuredFont = null;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double LineHeight
    {
        get
        {
            EnsureMetrics();
            return _lineHeight;
        }
    }

    public double CharacterAdvance
    {
        get
        {
            EnsureMetrics();
            return _characterAdvance;
        }
    }

    /// <summary>Width of the line-number gutter, zero when it is hidden.</summary>
    public double GutterWidth
    {
        get
        {
            EnsureMetrics();
            return ShowLineNumbers ? _gutterWidth : 0;
        }
    }

    /// <summary>
    /// How many lines the current bounds can show. The renderer paints this
    /// many and the viewport is sized from it.
    /// </summary>
    public int VisibleLineCapacity =>
        Bounds.Height <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(Bounds.Height / LineHeight));

    public void ApplyTheme(StandardThemeTokens tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        Palette = StandardCodeEditorPalette.FromTokens(tokens);
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        EnsureMetrics();
        double width = double.IsFinite(availableSize.Width) && availableSize.Width > 0
            ? Math.Min(PreferredSize.Width, availableSize.Width)
            : PreferredSize.Width;
        double height = double.IsFinite(availableSize.Height) && availableSize.Height > 0
            ? Math.Min(PreferredSize.Height, availableSize.Height)
            : PreferredSize.Height;
        return new BSize(width, height);
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        base.ArrangeCore(finalRect);

        // The viewport follows the arranged height so the renderer and the
        // caret-visibility logic agree on what "on screen" means.
        int capacity = VisibleLineCapacity;
        if (Viewport.VisibleLineCount != capacity)
            Viewport = Viewport with { VisibleLineCount = capacity };
    }

    protected override void RenderCore(UiRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsureMetrics();

        BRenderList list = context.RenderList;
        BRect bounds = Bounds;
        CodeEditorPalette palette = Palette;
        ICodeTextSnapshot snapshot = Snapshot;

        list.FillRect(bounds, palette.Background);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        double gutter = GutterWidth;
        if (gutter > 0)
            list.FillRect(new BRect(bounds.Left, bounds.Top, gutter, bounds.Height), palette.GutterBackground);

        int firstLine = Math.Clamp(Viewport.FirstVisibleLine, 0, Math.Max(0, snapshot.LineCount - 1));
        int lastLine = Math.Min(snapshot.LineCount - 1, firstLine + VisibleLineCapacity - 1);
        int caretLine = snapshot.GetLineFromPosition(CaretPosition);
        int tabSize = IndentPolicy.TabSize;
        int? matchingBracket = FindMatchingBracket(CaretPosition);

        double textLeft = bounds.Left + gutter + 4;
        list.PushClip(bounds);
        try
        {
            for (int line = firstLine; line <= lastLine; line++)
            {
                double top = bounds.Top + ((line - firstLine) * _lineHeight);
                RenderLine(
                    list, snapshot, palette, line, top, textLeft, bounds, gutter,
                    tabSize, line == caretLine, matchingBracket);
            }
        }
        finally
        {
            list.PopClip();
        }
    }

    private void RenderLine(
        BRenderList list,
        ICodeTextSnapshot snapshot,
        CodeEditorPalette palette,
        int line,
        double top,
        double textLeft,
        BRect bounds,
        double gutter,
        int tabSize,
        bool isCaretLine,
        int? matchingBracket)
    {
        int lineStart = snapshot.GetLineStart(line);
        int lineLength = snapshot.GetLineLength(line);

        // The only text read per line, bounded by the line itself.
        _lineBuffer.Clear();
        string text = lineLength == 0 ? string.Empty : snapshot.GetText(lineStart, lineLength);
        ReadOnlySpan<char> span = text.AsSpan();

        if (isCaretLine && Selection.IsEmpty)
        {
            list.FillRect(
                new BRect(bounds.Left + gutter, top, Math.Max(0, bounds.Width - gutter), _lineHeight),
                palette.CurrentLineBackground);
        }

        RenderSelection(list, palette, line, lineStart, lineLength, span, top, textLeft, tabSize);

        if (gutter > 0)
            RenderGutter(list, palette, line, top, bounds, gutter, isCaretLine);

        RenderClassifiedText(list, palette, line, span, top, textLeft, tabSize);
        RenderDiagnostics(list, palette, line, lineStart, span, top, textLeft, tabSize);

        if (matchingBracket is { } bracket && bracket >= lineStart && bracket < lineStart + lineLength)
        {
            double x = textLeft + (CodeLineLayout.VisualColumn(span, bracket - lineStart, tabSize) * _characterAdvance) - Viewport.HorizontalOffset;
            list.FillRect(new BRect(x, top, _characterAdvance, _lineHeight), palette.BracketMatch);
        }

        if (HasFocus && isCaretLine)
        {
            int column = CodeLineLayout.VisualColumn(span, Math.Max(0, CaretPosition - lineStart), tabSize);
            double x = textLeft + (column * _characterAdvance) - Viewport.HorizontalOffset;
            list.FillRect(new BRect(x, top + 1, 1, Math.Max(1, _lineHeight - 2)), palette.Caret);
        }

        if (HasComposition && CompositionStart >= lineStart && CompositionStart <= lineStart + lineLength)
        {
            int from = CodeLineLayout.VisualColumn(span, Math.Max(0, CompositionStart - lineStart), tabSize);
            int to = CodeLineLayout.VisualColumn(span, Math.Max(0, Math.Min(lineLength, CompositionEnd - lineStart)), tabSize);
            double x = textLeft + (from * _characterAdvance) - Viewport.HorizontalOffset;
            list.FillRect(
                new BRect(x, top + _lineHeight - 2, Math.Max(_characterAdvance, (to - from) * _characterAdvance), 1),
                palette.CompositionUnderline);
        }
    }

    private void RenderGutter(
        BRenderList list, CodeEditorPalette palette, int line, double top, BRect bounds, double gutter, bool isCaretLine)
    {
        string number = (line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        double advance = BTextMeasurer.MeasureAdvance(number, _font);
        list.DrawText(
            new BTextRun(number, _font, isCaretLine ? palette.CurrentLineNumber : palette.LineNumber),
            new BPoint(bounds.Left + gutter - advance - 6, top));

        // A severity marker in the gutter, shaped per severity so it is not
        // colour alone that distinguishes an error from a warning.
        CodeDiagnosticSeverity severity = Diagnostics?.GetLineSeverity(line) ?? CodeDiagnosticSeverity.Hidden;
        if (severity == CodeDiagnosticSeverity.Hidden)
            return;

        BColor color = palette.GetDiagnosticColor(severity);
        double size = Math.Max(3, _lineHeight / 3);
        double y = top + ((_lineHeight - size) / 2);
        if (severity == CodeDiagnosticSeverity.Error)
            list.FillRect(new BRect(bounds.Left + 2, y, size, size), color);
        else if (severity == CodeDiagnosticSeverity.Warning)
            list.FillRect(new BRect(bounds.Left + 2, y + (size / 3), size, size / 3), color);
        else
            list.FillRect(new BRect(bounds.Left + 2 + (size / 3), y, size / 3, size), color);
    }

    private void RenderSelection(
        BRenderList list,
        CodeEditorPalette palette,
        int line,
        int lineStart,
        int lineLength,
        ReadOnlySpan<char> span,
        double top,
        double textLeft,
        int tabSize)
    {
        CodeSelection selection = Selection;
        if (selection.IsEmpty)
            return;

        int lineEnd = lineStart + lineLength;
        if (selection.End < lineStart || selection.Start > lineEnd)
            return;

        int from = CodeLineLayout.VisualColumn(span, Math.Max(0, selection.Start - lineStart), tabSize);
        int to = selection.End >= lineEnd
            ? CodeLineLayout.ColumnWidth(span, tabSize) + 1  // Include the newline.
            : CodeLineLayout.VisualColumn(span, selection.End - lineStart, tabSize);

        double x = textLeft + (from * _characterAdvance) - Viewport.HorizontalOffset;
        double width = Math.Max(_characterAdvance / 2, (to - from) * _characterAdvance);
        list.FillRect(
            new BRect(x, top, width, _lineHeight),
            HasFocus ? palette.Selection : palette.InactiveSelection);
    }

    private void RenderClassifiedText(
        BRenderList list,
        CodeEditorPalette palette,
        int line,
        ReadOnlySpan<char> span,
        double top,
        double textLeft,
        int tabSize)
    {
        if (span.Length == 0)
            return;

        CodeClassificationResult? classifications = Classifications;
        ReadOnlySpan<CodeClassificationSpan> spans = default;

        // Classifications are only used while their snapshot is the current
        // one. A result for a superseded snapshot describes text that is no
        // longer there, and painting it would colour the wrong characters.
        if (classifications is not null &&
            ReferenceEquals(classifications.Snapshot, Snapshot) &&
            line < classifications.LineCount)
        {
            spans = classifications.GetLineSpans(line);
        }

        if (spans.Length == 0)
        {
            DrawSegment(list, span, 0, span.Length, CodeClassificationKind.None, palette, top, textLeft, tabSize);
            return;
        }

        int cursor = 0;
        foreach (CodeClassificationSpan classification in spans)
        {
            int start = Math.Clamp(classification.Start, 0, span.Length);
            int end = Math.Clamp(classification.Start + classification.Length, start, span.Length);
            if (start > cursor)
                DrawSegment(list, span, cursor, start, CodeClassificationKind.None, palette, top, textLeft, tabSize);
            if (end > start)
                DrawSegment(list, span, start, end, classification.Kind, palette, top, textLeft, tabSize);
            cursor = Math.Max(cursor, end);
        }

        if (cursor < span.Length)
            DrawSegment(list, span, cursor, span.Length, CodeClassificationKind.None, palette, top, textLeft, tabSize);
    }

    private void DrawSegment(
        BRenderList list,
        ReadOnlySpan<char> line,
        int start,
        int end,
        CodeClassificationKind kind,
        CodeEditorPalette palette,
        double top,
        double textLeft,
        int tabSize)
    {
        if (end <= start)
            return;

        int column = CodeLineLayout.VisualColumn(line, start, tabSize);
        double x = textLeft + (column * _characterAdvance) - Viewport.HorizontalOffset;
        if (x > Bounds.Right)
            return;

        // Tabs are expanded here rather than passed to the text renderer, which
        // has no notion of tab stops.
        _lineBuffer.Clear();
        int running = column;
        for (int i = start; i < end; i++)
        {
            if (line[i] == '\t')
            {
                int width = tabSize - (running % tabSize);
                _lineBuffer.Append(' ', width);
                running += width;
            }
            else
            {
                _lineBuffer.Append(line[i]);
                running++;
            }
        }

        BFontStyle font = _font;
        if (palette.IsEmphasized(kind))
            font = font with { Weight = BFontWeight.Bold };
        if (palette.IsItalic(kind))
            font = font with { Slant = BFontSlant.Italic };

        list.DrawText(
            new BTextRun(_lineBuffer.ToString(), font, palette.GetClassificationColor(kind)),
            new BPoint(x, top));
    }

    private void RenderDiagnostics(
        BRenderList list,
        CodeEditorPalette palette,
        int line,
        int lineStart,
        ReadOnlySpan<char> span,
        double top,
        double textLeft,
        int tabSize)
    {
        CodeDiagnosticSet? diagnostics = Diagnostics;
        if (diagnostics is null || !ReferenceEquals(diagnostics.Snapshot, Snapshot))
            return;

        foreach (CodeDiagnosticAdornment diagnostic in diagnostics.GetLineDiagnostics(line))
        {
            if (diagnostic.Severity == CodeDiagnosticSeverity.Hidden)
                continue;

            int from = Math.Clamp(diagnostic.Start - lineStart, 0, span.Length);
            int to = Math.Clamp(diagnostic.End - lineStart, from, span.Length);
            int fromColumn = CodeLineLayout.VisualColumn(span, from, tabSize);
            int toColumn = Math.Max(fromColumn + 1, CodeLineLayout.VisualColumn(span, to, tabSize));

            double x = textLeft + (fromColumn * _characterAdvance) - Viewport.HorizontalOffset;
            double width = (toColumn - fromColumn) * _characterAdvance;
            double y = top + _lineHeight - 2;
            BColor color = palette.GetDiagnosticColor(diagnostic.Severity);

            // The shape carries the severity as well as the colour does, so the
            // three remain tellable apart in high contrast and to a viewer who
            // cannot distinguish the hues.
            switch (CodeEditorPalette.GetDiagnosticShape(diagnostic.Severity))
            {
                case CodeDiagnosticAdornmentShape.Wave:
                    for (double dx = 0; dx < width; dx += 4)
                    {
                        double segment = Math.Min(2, width - dx);
                        list.FillRect(new BRect(x + dx, y, segment, 1), color);
                        list.FillRect(new BRect(x + dx + 2, y + 1, Math.Min(2, Math.Max(0, width - dx - 2)), 1), color);
                    }

                    break;
                case CodeDiagnosticAdornmentShape.Dashed:
                    for (double dx = 0; dx < width; dx += 6)
                        list.FillRect(new BRect(x + dx, y, Math.Min(3, width - dx), 1), color);
                    break;
                case CodeDiagnosticAdornmentShape.Dotted:
                    for (double dx = 0; dx < width; dx += 3)
                        list.FillRect(new BRect(x + dx, y, Math.Min(1, width - dx), 1), color);
                    break;
            }
        }
    }

    private void EnsureMetrics()
    {
        if (_measuredFont is not null && _measuredFont == _font)
            return;

        _characterAdvance = Math.Max(1, BTextMeasurer.MeasureAdvance("M", _font));
        _lineHeight = Math.Max(1, BTextMeasurer.GetLineHeight(_font));

        // Wide enough for the largest line number the document can show, so the
        // gutter does not resize as the user scrolls.
        int digits = Math.Max(3, Snapshot.LineCount.ToString(System.Globalization.CultureInfo.InvariantCulture).Length);
        _gutterWidth = (digits * _characterAdvance) + 14;
        _measuredFont = _font;
    }
}
