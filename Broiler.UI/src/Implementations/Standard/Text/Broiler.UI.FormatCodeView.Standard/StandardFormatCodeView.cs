using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Documents.FormatCodes;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.FormatCodeView.Standard;

/// <summary>
/// Broiler-drawn, token-aware Formatting Codes view. Layout is a flat list of
/// virtual text lines; tokens are not UI children and only visible slices are
/// submitted to the render list.
/// </summary>
public sealed class StandardFormatCodeView : UiFormatCodeView, IStandardThemedControl
{
    private readonly List<VisualLine> _lines = [];
    private FormatCodeProjection? _layoutProjection;
    private BFontStyle? _layoutFont;
    private FormatCodeViewWrapping _layoutWrapping;
    private double _layoutWidth = double.NaN;
    private double _characterAdvance;
    private double _lineHeight;
    private double _contentWidth;
    private double _contentHeight;
    private double _scrollX;
    private double _scrollY;
    private bool _isSelecting;
    private ScrollbarAxis _dragAxis;
    private double _scrollbarDragOffset;
    private string _compositionText = string.Empty;

    public StandardFormatCodeView()
    {
        SelectionChanged += (_, _) => EnsureCaretVisible();
    }

    public BColor Background { get; set; } = StandardControlPaint.SurfaceAlt;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor InlineCodeForeground { get; set; } = StandardControlPaint.Accent;

    public BColor ParagraphCodeForeground { get; set; } = StandardControlPaint.Info;

    public BColor StructureCodeForeground { get; set; } = StandardControlPaint.Warning;

    public BColor EscapeForeground { get; set; } = StandardControlPaint.Danger;

    public BColor PendingForeground { get; set; } = StandardControlPaint.Success;

    public BColor DiagnosticForeground { get; set; } = StandardControlPaint.Danger;

    public BColor SelectionBackground { get; set; } = StandardControlPaint.AccentSoft;

    public BColor CaretColor { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BColor ScrollbarTrack { get; set; } = BColor.FromArgb(0x33, 0x94, 0xA3, 0xB8);

    public BColor ScrollbarThumb { get; set; } = BColor.FromArgb(0xAA, 0x7D, 0x8D, 0xA3);

    public BFontStyle Font { get; set; } = new("monospace", 15);

    public double PaddingX { get; set; } = 8;

    public double PaddingY { get; set; } = 6;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public double ScrollbarThickness { get; set; } = 12;

    public double MinimumScrollbarThumbLength { get; set; } = 18;

    public double VerticalScrollOffset => _scrollY;

    public double HorizontalScrollOffset => _scrollX;

    public string CompositionText => _compositionText;

    protected override bool IsCompositionActive => _compositionText.Length > 0;

    public int VisualLineCount
    {
        get
        {
            EnsureLayout();
            return _lines.Count;
        }
    }

    public bool HasVerticalScrollbar
    {
        get
        {
            EnsureLayout();
            return VerticalScrollPolicy == FormatCodeViewScrollPolicy.Always ||
                (VerticalScrollPolicy == FormatCodeViewScrollPolicy.Auto && MaxVerticalScroll > 0);
        }
    }

    public bool HasHorizontalScrollbar
    {
        get
        {
            EnsureLayout();
            return Wrapping == FormatCodeViewWrapping.NoWrap &&
                (HorizontalScrollPolicy == FormatCodeViewScrollPolicy.Always ||
                 (HorizontalScrollPolicy == FormatCodeViewScrollPolicy.Auto && MaxHorizontalScroll > 0));
        }
    }

    public void ApplyTheme(StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        Background = theme.SurfaceAlt;
        Foreground = theme.Text;
        InlineCodeForeground = theme.Accent;
        ParagraphCodeForeground = theme.Info;
        StructureCodeForeground = theme.Warning;
        EscapeForeground = theme.Danger;
        PendingForeground = theme.Success;
        DiagnosticForeground = theme.Danger;
        SelectionBackground = theme.AccentSoft;
        CaretColor = theme.Text;
        BorderColor = theme.Border;
        FocusRing = theme.FocusRing;
        Invalidate(UiInvalidationKind.Render);
    }

    protected override BSize MeasureCore(BSize availableSize) => new(
        ClampDesired(PreferredSize.Width, availableSize.Width),
        ClampDesired(PreferredSize.Height, availableSize.Height));

    protected override void RenderCore(UiRenderContext context)
    {
        EnsureLayout();
        bool focused = Session?.FocusedElement == this;
        BRenderList list = context.RenderList;
        StandardControlPaint.FillRounded(
            list,
            Bounds,
            IsEnabled ? Background : StandardControlPaint.SurfaceDisabled,
            CornerRadius);
        StandardControlPaint.StrokeRounded(
            list,
            Bounds,
            focused ? FocusRing : BorderColor,
            CornerRadius,
            focused ? 2 : 1);

        list.PushClip(ViewportBounds);
        DrawSelection(list);
        DrawVisibleText(list);
        DrawPendingTokens(list);
        DrawCaret(list, focused);
        DrawComposition(list, focused);
        list.PopClip();
        DrawScrollbars(list);
        PublishCaret(focused);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;
        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.PointerMove => HandlePointerMove(input),
            UiInputEventKind.PointerWheel => HandleWheel(input),
            UiInputEventKind.TextInput => RequestTextReplacement(input.Text ?? string.Empty),
            UiInputEventKind.TextComposition => HandleTextComposition(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    protected override void OnDetached()
    {
        if (Session?.Host is IUiTextInputHost textInput)
            textInput.ClearCaret(this);
        _compositionText = string.Empty;
        base.OnDetached();
    }

    private void DrawVisibleText(BRenderList list)
    {
        if (Projection is null || Projection.Tokens.Count == 0)
            return;

        (int firstLine, int lastLine) = VisibleLineRange();
        for (int lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            VisualLine line = _lines[lineIndex];
            (int start, int end) = VisibleTextRange(line);
            if (end <= start)
                continue;

            int tokenIndex = FindTokenIndex(start);
            while (tokenIndex < Projection.Tokens.Count)
            {
                FormatCodeToken token = Projection.Tokens[tokenIndex];
                if (token.ProjectedStart >= end)
                    break;
                int segmentStart = Math.Max(start, token.ProjectedStart);
                int segmentEnd = Math.Min(end, token.ProjectedStart + token.ProjectedLength);
                if (segmentEnd > segmentStart)
                {
                    string segment = Text.Substring(segmentStart, segmentEnd - segmentStart);
                    double x = ContentLeft + ((segmentStart - line.Start) * _characterAdvance) - _scrollX;
                    double y = ContentTop + line.Top - _scrollY;
                    list.DrawText(
                        new BTextRun(segment, TokenFont(token), TokenColor(token)),
                        new BPoint(x, y));
                }

                tokenIndex++;
            }
        }
    }

    private void DrawSelection(BRenderList list)
    {
        if (!HasSelection)
            return;
        (int firstLine, int lastLine) = VisibleLineRange();
        for (int lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            VisualLine line = _lines[lineIndex];
            int start = Math.Max(SelectionStart, line.Start);
            int end = Math.Min(SelectionEnd, line.End);
            if (end <= start)
                continue;
            double x = ContentLeft + ((start - line.Start) * _characterAdvance) - _scrollX;
            double y = ContentTop + line.Top - _scrollY;
            list.FillRect(
                new BRect(x, y, (end - start) * _characterAdvance, _lineHeight),
                SelectionBackground);
        }
    }

    private void DrawCaret(BRenderList list, bool focused)
    {
        if (!focused)
            return;
        VisualLine line = LineForOffset(CaretOffset);
        double x = ContentLeft + ((CaretOffset - line.Start) * _characterAdvance) - _scrollX;
        double y = ContentTop + line.Top - _scrollY;
        list.FillRect(new BRect(x, y + 1, 1, Math.Max(1, _lineHeight - 2)), CaretColor);
    }

    private void DrawComposition(BRenderList list, bool focused)
    {
        if (!focused || _compositionText.Length == 0)
            return;
        BRect caret = CaretBounds();
        double advance = BTextMeasurer.MeasureAdvance(_compositionText, Font);
        list.DrawText(new BTextRun(_compositionText, Font, Foreground), new BPoint(caret.Left, caret.Top));
        list.FillRect(new BRect(caret.Left, caret.Bottom - 1, advance, 1), Foreground);
    }

    private void PublishCaret(bool focused)
    {
        if (!focused || Session?.Host is not IUiTextInputHost textInput)
            return;
        textInput.PublishCaret(new UiTextCaretInfo(
            this,
            CaretBounds(),
            CaretOffset,
            SelectionStart,
            SelectionLength,
            IsCompositionActive));
    }

    private BRect CaretBounds()
    {
        VisualLine line = LineForOffset(CaretOffset);
        double x = ContentLeft + ((CaretOffset - line.Start) * _characterAdvance) - _scrollX;
        double y = ContentTop + line.Top - _scrollY;
        return new BRect(x, y + 1, 1, Math.Max(1, _lineHeight - 2));
    }

    private bool HandleTextComposition(UiInputEvent input)
    {
        if (!IsEditable)
            return false;
        TextCompositionState state = input.CompositionState ?? TextCompositionState.Updated;
        if (state is TextCompositionState.Started or TextCompositionState.Updated)
        {
            _compositionText = input.Text ?? string.Empty;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            return true;
        }
        _compositionText = string.Empty;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return state != TextCompositionState.Committed ||
            RequestTextReplacement(input.Text ?? string.Empty);
    }

    private void DrawPendingTokens(BRenderList list)
    {
        if (Projection is null || Projection.PendingTokens.Count == 0)
            return;
        int previousStart = -1;
        double additionalAdvance = 0;
        foreach (FormatCodeToken token in Projection.PendingTokens)
        {
            if (token.ProjectedStart != previousStart)
            {
                previousStart = token.ProjectedStart;
                additionalAdvance = 0;
            }
            VisualLine line = LineForOffset(token.ProjectedStart);
            BFontStyle pendingFont = Font with { Weight = BFontWeight.Bold };
            double x = ContentLeft + ((token.ProjectedStart - line.Start) * _characterAdvance) -
                _scrollX + additionalAdvance;
            double y = ContentTop + line.Top - _scrollY;
            list.DrawText(
                new BTextRun(token.DisplayText, pendingFont, PendingForeground),
                new BPoint(x, y));
            additionalAdvance += BTextMeasurer.MeasureAdvance(token.DisplayText, pendingFont);
        }
    }

    private BColor TokenColor(FormatCodeToken token) => token.Kind switch
    {
        FormatCodeTokenKind.Text => IsEnabled ? Foreground : StandardControlPaint.TextDisabled,
        FormatCodeTokenKind.InlineCode => InlineCodeForeground,
        FormatCodeTokenKind.ParagraphCode => ParagraphCodeForeground,
        FormatCodeTokenKind.StructureCode => StructureCodeForeground,
        FormatCodeTokenKind.Escape => EscapeForeground,
        FormatCodeTokenKind.PendingCode => PendingForeground,
        FormatCodeTokenKind.Diagnostic => DiagnosticForeground,
        _ => Foreground,
    };

    private BFontStyle TokenFont(FormatCodeToken token) => token.Kind == FormatCodeTokenKind.Text
        ? Font
        : Font with { Weight = BFontWeight.SemiBold };

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;
        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            Session?.CaptureInput(this);
            if (TryBeginScrollbarInteraction(input.Position))
                return true;

            int offset = OffsetFromPoint(input.Position);
            _isSelecting = true;
            SetSelection(offset, offset);
            ActivateAt(offset);
            EnsureCaretVisible();
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            _isSelecting = false;
            _dragAxis = ScrollbarAxis.None;
            Session?.ReleaseInputCapture(this);
            return true;
        }

        return false;
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        if (Session?.CapturedElement != this)
            return false;
        if (_dragAxis != ScrollbarAxis.None)
        {
            DragScrollbar(input.Position);
            return true;
        }
        if (!_isSelecting)
            return false;

        SetSelection(SelectionAnchor, OffsetFromPoint(input.Position));
        EnsureCaretVisible();
        return true;
    }

    private bool HandleWheel(UiInputEvent input)
    {
        EnsureLayout();
        if (input.WheelAxis == MouseWheelAxis.Horizontal && MaxHorizontalScroll > 0)
        {
            SetHorizontalScroll(_scrollX - input.WheelDeltaNotches * _characterAdvance * 6);
            return true;
        }
        if (VerticalScrollPolicy == FormatCodeViewScrollPolicy.Never || MaxVerticalScroll <= 0)
            return false;
        SetVerticalScroll(_scrollY - input.WheelDeltaNotches * _lineHeight * 3);
        return true;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;
        bool control = input.KeyModifiers.HasFlag(KeyboardModifierState.Control);
        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);

        if (control && IsKey(input, BVirtualKey.A, "A"))
        {
            SelectAll();
            EnsureCaretVisible();
            return true;
        }
        if (control && IsKey(input, BVirtualKey.C, "C"))
            return CopySelection();
        if (control && IsKey(input, 0x58, "X"))
            return CutSelection();
        if (control && IsKey(input, 0x56, "V"))
            return Paste();
        if (control && IsKey(input, 0x5A, "Z"))
        {
            RequestUndo();
            return true;
        }
        if (control && IsKey(input, 0x59, "Y"))
        {
            RequestRedo();
            return true;
        }
        if (control && IsKey(input, 0x46, "F"))
        {
            RequestSearch();
            return true;
        }
        if (IsKey(input, 0x72, "F3"))
        {
            bool found = shift ? FindPrevious() : FindNext();
            if (found)
                EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.Escape, "Escape"))
        {
            RequestExit();
            return true;
        }
        if (IsKey(input, BVirtualKey.Enter, "Enter") || IsKey(input, BVirtualKey.Space, "Space"))
        {
            ActivateAt(CaretOffset);
            return true;
        }
        if (IsKey(input, BVirtualKey.Back, "Backspace"))
            return DeleteProjected(backward: true);
        if (IsKey(input, 0x2E, "Delete"))
            return DeleteProjected(backward: false);
        if (IsKey(input, BVirtualKey.Left, "Left"))
        {
            MoveCaret(control ? PreviousTokenBoundary(CaretOffset) : PreviousTextBoundary(CaretOffset), shift);
            EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.Right, "Right"))
        {
            MoveCaret(control ? NextTokenBoundary(CaretOffset) : NextTextBoundary(CaretOffset), shift);
            EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.Up, "Up"))
        {
            MoveVertical(-1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Down, "Down"))
        {
            MoveVertical(1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Home, "Home"))
        {
            MoveCaret(control ? 0 : LineForOffset(CaretOffset).Start, shift);
            EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.End, "End"))
        {
            MoveCaret(control ? Text.Length : LineForOffset(CaretOffset).End, shift);
            EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
        {
            MovePage(-1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
        {
            MovePage(1, shift);
            return true;
        }

        return false;
    }

    private bool DeleteProjected(bool backward)
    {
        if (!IsEditable)
            return false;
        if (HasSelection)
            return RequestTextReplacement(string.Empty);
        if (RequestTokenRemoval(backward))
            return true;

        int target = backward ? PreviousTextBoundary(CaretOffset) : NextTextBoundary(CaretOffset);
        if (target == CaretOffset)
            return false;
        int start = Math.Min(target, CaretOffset);
        int end = Math.Max(target, CaretOffset);
        SetSelection(start, end);
        if (RequestTextReplacement(string.Empty))
            return true;
        SetSelection(CaretOffset, CaretOffset);
        return false;
    }

    private void MoveVertical(int direction, bool extend)
    {
        EnsureLayout();
        int index = LineIndexForOffset(CaretOffset);
        VisualLine current = _lines[index];
        int column = CaretOffset - current.Start;
        int targetIndex = Math.Clamp(index + direction, 0, _lines.Count - 1);
        VisualLine target = _lines[targetIndex];
        MoveCaret(Math.Min(target.End, target.Start + column), extend);
        EnsureCaretVisible();
    }

    private void MovePage(int direction, bool extend)
    {
        EnsureLayout();
        int current = LineIndexForOffset(CaretOffset);
        int perPage = Math.Max(1, (int)(ViewportHeight / _lineHeight));
        int target = Math.Clamp(current + direction * perPage, 0, _lines.Count - 1);
        int column = CaretOffset - _lines[current].Start;
        MoveCaret(Math.Min(_lines[target].End, _lines[target].Start + column), extend);
        EnsureCaretVisible();
    }

    private int PreviousTextBoundary(int offset)
    {
        if (offset <= 0)
            return 0;
        int result = offset - 1;
        if (result > 0 && char.IsLowSurrogate(Text[result]) && char.IsHighSurrogate(Text[result - 1]))
            result--;
        return result;
    }

    private int NextTextBoundary(int offset)
    {
        if (offset >= Text.Length)
            return Text.Length;
        int result = offset + 1;
        if (char.IsHighSurrogate(Text[offset]) && result < Text.Length && char.IsLowSurrogate(Text[result]))
            result++;
        return result;
    }

    private int PreviousTokenBoundary(int offset)
    {
        if (Projection is null)
            return PreviousTextBoundary(offset);
        for (int index = Projection.Tokens.Count - 1; index >= 0; index--)
        {
            int start = Projection.Tokens[index].ProjectedStart;
            if (start < offset)
                return start;
        }
        return 0;
    }

    private int NextTokenBoundary(int offset)
    {
        if (Projection is null)
            return NextTextBoundary(offset);
        foreach (FormatCodeToken token in Projection.Tokens)
        {
            int end = token.ProjectedStart + token.ProjectedLength;
            if (end > offset)
                return end;
        }
        return Text.Length;
    }

    private int OffsetFromPoint(BPoint point)
    {
        EnsureLayout();
        int lineIndex = Math.Clamp(
            (int)Math.Floor((point.Y - ContentTop + _scrollY) / _lineHeight),
            0,
            _lines.Count - 1);
        VisualLine line = _lines[lineIndex];
        int column = (int)Math.Round((point.X - ContentLeft + _scrollX) / _characterAdvance);
        int offset = Math.Clamp(line.Start + column, line.Start, line.End);
        if (offset > line.Start && offset < Text.Length &&
            char.IsLowSurrogate(Text[offset]) && char.IsHighSurrogate(Text[offset - 1]))
        {
            offset--;
        }
        return offset;
    }

    private void EnsureCaretVisible()
    {
        EnsureLayout();
        VisualLine line = LineForOffset(CaretOffset);
        if (line.Top < _scrollY)
            SetVerticalScroll(line.Top);
        else if (line.Top + _lineHeight > _scrollY + ViewportHeight)
            SetVerticalScroll(line.Top + _lineHeight - ViewportHeight);

        double x = (CaretOffset - line.Start) * _characterAdvance;
        if (x < _scrollX)
            SetHorizontalScroll(x);
        else if (x + _characterAdvance > _scrollX + ViewportWidth)
            SetHorizontalScroll(x + _characterAdvance - ViewportWidth);
    }

    private void EnsureLayout()
    {
        double width = ViewportWidth;
        if (ReferenceEquals(_layoutProjection, Projection) &&
            Equals(_layoutFont, Font) &&
            _layoutWrapping == Wrapping &&
            _layoutWidth == width)
        {
            return;
        }

        _lines.Clear();
        _characterAdvance = Math.Max(1, BTextMeasurer.MeasureAdvance("M", Font));
        _lineHeight = Math.Max(1, BTextMeasurer.GetLineHeight(Font));
        _contentWidth = 0;
        int maxColumns = Wrapping == FormatCodeViewWrapping.Wrap && width > 0
            ? Math.Max(1, (int)Math.Floor(width / _characterAdvance))
            : int.MaxValue;
        int start = 0;
        double top = 0;
        while (start <= Text.Length)
        {
            int hardEnd = Text.IndexOf('\n', start);
            if (hardEnd < 0)
                hardEnd = Text.Length;
            if (hardEnd == start)
            {
                AddLine(start, start, ref top);
            }
            else
            {
                int segmentStart = start;
                while (segmentStart < hardEnd)
                {
                    int end = maxColumns == int.MaxValue
                        ? hardEnd
                        : Math.Min(hardEnd, segmentStart + maxColumns);
                    if (end < hardEnd && end > segmentStart &&
                        char.IsHighSurrogate(Text[end - 1]) && char.IsLowSurrogate(Text[end]))
                    {
                        end--;
                    }
                    if (end == segmentStart)
                        end = Math.Min(hardEnd, segmentStart + 2);
                    AddLine(segmentStart, end, ref top);
                    segmentStart = end;
                }
            }

            if (hardEnd == Text.Length)
                break;
            start = hardEnd + 1;
            if (start == Text.Length)
            {
                AddLine(start, start, ref top);
                break;
            }
        }

        if (_lines.Count == 0)
            AddLine(0, 0, ref top);
        _contentHeight = top;
        _layoutProjection = Projection;
        _layoutFont = Font;
        _layoutWrapping = Wrapping;
        _layoutWidth = width;
        _scrollX = ClampHorizontalScroll(_scrollX);
        _scrollY = ClampVerticalScroll(_scrollY);
    }

    private void AddLine(int start, int end, ref double top)
    {
        _lines.Add(new VisualLine(start, end, top));
        _contentWidth = Math.Max(_contentWidth, (end - start) * _characterAdvance);
        top += _lineHeight;
    }

    private (int Start, int End) VisibleTextRange(VisualLine line)
    {
        int firstColumn = Math.Max(0, (int)Math.Floor(_scrollX / _characterAdvance));
        int visibleColumns = Math.Max(1, (int)Math.Ceiling(ViewportWidth / _characterAdvance) + 2);
        int start = Math.Min(line.End, line.Start + firstColumn);
        int end = Math.Min(line.End, start + visibleColumns);
        if (start > line.Start && start < Text.Length &&
            char.IsLowSurrogate(Text[start]) && char.IsHighSurrogate(Text[start - 1]))
        {
            start--;
        }
        if (end < line.End && end > 0 && char.IsHighSurrogate(Text[end - 1]) && char.IsLowSurrogate(Text[end]))
            end++;
        return (start, end);
    }

    private (int First, int Last) VisibleLineRange()
    {
        int first = Math.Clamp((int)Math.Floor(_scrollY / _lineHeight), 0, _lines.Count - 1);
        int last = Math.Clamp(
            (int)Math.Ceiling((_scrollY + ViewportHeight) / _lineHeight),
            first,
            _lines.Count - 1);
        return (first, last);
    }

    private VisualLine LineForOffset(int offset) => _lines[LineIndexForOffset(offset)];

    private int LineIndexForOffset(int offset)
    {
        EnsureLayout();
        offset = Math.Clamp(offset, 0, Text.Length);
        int low = 0;
        int high = _lines.Count - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            VisualLine line = _lines[middle];
            if (offset < line.Start)
                high = middle - 1;
            else if (offset > line.End || (offset == line.End && middle + 1 < _lines.Count && _lines[middle + 1].Start == offset))
                low = middle + 1;
            else
                return middle;
        }
        return Math.Clamp(low, 0, _lines.Count - 1);
    }

    private int FindTokenIndex(int offset)
    {
        if (Projection is null)
            return 0;
        int low = 0;
        int high = Projection.Tokens.Count - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            FormatCodeToken token = Projection.Tokens[middle];
            if (offset < token.ProjectedStart)
                high = middle - 1;
            else if (offset >= token.ProjectedStart + token.ProjectedLength)
                low = middle + 1;
            else
                return middle;
        }
        return Math.Clamp(low, 0, Math.Max(0, Projection.Tokens.Count - 1));
    }

    private bool TryBeginScrollbarInteraction(BPoint point)
    {
        if (HasVerticalScrollbar && VerticalScrollbarTrack.Contains(point))
        {
            BeginScrollbarDrag(ScrollbarAxis.Vertical, point.Y, VerticalScrollbarThumb);
            return true;
        }
        if (HasHorizontalScrollbar && HorizontalScrollbarTrack.Contains(point))
        {
            BeginScrollbarDrag(ScrollbarAxis.Horizontal, point.X, HorizontalScrollbarThumb);
            return true;
        }
        return false;
    }

    private void BeginScrollbarDrag(ScrollbarAxis axis, double coordinate, BRect thumb)
    {
        _dragAxis = axis;
        double start = axis == ScrollbarAxis.Vertical ? thumb.Top : thumb.Left;
        double length = axis == ScrollbarAxis.Vertical ? thumb.Height : thumb.Width;
        if (coordinate >= start && coordinate <= start + length)
        {
            _scrollbarDragOffset = coordinate - start;
            return;
        }
        _scrollbarDragOffset = length / 2;
        DragScrollbar(axis == ScrollbarAxis.Vertical
            ? new BPoint(0, coordinate)
            : new BPoint(coordinate, 0));
    }

    private void DragScrollbar(BPoint point)
    {
        if (_dragAxis == ScrollbarAxis.Vertical)
        {
            BRect track = VerticalScrollbarTrack;
            BRect thumb = VerticalScrollbarThumb;
            double travel = track.Height - thumb.Height;
            if (travel > 0)
                SetVerticalScroll(((point.Y - _scrollbarDragOffset - track.Top) / travel) * MaxVerticalScroll);
        }
        else if (_dragAxis == ScrollbarAxis.Horizontal)
        {
            BRect track = HorizontalScrollbarTrack;
            BRect thumb = HorizontalScrollbarThumb;
            double travel = track.Width - thumb.Width;
            if (travel > 0)
                SetHorizontalScroll(((point.X - _scrollbarDragOffset - track.Left) / travel) * MaxHorizontalScroll);
        }
    }

    private void DrawScrollbars(BRenderList list)
    {
        if (HasVerticalScrollbar)
        {
            StandardControlPaint.FillRounded(list, VerticalScrollbarTrack, ScrollbarTrack, StandardControlPaint.PillRadius);
            StandardControlPaint.FillRounded(list, VerticalScrollbarThumb, ScrollbarThumb, StandardControlPaint.PillRadius);
        }
        if (HasHorizontalScrollbar)
        {
            StandardControlPaint.FillRounded(list, HorizontalScrollbarTrack, ScrollbarTrack, StandardControlPaint.PillRadius);
            StandardControlPaint.FillRounded(list, HorizontalScrollbarThumb, ScrollbarThumb, StandardControlPaint.PillRadius);
        }
    }

    private void SetVerticalScroll(double value)
    {
        double next = ClampVerticalScroll(value);
        if (next == _scrollY)
            return;
        _scrollY = next;
        Invalidate(UiInvalidationKind.Render);
    }

    private void SetHorizontalScroll(double value)
    {
        double next = ClampHorizontalScroll(value);
        if (next == _scrollX)
            return;
        _scrollX = next;
        Invalidate(UiInvalidationKind.Render);
    }

    private double ClampVerticalScroll(double value) => Math.Clamp(value, 0, MaxVerticalScroll);

    private double ClampHorizontalScroll(double value) => Math.Clamp(value, 0, MaxHorizontalScroll);

    private double MaxVerticalScroll => VerticalScrollPolicy == FormatCodeViewScrollPolicy.Never
        ? 0
        : Math.Max(0, _contentHeight - ViewportHeight);

    private double MaxHorizontalScroll => HorizontalScrollPolicy == FormatCodeViewScrollPolicy.Never ||
        Wrapping == FormatCodeViewWrapping.Wrap
        ? 0
        : Math.Max(0, _contentWidth - ViewportWidth);

    private BRect ViewportBounds => new(ContentLeft, ContentTop, ViewportWidth, ViewportHeight);

    private double ContentLeft => Bounds.Left + PaddingX;

    private double ContentTop => Bounds.Top + PaddingY;

    private double ViewportWidth => Math.Max(0, Bounds.Width - (PaddingX * 2));

    private double ViewportHeight => Math.Max(0, Bounds.Height - (PaddingY * 2));

    private BRect VerticalScrollbarTrack => new(
        Bounds.Right - ScrollbarThickness,
        Bounds.Top + PaddingY,
        ScrollbarThickness,
        Math.Max(0, Bounds.Height - (PaddingY * 2) - (HasHorizontalScrollbar ? ScrollbarThickness : 0)));

    private BRect HorizontalScrollbarTrack => new(
        Bounds.Left + PaddingX,
        Bounds.Bottom - ScrollbarThickness,
        Math.Max(0, Bounds.Width - (PaddingX * 2) - (HasVerticalScrollbar ? ScrollbarThickness : 0)),
        ScrollbarThickness);

    private BRect VerticalScrollbarThumb => Thumb(VerticalScrollbarTrack, vertical: true);

    private BRect HorizontalScrollbarThumb => Thumb(HorizontalScrollbarTrack, vertical: false);

    private BRect Thumb(BRect track, bool vertical)
    {
        double viewport = vertical ? ViewportHeight : ViewportWidth;
        double content = vertical ? _contentHeight : _contentWidth;
        double max = vertical ? MaxVerticalScroll : MaxHorizontalScroll;
        double trackLength = vertical ? track.Height : track.Width;
        double length = max <= 0
            ? trackLength
            : Math.Clamp(trackLength * (viewport / Math.Max(viewport, content)),
                Math.Min(MinimumScrollbarThumbLength, trackLength), trackLength);
        double start = vertical ? track.Top : track.Left;
        if (max > 0)
            start += (trackLength - length) * ((vertical ? _scrollY : _scrollX) / max);
        return vertical
            ? new BRect(track.Left, start, track.Width, length)
            : new BRect(start, track.Top, length, track.Height);
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    private readonly record struct VisualLine(int Start, int End, double Top);

    private enum ScrollbarAxis
    {
        None = 0,
        Vertical,
        Horizontal,
    }
}
