using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.Input.Touch;
using Broiler.UI.Standard;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// The Broiler-drawn standard <see cref="UiRichEdit"/>. It lays out the document
/// into wrapped visual lines, renders per-run styled text (family, size, bold,
/// italic, underline, strike, foreground, and background), the selection, caret, and placeholder,
/// supports vertical scrolling, and hit-tests points to positions. Keyboard, text,
/// and IME input drive caret/selection navigation plus editing and formatting
/// through the <see cref="UiRichEdit"/> command surface and its single undo model.
/// No native control or OS API is used.
/// </summary>
public sealed class StandardRichEdit : UiRichEdit, IStandardThemedControl, IUiTextEditor
{
    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        Foreground = theme.Text;
        PlaceholderForeground = theme.TextDisabled;
        BorderColor = theme.Border;
        FocusRing = theme.FocusRing;
        SelectionBackground = theme.AccentSoft;
        SecondarySelectionBackground = theme.Warning;
        CaretColor = theme.Text;
    }

    private readonly List<VisualLine> _lines = [];
    private readonly Dictionary<InlineImage, BImageHandle> _imageHandles = new(ReferenceEqualityComparer.Instance);
    private RichTextDocument? _layoutDocument;
    private BFontStyle? _layoutFont;
    private double _layoutWidth = double.NaN;
    private bool _layoutValid;
    private double _contentHeight;
    private double _scrollY;
    private UiTimestamp _lastClickTime;
    private BPoint _lastClickPosition;
    private bool _hasClicked;
    private string _compositionText = string.Empty;
    private bool _isDraggingScrollbar;
    private double _scrollbarDragOffset;
    private long? _touchContactId;
    private BPoint _touchStart;
    private BPoint _touchLast;
    private bool _isTouchScrolling;

    private const double TouchScrollThreshold = 6;

    /// <summary>The box an image with no known size is drawn in, and the minimum for any image.</summary>
    private const double FallbackImageExtent = 72;

    /// <summary>Space left around an inline image so it does not touch the text above and below it.</summary>
    private const double ImageMargin = 2;

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor PlaceholderForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BColor SelectionBackground { get; set; } = BColor.FromArgb(0xFF, 0xC7, 0xDD, 0xFA);

    public BColor SecondarySelectionBackground { get; set; } = BColor.FromArgb(0xFF, 0xFF, 0xF0, 0xB3);

    public BColor CaretColor { get; set; } = BColor.Black;

    public BColor ScrollbarTrack { get; set; } = BColor.FromArgb(0x33, 0x94, 0xA3, 0xB8);

    public BColor ScrollbarThumb { get; set; } = BColor.FromArgb(0xAA, 0x7D, 0x8D, 0xA3);

    public double ScrollbarThickness { get; set; } = 12;

    public double MinimumScrollbarThumbLength { get; set; } = 18;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double PaddingX { get; set; } = 8;

    public double PaddingY { get; set; } = 6;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public double VerticalScrollOffset => _scrollY;

    /// <summary>The in-progress IME composition text, or empty when not composing.</summary>
    public string CompositionText => _compositionText;

    protected override bool IsCompositionActive => _compositionText.Length > 0;

    public UiTextEditorMetrics GetTextEditorMetrics()
    {
        int start = FlatIndex(Selection.Start);
        int end = FlatIndex(Selection.End);
        int compositionStart = _compositionText.Length > 0 ? FlatIndex(Selection.Focus) : -1;
        int compositionEnd = compositionStart < 0 ? -1 : compositionStart + _compositionText.Length;
        return new UiTextEditorMetrics(Document.PlainText.Length, start, end, compositionStart, compositionEnd);
    }

    /// <summary>
    /// A formatted document materializes its plain text to answer this. That is
    /// acceptable for RichEdit's document sizes and would not be for a source
    /// buffer, which is why the contract is a bounded range rather than a whole
    /// string: the cost stays with the implementation that can afford it.
    /// </summary>
    public string GetTextEditorRange(int start, int maxLength) =>
        UiTextEditorRange.Slice(Document.PlainText, start, maxLength);

    public bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        if (IsReadOnly || !IsEnabled)
            return false;

        string text = Document.PlainText;
        int caret = FlatIndex(Selection.Focus);
        int start = Math.Max(0, caret - Math.Max(0, beforeLength));
        int end = Math.Min(text.Length, caret + Math.Max(0, afterLength));
        if (!Selection.IsEmpty)
        {
            start = Math.Min(start, FlatIndex(Selection.Start));
            end = Math.Max(end, FlatIndex(Selection.End));
        }
        if (end <= start)
            return false;

        Selection = new RichTextRange(PositionFromFlatIndex(start), PositionFromFlatIndex(end));
        bool changed = DeleteCurrentSelection();
        EnsureCaretVisible();
        return changed;
    }

    public bool SetEditorSelection(int start, int end)
    {
        int textLength = Document.PlainText.Length;
        int clampedStart = Math.Clamp(Math.Min(start, end), 0, textLength);
        int clampedEnd = Math.Clamp(Math.Max(start, end), clampedStart, textLength);
        Selection = new RichTextRange(PositionFromFlatIndex(clampedStart), PositionFromFlatIndex(clampedEnd));
        EnsureCaretVisible();
        return true;
    }

    public bool SetComposingRegion(int start, int end) => SetEditorSelection(start, end);

    public bool PerformEditorAction(UiTextEditorAction action)
    {
        if (action == UiTextEditorAction.None)
            return false;

        if (AcceptsReturn && action is not UiTextEditorAction.Next and not UiTextEditorAction.Previous)
            return RunCommand(RichEditCommand.InsertParagraphBreak);

        Submit();
        return true;
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        double width = ClampDesired(PreferredSize.Width, availableSize.Width);
        double height = ClampDesired(PreferredSize.Height, availableSize.Height);
        return new BSize(width, height);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        EnsureLayout();
        BRenderList renderList = context.RenderList;
        BRect inner = InnerBounds;
        bool focused = Session?.FocusedElement == this;

        StandardControlPaint.FillRounded(renderList, Bounds, IsEnabled ? Background : StandardControlPaint.SurfaceDisabled, CornerRadius);
        StandardControlPaint.StrokeRounded(renderList, Bounds, focused ? FocusRing : BorderColor, CornerRadius, focused ? 2 : 1);

        renderList.PushClip(inner);
        if (Document.PlainText.Length == 0 && _compositionText.Length == 0 && PlaceholderText.Length > 0)
        {
            renderList.DrawText(new BTextRun(PlaceholderText, Font, PlaceholderForeground), new BPoint(ContentLeft, ContentTop - _scrollY));
        }
        else
        {
            DrawRunBackgrounds(renderList, inner);
            DrawRange(renderList, inner, SecondarySelection, SecondarySelectionBackground);
            DrawSelection(renderList, inner);
            DrawText(renderList, inner);
            DrawComposition(renderList, inner, focused);
        }

        DrawCaret(renderList, focused);
        renderList.PopClip();
        DrawScrollbar(renderList);
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
            UiInputEventKind.TouchContact => HandleTouch(input),
            UiInputEventKind.TextInput => HandleTextInput(input),
            UiInputEventKind.TextComposition => HandleTextComposition(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        // A layout built before the session existed sized its images from the
        // fallback box, because no host was there to decode them. Rebuild it now
        // that one is, or those sizes would stick until the document changes.
        _layoutValid = false;
    }

    protected override void OnDetached()
    {
        if (Session?.Host is IUiTextInputHost textInput)
            textInput.ClearCaret(this);

        // The handles belong to the detaching session's renderer, so they are
        // released here rather than kept for a session that cannot draw them.
        ReleaseImages();
        base.OnDetached();
    }

    // --- Rendering ---------------------------------------------------------

    private void DrawScrollbar(BRenderList renderList)
    {
        if (!HasVerticalScrollbar)
            return;

        BRect track = ScrollbarTrackBounds;
        BRect thumb = ScrollbarThumbBounds;
        StandardControlPaint.FillRounded(renderList, track, ScrollbarTrack, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(renderList, thumb, ScrollbarThumb, StandardControlPaint.PillRadius);
    }

    private void DrawSelection(BRenderList renderList, BRect inner)
    {
        DrawRange(renderList, inner, Selection, SelectionBackground);
    }

    private void DrawRange(BRenderList renderList, BRect inner, RichTextRange? range, BColor color)
    {
        if (range is not RichTextRange selection || selection.IsEmpty)
            return;

        RichTextPosition start = selection.Start;
        RichTextPosition end = selection.End;
        foreach (VisualLine line in _lines)
        {
            var lineStart = new RichTextPosition(line.ParagraphIndex, line.Start);
            var lineEnd = new RichTextPosition(line.ParagraphIndex, line.End);

            bool fullyInside = start <= lineStart && end >= lineEnd;
            if (!fullyInside && (end <= lineStart || start >= lineEnd))
                continue;

            double y = ContentTop + line.Top - _scrollY;
            if (y + line.Height < inner.Top || y > inner.Bottom)
                continue;

            int subStart = start.ParagraphIndex == line.ParagraphIndex ? Math.Clamp(start.Offset, line.Start, line.End) : line.Start;
            int subEnd = end.ParagraphIndex == line.ParagraphIndex ? Math.Clamp(end.Offset, line.Start, line.End) : line.End;
            if (start.ParagraphIndex < line.ParagraphIndex)
                subStart = line.Start;
            if (end.ParagraphIndex > line.ParagraphIndex)
                subEnd = line.End;

            RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
            double x1 = ContentLeft + MeasureAdvance(paragraph, line.Start, subStart);
            double x2 = ContentLeft + MeasureAdvance(paragraph, line.Start, subEnd);
            double width = x2 - x1;
            if (width <= 0)
            {
                if (!fullyInside)
                    continue;
                width = BTextMeasurer.MeasureAdvance(" ", Font); // sliver marking an empty selected line
            }

            renderList.FillRect(new BRect(x1, y, width, line.Height), color);
        }
    }

    private void DrawRunBackgrounds(BRenderList renderList, BRect inner)
    {
        if (!IsEnabled)
            return;

        foreach (VisualLine line in _lines)
        {
            double y = ContentTop + line.Top - _scrollY;
            if (y + line.Height < inner.Top || y > inner.Bottom || line.End <= line.Start)
                continue;

            foreach (LineSegment segment in LineSegments(line))
            {
                if (!segment.Style.Background.IsEmpty && segment.Advance > 0)
                    renderList.FillRect(new BRect(segment.X, y, segment.Advance, line.Height), segment.Style.Background);
            }
        }
    }

    private void DrawText(BRenderList renderList, BRect inner)
    {
        BColor fallback = IsEnabled ? Foreground : PlaceholderForeground;
        foreach (VisualLine line in _lines)
        {
            double y = ContentTop + line.Top - _scrollY;
            if (y + line.Height < inner.Top || y > inner.Bottom || line.End <= line.Start)
                continue;

            foreach (LineSegment segment in LineSegments(line))
            {
                BColor color = IsEnabled && !segment.Style.Foreground.IsEmpty ? segment.Style.Foreground : fallback;
                if (segment.Image is InlineImage image)
                {
                    DrawInlineImage(renderList, image, segment, y, line.Height, color);
                    continue;
                }

                renderList.DrawText(new BTextRun(segment.Text, segment.Font, color), new BPoint(segment.X, y));
                DrawDecorations(renderList, segment, y, line.Height, color);
            }
        }
    }

    /// <summary>
    /// Draws one inline picture, bottom-aligned in its line the way Word sits an
    /// inline image on the text baseline. An image the backend could not decode
    /// is drawn as an outlined box, so the document still shows where it is.
    /// </summary>
    private void DrawInlineImage(
        BRenderList renderList,
        InlineImage image,
        LineSegment segment,
        double lineTop,
        double lineHeight,
        BColor color)
    {
        BSize size = ImageDisplaySize(image);
        double height = Math.Min(size.Height, Math.Max(0, lineHeight - (ImageMargin * 2)));
        double top = lineTop + Math.Max(ImageMargin, lineHeight - height - ImageMargin);
        var destination = new BRect(segment.X, top, segment.Advance, height);

        BImageHandle handle = ResolveImage(image);
        if (handle.IsValid)
        {
            var source = new BRect(0, 0, handle.PixelSize.Width, handle.PixelSize.Height);
            renderList.DrawImage(handle, source, destination, IsEnabled ? 1.0 : 0.5);
            return;
        }

        StandardControlPaint.StrokeRounded(renderList, destination, color, StandardControlPaint.ControlRadius, 1);
    }

    private void DrawDecorations(BRenderList renderList, LineSegment segment, double y, double lineHeight, BColor color)
    {
        if (segment.Advance <= 0 || (!segment.Style.Underline && !segment.Style.Strikethrough))
            return;

        double thickness = Math.Max(1, Math.Round(segment.Font.SizeInPixels / 14));
        if (segment.Style.Underline)
            renderList.FillRect(new BRect(segment.X, y + lineHeight - thickness - 1, segment.Advance, thickness), color);
        if (segment.Style.Strikethrough)
            renderList.FillRect(new BRect(segment.X, y + (lineHeight / 2), segment.Advance, thickness), color);
    }

    private void DrawComposition(BRenderList renderList, BRect inner, bool focused)
    {
        if (!focused || _compositionText.Length == 0)
            return;

        VisualLine line = LineForPosition(Selection.Focus).Line;
        double y = ContentTop + line.Top - _scrollY;
        if (y + line.Height < inner.Top || y > inner.Bottom)
            return;

        double x = CaretX(Selection.Focus);
        InlineStyle style = CaretInlineStyle;
        BFontStyle font = RunFont(style);
        double advance = MeasurePieces(_compositionText, style, font);
        BColor color = IsEnabled ? Foreground : PlaceholderForeground;

        // Preedit text is drawn with the capitalization it will take once
        // committed, so the text does not jump when the IME finishes.
        double pieceX = x;
        foreach (ShapedPiece piece in ShapePieces(_compositionText, style, font))
        {
            renderList.DrawText(new BTextRun(piece.Text, piece.Font, color), new BPoint(pieceX, y));
            pieceX += BTextMeasurer.MeasureAdvance(piece.Text, piece.Font);
        }

        renderList.FillRect(new BRect(x, y + line.Height - 2, advance, 1), color); // composition underline
    }

    /// <summary>
    /// Splits a visual line into contiguous styled segments, each carrying its
    /// resolved font, on-screen x origin, and advance.
    /// </summary>
    private IEnumerable<LineSegment> LineSegments(VisualLine line)
    {
        RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
        double x = ContentLeft;
        int pos = 0;
        foreach (StyleRun run in paragraph.Runs)
        {
            int runStart = pos;
            int runEnd = pos + run.Length;
            pos = runEnd;

            int segStart = Math.Max(runStart, line.Start);
            int segEnd = Math.Min(runEnd, line.End);
            if (segEnd <= segStart)
                continue;

            string text = paragraph.Text.Substring(segStart, segEnd - segStart);
            if (run.Style.Image is InlineImage image)
            {
                // Every placeholder character in an image run draws the image, so
                // a run that happens to hold two of them draws two pictures
                // rather than one stretched across both character positions.
                foreach (char character in text)
                {
                    if (character == InlineImage.Placeholder)
                    {
                        double width = ImageDisplaySize(image).Width;
                        yield return LineSegment.ForImage(image, run.Style, x, width);
                        x += width;
                        continue;
                    }

                    string single = character.ToString();
                    double advance = MeasurePieces(single, run.Style, RunFont(run.Style));
                    yield return new LineSegment(single, run.Style, RunFont(run.Style), x, advance);
                    x += advance;
                }

                continue;
            }

            foreach (ShapedPiece piece in ShapePieces(text, run.Style, RunFont(run.Style)))
            {
                double advance = BTextMeasurer.MeasureAdvance(piece.Text, piece.Font);
                yield return new LineSegment(piece.Text, run.Style, piece.Font, x, advance);
                x += advance;
            }
        }
    }

    /// <summary>
    /// The size an inline image is drawn at: the display size the document
    /// states, else the decoded pixel size, else a fixed box. The box keeps a
    /// picture the backend could not decode visible and selectable instead of
    /// collapsing it to nothing.
    /// </summary>
    private BSize ImageDisplaySize(InlineImage image)
    {
        if (image.HasExplicitSize)
            return new BSize(image.Width, image.Height);

        BImageHandle handle = ResolveImage(image);
        if (handle.IsValid && handle.PixelSize.Width > 0 && handle.PixelSize.Height > 0)
            return handle.PixelSize;

        return new BSize(FallbackImageExtent, FallbackImageExtent);
    }

    /// <summary>
    /// The backend handle for an image, created once per image object and kept
    /// until the control is detached. A host with no image capability, or bytes
    /// the backend cannot decode, cache as invalid so the failure is not retried
    /// on every frame.
    /// </summary>
    private BImageHandle ResolveImage(InlineImage image)
    {
        if (_imageHandles.TryGetValue(image, out BImageHandle cached))
            return cached;

        BImageHandle handle = BImageHandle.Invalid;
        if (Session?.Host is IUiImageHost imageHost && !image.Data.IsEmpty)
            handle = imageHost.CreateImage(image.Data.Span);

        _imageHandles[image] = handle;
        return handle;
    }

    private void ReleaseImages()
    {
        if (Session?.Host is IUiImageHost imageHost)
        {
            foreach (BImageHandle handle in _imageHandles.Values)
            {
                if (handle.IsValid)
                    imageHost.ReleaseImage(handle);
            }
        }

        _imageHandles.Clear();
    }

    /// <summary>How much smaller a small-caps letter is drawn than a full capital.</summary>
    private const double SmallCapsScale = 0.8;

    /// <summary>A stretch of a run as it is drawn: the glyphs, and the font to draw them with.</summary>
    private readonly record struct ShapedPiece(string Text, BFontStyle Font);

    /// <summary>
    /// The pieces a stored substring is drawn as. Capitalization is a display
    /// property — the document keeps the casing the author typed, and this is the
    /// only place it becomes capitals, so turning it off restores the original
    /// text exactly. Small caps additionally splits the substring wherever the
    /// stored case changes, so letters typed in lower case can be drawn smaller
    /// than the ones typed as capitals.
    /// </summary>
    private static IEnumerable<ShapedPiece> ShapePieces(string text, InlineStyle style, BFontStyle font)
    {
        if (text.Length == 0)
            yield break;

        if (style.Capitalization == TextCapitalization.AllCaps)
        {
            yield return new ShapedPiece(text.ToUpperInvariant(), font);
            yield break;
        }

        if (style.Capitalization != TextCapitalization.SmallCaps)
        {
            yield return new ShapedPiece(text, font);
            yield break;
        }

        BFontStyle reduced = font with { SizeInPixels = Math.Max(1, font.SizeInPixels * SmallCapsScale) };
        int start = 0;
        bool small = char.IsLower(text[0]);
        for (int i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && char.IsLower(text[i]) == small)
                continue;

            string piece = text[start..i];
            yield return small
                ? new ShapedPiece(piece.ToUpperInvariant(), reduced)
                : new ShapedPiece(piece, font);

            if (i < text.Length)
            {
                start = i;
                small = char.IsLower(text[i]);
            }
        }
    }

    /// <summary>
    /// The advance of a stored substring as drawn. Every measurement goes through
    /// here so caret placement, selection rectangles, and wrapping agree with what
    /// <see cref="DrawText"/> puts on screen.
    /// </summary>
    private static double MeasurePieces(string text, InlineStyle style, BFontStyle font)
    {
        if (style.Capitalization == TextCapitalization.None)
            return BTextMeasurer.MeasureAdvance(text, font);

        double advance = 0;
        foreach (ShapedPiece piece in ShapePieces(text, style, font))
            advance += BTextMeasurer.MeasureAdvance(piece.Text, piece.Font);

        return advance;
    }

    /// <summary>
    /// The advance of a substring of one run, counting a picture as the width it
    /// is drawn at. Caret placement, selection rectangles, and wrapping all come
    /// through here, so an image occupies the same horizontal space in each.
    /// </summary>
    private double MeasureRunText(string text, InlineStyle style)
    {
        if (style.Image is not InlineImage image)
            return MeasurePieces(text, style, RunFont(style));

        double advance = 0;
        foreach (char character in text)
        {
            advance += character == InlineImage.Placeholder
                ? ImageDisplaySize(image).Width
                : MeasurePieces(character.ToString(), style, RunFont(style));
        }

        return advance;
    }

    private BFontStyle RunFont(InlineStyle style)
    {
        return Font with
        {
            FamilyName = string.IsNullOrWhiteSpace(style.FontFamily) ? Font.FamilyName : style.FontFamily,
            SizeInPixels = style.FontSize is > 0 ? style.FontSize.Value : Font.SizeInPixels,
            Weight = style.Bold ? BFontWeight.Bold : Font.Weight,
            Slant = style.Italic ? BFontSlant.Italic : Font.Slant,
        };
    }

    private void DrawCaret(BRenderList renderList, bool focused)
    {
        if (!focused || !IsEnabled)
            return;

        renderList.FillRect(CaretRect(Selection.Focus), CaretColor);
    }

    private void PublishCaret(bool focused)
    {
        if (!focused || Session?.Host is not IUiTextInputHost textInput)
            return;

        int caret = FlatIndex(Selection.Focus);
        int start = FlatIndex(Selection.Start);
        int end = FlatIndex(Selection.End);
        textInput.PublishCaret(new UiTextCaretInfo(this, CaretRect(Selection.Focus), caret, start, end - start, IsCompositionActive));
    }

    private bool HandleTextInput(UiInputEvent input) => InsertCommittedText(input.Text ?? string.Empty);

    private bool HandleTextComposition(UiInputEvent input)
    {
        TextCompositionState state = input.CompositionState ?? TextCompositionState.Updated;
        if (state is TextCompositionState.Started or TextCompositionState.Updated)
        {
            _compositionText = input.Text ?? string.Empty;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            return true;
        }

        if (state == TextCompositionState.Committed)
        {
            _compositionText = string.Empty;
            return InsertCommittedText(input.Text ?? string.Empty);
        }

        _compositionText = string.Empty;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    private bool InsertCommittedText(string text)
    {
        text = SanitizeCommittedText(text);
        if (text.Length == 0)
            return false;

        bool changed = ExecuteCommand(RichEditCommand.InsertText, text);
        EnsureCaretVisible();
        return changed;
    }

    // --- Input -------------------------------------------------------------

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

            RichTextPosition position = PositionFromPoint(input.Position);
            if (IsDoubleClick(input.Position))
            {
                SelectWordAt(position);
            }
            else
            {
                Selection = RichTextRange.Caret(position);
                EnsureCaretVisible();
            }

            UpdateClickState(input.Position);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            _isDraggingScrollbar = false;
            Session?.ReleaseInputCapture(this);
            return true;
        }

        return false;
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        if (Session?.CapturedElement != this)
            return false;

        if (_isDraggingScrollbar)
        {
            DragScrollbar(input.Position.Y);
            return true;
        }

        RichTextPosition position = PositionFromPoint(input.Position);
        Selection = new RichTextRange(Selection.Anchor, position);
        EnsureCaretVisible();
        return true;
    }

    private bool HandleWheel(UiInputEvent input)
    {
        if (VerticalScrollPolicy == RichEditScrollPolicy.Never)
            return false;

        double newScroll = ClampScroll(_scrollY - input.WheelDeltaNotches * DefaultLineHeight * 3);
        if (newScroll == _scrollY)
            return false;

        _scrollY = newScroll;
        Invalidate(UiInvalidationKind.Render);
        return true;
    }

    private bool HandleTouch(UiInputEvent input)
    {
        if (input.TouchContactState is not TouchContactState state)
            return false;

        if (state == TouchContactState.Pressed)
        {
            if (_touchContactId is not null)
                return false;

            _touchContactId = input.ContactId;
            _touchStart = input.Position;
            _touchLast = input.Position;
            _isTouchScrolling = false;
            return false;
        }

        if (_touchContactId != input.ContactId)
            return false;

        if (state == TouchContactState.Moved)
        {
            double totalX = input.Position.X - _touchStart.X;
            double totalY = input.Position.Y - _touchStart.Y;
            if (!_isTouchScrolling && Math.Sqrt((totalX * totalX) + (totalY * totalY)) >= TouchScrollThreshold)
                _isTouchScrolling = true;

            if (!_isTouchScrolling)
            {
                _touchLast = input.Position;
                return false;
            }

            SetVerticalScroll(_scrollY + (_touchLast.Y - input.Position.Y));
            _touchLast = input.Position;
            return true;
        }

        if (state is TouchContactState.Released or TouchContactState.Cancelled)
        {
            bool handled = _isTouchScrolling;
            _touchContactId = null;
            _isTouchScrolling = false;
            return handled;
        }

        return false;
    }

    private bool TryBeginScrollbarInteraction(BPoint position)
    {
        if (!HasVerticalScrollbar || !ScrollbarTrackBounds.Contains(position))
            return false;

        BRect thumb = ScrollbarThumbBounds;
        if (thumb.Contains(position))
        {
            _isDraggingScrollbar = true;
            _scrollbarDragOffset = position.Y - thumb.Top;
        }
        else
        {
            double page = ContentHeight * 0.85;
            SetVerticalScroll(_scrollY + (position.Y < thumb.Top ? -page : page));
        }

        return true;
    }

    private void DragScrollbar(double pointerY)
    {
        BRect track = ScrollbarTrackBounds;
        BRect thumb = ScrollbarThumbBounds;
        double travel = track.Height - thumb.Height;
        if (travel <= 0)
            return;

        double normalized = (pointerY - _scrollbarDragOffset - track.Top) / travel;
        SetVerticalScroll(Math.Clamp(normalized, 0, 1) * MaxScroll);
    }

    private void SetVerticalScroll(double value)
    {
        double newScroll = ClampScroll(value);
        if (newScroll == _scrollY)
            return;

        _scrollY = newScroll;
        Invalidate(UiInvalidationKind.Render);
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        bool control = input.KeyModifiers.HasFlag(KeyboardModifierState.Control);
        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);

        if (control && HandleControlChord(input))
            return true;

        if (IsKey(input, BVirtualKey.Enter, "Enter"))
        {
            if (shift)
                RunCommand(RichEditCommand.InsertLineBreak);
            else if (AcceptsReturn)
                RunCommand(RichEditCommand.InsertParagraphBreak);
            else
                Submit();
            return true;
        }
        if (IsKey(input, BVirtualKey.Back, "Backspace"))
        {
            if (DeleteBackward())
                EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, 0x2E, "Delete"))
        {
            if (DeleteForward())
                EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.Left, "Left"))
        {
            MoveFocusTo(control ? WordLeft(Selection.Focus) : Document.PositionLeftOf(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Right, "Right"))
        {
            MoveFocusTo(control ? WordRight(Selection.Focus) : Document.PositionRightOf(Selection.Focus), shift);
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
            MoveFocusTo(control ? Document.Start : VisualLineStart(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.End, "End"))
        {
            MoveFocusTo(control ? Document.End : VisualLineEnd(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
        {
            PageMove(-1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
        {
            PageMove(1, shift);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles the Ctrl-modified editing, clipboard, history, and inline-format
    /// shortcuts. Ctrl with a navigation key (arrows, Home, End) is left to the
    /// navigation handlers. Returns true when the chord was recognized.
    /// </summary>
    private bool HandleControlChord(UiInputEvent input)
    {
        if (IsKey(input, BVirtualKey.A, "A"))
        {
            SelectAllInternal();
            return true;
        }
        if (IsKey(input, BVirtualKey.C, "C"))
        {
            RunCommand(RichEditCommand.Copy);
            return true;
        }
        if (IsKey(input, 0x58, "X"))
        {
            RunCommand(RichEditCommand.Cut);
            return true;
        }
        if (IsKey(input, 0x56, "V"))
        {
            RunCommand(RichEditCommand.Paste);
            return true;
        }
        if (IsKey(input, 0x5A, "Z"))
        {
            RunCommand(RichEditCommand.Undo);
            return true;
        }
        if (IsKey(input, 0x59, "Y"))
        {
            RunCommand(RichEditCommand.Redo);
            return true;
        }
        if (IsKey(input, 0x42, "B"))
        {
            RunCommand(RichEditCommand.Bold);
            return true;
        }
        if (IsKey(input, 0x49, "I"))
        {
            RunCommand(RichEditCommand.Italic);
            return true;
        }
        if (IsKey(input, 0x55, "U"))
        {
            RunCommand(RichEditCommand.Underline);
            return true;
        }

        return false;
    }

    /// <summary>Runs a command through the shared undo model and keeps the caret in view.</summary>
    private bool RunCommand(RichEditCommand command, object? parameter = null)
    {
        bool changed = ExecuteCommand(command, parameter);
        EnsureCaretVisible();
        return changed;
    }

    private static string SanitizeCommittedText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var builder = new System.Text.StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (!char.IsControl(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    // --- Navigation --------------------------------------------------------

    private void SelectAllInternal()
    {
        Selection = new RichTextRange(Document.Start, Document.End);
        EnsureCaretVisible();
    }

    private void MoveFocusTo(RichTextPosition target, bool extend)
    {
        RichTextPosition anchor = extend ? Selection.Anchor : target;
        Selection = new RichTextRange(anchor, target);
        EnsureCaretVisible();
    }

    private void MoveVertical(int direction, bool extend)
    {
        EnsureLayout();
        (VisualLine _, int index) = LineForPosition(Selection.Focus);
        double caretX = CaretX(Selection.Focus);
        int target = index + direction;
        if (target < 0)
        {
            MoveFocusTo(Document.Start, extend);
            return;
        }
        if (target >= _lines.Count)
        {
            MoveFocusTo(Document.End, extend);
            return;
        }

        MoveFocusTo(PositionInLineAtX(_lines[target], caretX), extend);
    }

    private void PageMove(int direction, bool extend)
    {
        EnsureLayout();
        int linesPerPage = Math.Max(1, (int)(ContentHeight / DefaultLineHeight));
        (VisualLine _, int index) = LineForPosition(Selection.Focus);
        double caretX = CaretX(Selection.Focus);
        int target = Math.Clamp(index + (direction * linesPerPage), 0, _lines.Count - 1);
        MoveFocusTo(PositionInLineAtX(_lines[target], caretX), extend);
    }

    private RichTextPosition VisualLineStart(RichTextPosition position)
    {
        EnsureLayout();
        VisualLine line = LineForPosition(position).Line;
        return new RichTextPosition(line.ParagraphIndex, line.Start);
    }

    private RichTextPosition VisualLineEnd(RichTextPosition position)
    {
        EnsureLayout();
        VisualLine line = LineForPosition(position).Line;
        return new RichTextPosition(line.ParagraphIndex, line.End);
    }

    private RichTextPosition WordLeft(RichTextPosition position)
    {
        string text = Document.Paragraphs[position.ParagraphIndex].Text;
        int i = position.Offset;
        if (i <= 0)
            return Document.PositionLeftOf(position);
        i--;
        while (i > 0 && char.IsWhiteSpace(text[i]))
            i--;
        while (i > 0 && !char.IsWhiteSpace(text[i - 1]))
            i--;
        return new RichTextPosition(position.ParagraphIndex, i);
    }

    private RichTextPosition WordRight(RichTextPosition position)
    {
        string text = Document.Paragraphs[position.ParagraphIndex].Text;
        int n = text.Length;
        int i = position.Offset;
        if (i >= n)
            return Document.PositionRightOf(position);
        while (i < n && !char.IsWhiteSpace(text[i]))
            i++;
        while (i < n && char.IsWhiteSpace(text[i]))
            i++;
        return new RichTextPosition(position.ParagraphIndex, i);
    }

    private void SelectWordAt(RichTextPosition position)
    {
        string text = Document.Paragraphs[position.ParagraphIndex].Text;
        int start = Math.Clamp(position.Offset, 0, text.Length);
        int end = start;
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;
        while (end < text.Length && IsWordChar(text[end]))
            end++;
        if (start == end)
        {
            while (start > 0 && char.IsWhiteSpace(text[start - 1]))
                start--;
            while (end < text.Length && char.IsWhiteSpace(text[end]))
                end++;
        }

        Selection = new RichTextRange(
            new RichTextPosition(position.ParagraphIndex, start),
            new RichTextPosition(position.ParagraphIndex, end));
        EnsureCaretVisible();
    }

    private void EnsureCaretVisible()
    {
        EnsureLayout();
        double contentHeight = ContentHeight;
        if (contentHeight <= 0)
            return;

        VisualLine line = LineForPosition(Selection.Focus).Line;
        double newScroll = _scrollY;
        if (line.Top < newScroll)
            newScroll = line.Top;
        else if (line.Top + line.Height > newScroll + contentHeight)
            newScroll = line.Top + line.Height - contentHeight;

        newScroll = ClampScroll(newScroll);
        if (newScroll != _scrollY)
        {
            _scrollY = newScroll;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    // --- Geometry and hit testing ------------------------------------------

    private (VisualLine Line, int Index) LineForPosition(RichTextPosition position)
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            VisualLine line = _lines[i];
            if (line.ParagraphIndex == position.ParagraphIndex && position.Offset <= line.End)
                return (line, i);
        }

        for (int i = _lines.Count - 1; i >= 0; i--)
        {
            if (_lines[i].ParagraphIndex == position.ParagraphIndex)
                return (_lines[i], i);
        }

        return (_lines[^1], _lines.Count - 1);
    }

    private RichTextPosition PositionFromPoint(BPoint point)
    {
        EnsureLayout();
        double localY = point.Y - ContentTop + _scrollY;
        VisualLine line = _lines[0];
        for (int i = 0; i < _lines.Count; i++)
        {
            line = _lines[i];
            if (localY < line.Top + line.Height)
                break;
        }

        return PositionInLineAtX(line, point.X);
    }

    private RichTextPosition PositionInLineAtX(VisualLine line, double x)
    {
        int offset = OffsetAtX(line, x - ContentLeft);
        return new RichTextPosition(line.ParagraphIndex, line.Start + offset);
    }

    private int OffsetAtX(VisualLine line, double localX)
    {
        RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
        double advance = 0;
        int index = line.Start;
        while (index < line.End)
        {
            double charAdvance = CharAdvance(paragraph, index, out int step);
            if (localX < advance + (charAdvance / 2))
                break;
            advance += charAdvance;
            index += step;
        }

        return index - line.Start;
    }

    private double CaretX(RichTextPosition position)
    {
        VisualLine line = LineForPosition(position).Line;
        int end = Math.Clamp(position.Offset, line.Start, line.End);
        return ContentLeft + MeasureAdvance(Document.Paragraphs[line.ParagraphIndex], line.Start, end);
    }

    private BRect CaretRect(RichTextPosition position)
    {
        VisualLine line = LineForPosition(position).Line;
        double x = CaretX(position);
        double y = ContentTop + line.Top - _scrollY;
        return new BRect(x, y + 1, 1, Math.Max(1, line.Height - 2));
    }

    private int FlatIndex(RichTextPosition position)
    {
        RichTextDocument document = Document;
        int flat = 0;
        for (int i = 0; i < position.ParagraphIndex; i++)
            flat += document.Paragraphs[i].Length + 1;
        return flat + position.Offset;
    }

    private RichTextPosition PositionFromFlatIndex(int flatIndex)
    {
        flatIndex = Math.Clamp(flatIndex, 0, Document.PlainText.Length);
        for (int paragraphIndex = 0; paragraphIndex < Document.ParagraphCount; paragraphIndex++)
        {
            RichTextParagraph paragraph = Document.Paragraphs[paragraphIndex];
            if (flatIndex <= paragraph.Length || paragraphIndex == Document.ParagraphCount - 1)
                return new RichTextPosition(paragraphIndex, Math.Min(flatIndex, paragraph.Length));

            flatIndex -= paragraph.Length + 1;
        }

        return Document.End;
    }

    // --- Layout ------------------------------------------------------------

    private double DefaultLineHeight => BTextMeasurer.GetLineHeight(Font);

    private BRect InnerBounds => new(
        Bounds.Left + PaddingX,
        Bounds.Top + PaddingY,
        Math.Max(0, Bounds.Width - (PaddingX * 2)),
        Math.Max(0, Bounds.Height - (PaddingY * 2)));

    private double ContentLeft => Bounds.Left + PaddingX;

    private double ContentTop => Bounds.Top + PaddingY;

    private double ContentWidth => Math.Max(0, Bounds.Width - (PaddingX * 2));

    private double ContentHeight => Math.Max(0, Bounds.Height - (PaddingY * 2));

    public bool HasVerticalScrollbar => VerticalScrollPolicy == RichEditScrollPolicy.Always ||
                                        (VerticalScrollPolicy == RichEditScrollPolicy.Auto && MaxScroll > 0);

    private double MaxScroll => VerticalScrollPolicy == RichEditScrollPolicy.Never
        ? 0
        : Math.Max(0, _contentHeight - ContentHeight);

    private BRect ScrollbarTrackBounds
    {
        get
        {
            double thickness = Math.Clamp(ScrollbarThickness, 0, InnerBounds.Width);
            return new BRect(InnerBounds.Right - thickness, InnerBounds.Top, thickness, InnerBounds.Height);
        }
    }

    private BRect ScrollbarThumbBounds
    {
        get
        {
            BRect track = ScrollbarTrackBounds;
            if (track.Height <= 0)
                return BRect.Empty;

            double thumbHeight = MaxScroll <= 0
                ? track.Height
                : Math.Clamp(track.Height * (ContentHeight / Math.Max(ContentHeight, _contentHeight)),
                    Math.Min(MinimumScrollbarThumbLength, track.Height), track.Height);
            double top = track.Top;
            if (MaxScroll > 0)
                top += (track.Height - thumbHeight) * (_scrollY / MaxScroll);
            return new BRect(track.Left, top, track.Width, thumbHeight);
        }
    }

    private double ClampScroll(double value) => Math.Clamp(value, 0, MaxScroll);

    private string LineText(VisualLine line) =>
        Document.Paragraphs[line.ParagraphIndex].Text.Substring(line.Start, line.End - line.Start);

    private double MeasureAdvance(RichTextParagraph paragraph, int start, int end)
    {
        start = Math.Clamp(start, 0, paragraph.Length);
        end = Math.Clamp(end, start, paragraph.Length);
        if (end <= start)
            return 0;

        double advance = 0;
        int position = 0;
        foreach (StyleRun run in paragraph.Runs)
        {
            int runStart = position;
            int runEnd = position + run.Length;
            position = runEnd;

            int segmentStart = Math.Max(start, runStart);
            int segmentEnd = Math.Min(end, runEnd);
            if (segmentEnd <= segmentStart)
                continue;

            string text = paragraph.Text.Substring(segmentStart, segmentEnd - segmentStart);
            advance += MeasureRunText(text, run.Style);
        }

        return advance;
    }

    private void EnsureLayout()
    {
        double contentWidth = ContentWidth;
        if (_layoutValid &&
            ReferenceEquals(_layoutDocument, Document) &&
            _layoutWidth == contentWidth &&
            Equals(_layoutFont, Font))
        {
            return;
        }

        BuildLayout(contentWidth);
        _layoutValid = true;
        _layoutDocument = Document;
        _layoutWidth = contentWidth;
        _layoutFont = Font;
        _scrollY = ClampScroll(_scrollY);
    }

    private void BuildLayout(double contentWidth)
    {
        _lines.Clear();
        double defaultLineHeight = DefaultLineHeight;
        double y = 0;
        RichTextDocument document = Document;

        for (int paragraphIndex = 0; paragraphIndex < document.ParagraphCount; paragraphIndex++)
        {
            RichTextParagraph paragraph = document.Paragraphs[paragraphIndex];
            string text = paragraph.Text;
            foreach ((int segmentStart, int segmentEnd) in HardSegments(text))
            {
                if (segmentStart == segmentEnd)
                {
                    _lines.Add(new VisualLine(paragraphIndex, segmentStart, segmentEnd, y, defaultLineHeight));
                    y += defaultLineHeight;
                    continue;
                }

                int i = segmentStart;
                while (i < segmentEnd)
                {
                    int end = MeasureWrap(paragraph, i, segmentEnd, contentWidth);
                    double lineHeight = MeasureLineHeight(paragraph, i, end, defaultLineHeight);
                    _lines.Add(new VisualLine(paragraphIndex, i, end, y, lineHeight));
                    y += lineHeight;
                    i = end;
                }
            }
        }

        if (_lines.Count == 0)
        {
            _lines.Add(new VisualLine(0, 0, 0, 0, defaultLineHeight));
            y = defaultLineHeight;
        }

        _contentHeight = y;
        ReleaseImagesNotIn(document);
    }

    /// <summary>
    /// Releases the handles of images the document no longer contains. Layout is
    /// the right moment: it runs when the document actually changed, not once per
    /// frame, and opening a second document would otherwise leave the first
    /// document's pictures uploaded for the life of the control.
    /// </summary>
    private void ReleaseImagesNotIn(RichTextDocument document)
    {
        if (_imageHandles.Count == 0)
            return;

        var live = new HashSet<InlineImage>(ReferenceEqualityComparer.Instance);
        foreach (RichTextParagraph paragraph in document.Paragraphs)
        {
            foreach (StyleRun run in paragraph.Runs)
            {
                if (run.Style.Image is InlineImage image)
                    live.Add(image);
            }
        }

        List<InlineImage>? stale = null;
        foreach (KeyValuePair<InlineImage, BImageHandle> entry in _imageHandles)
        {
            if (!live.Contains(entry.Key))
                (stale ??= []).Add(entry.Key);
        }

        if (stale is null)
            return;

        var imageHost = Session?.Host as IUiImageHost;
        foreach (InlineImage image in stale)
        {
            if (_imageHandles.Remove(image, out BImageHandle handle) && handle.IsValid)
                imageHost?.ReleaseImage(handle);
        }
    }

    private static IEnumerable<(int Start, int End)> HardSegments(string text)
    {
        // U+2028 (LINE SEPARATOR) is a soft break inside a paragraph; each one
        // forces a new visual line and is not itself rendered.
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == (char)0x2028)
            {
                yield return (start, i);
                start = i + 1;
            }
        }

        yield return (start, text.Length);
    }

    private int MeasureWrap(RichTextParagraph paragraph, int start, int segmentEnd, double contentWidth)
    {
        if (contentWidth <= 0)
            return segmentEnd;

        string text = paragraph.Text;
        double width = 0;
        int lastBreak = -1;
        int j = start;
        while (j < segmentEnd)
        {
            double advance = CharAdvance(paragraph, j, out int step);
            if (width + advance > contentWidth && j > start)
                break;

            width += advance;
            if (char.IsWhiteSpace(text[j]))
                lastBreak = j;
            j += step;
        }

        if (j >= segmentEnd)
            return segmentEnd;
        if (lastBreak >= start && lastBreak + 1 > start && lastBreak + 1 <= j)
            return lastBreak + 1;
        return j;
    }

    private double MeasureLineHeight(RichTextParagraph paragraph, int start, int end, double fallback)
    {
        if (start >= end || paragraph.Runs.Count == 0)
            return fallback;

        double height = fallback;
        int position = 0;
        foreach (StyleRun run in paragraph.Runs)
        {
            int runStart = position;
            int runEnd = position + run.Length;
            position = runEnd;
            if (Math.Max(start, runStart) >= Math.Min(end, runEnd))
                continue;

            // A picture makes its line as tall as it needs to be, or the image
            // would be clipped by the surrounding text's line height.
            height = run.Style.Image is InlineImage image
                ? Math.Max(height, ImageDisplaySize(image).Height + (ImageMargin * 2))
                : Math.Max(height, BTextMeasurer.GetLineHeight(RunFont(run.Style)));
        }

        return height;
    }

    private double CharAdvance(RichTextParagraph paragraph, int index, out int step)
    {
        string text = paragraph.Text;
        InlineStyle style = paragraph.StyleAt(index);
        BFontStyle font = RunFont(style);
        if (text[index] == InlineImage.Placeholder && style.Image is InlineImage image)
        {
            step = 1;
            return ImageDisplaySize(image).Width;
        }

        if (index + 1 < text.Length && char.IsHighSurrogate(text[index]) && char.IsLowSurrogate(text[index + 1]))
        {
            step = 2;
            return MeasurePieces(text.Substring(index, 2), style, font);
        }

        step = 1;
        return MeasurePieces(text[index].ToString(), style, font);
    }

    private bool IsDoubleClick(BPoint point)
    {
        if (!_hasClicked || Session is null)
            return false;

        TimeSpan delta = Session.Clock.Now.Elapsed - _lastClickTime.Elapsed;
        bool quick = delta >= TimeSpan.Zero && delta <= TimeSpan.FromMilliseconds(400);
        bool near = Math.Abs(point.X - _lastClickPosition.X) <= 4 && Math.Abs(point.Y - _lastClickPosition.Y) <= 4;
        return quick && near;
    }

    private void UpdateClickState(BPoint point)
    {
        _lastClickTime = Session?.Clock.Now ?? default;
        _lastClickPosition = point;
        _hasClicked = true;
    }

    private static bool IsWordChar(char character) => char.IsLetterOrDigit(character) || character == '_';

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    private readonly record struct VisualLine(int ParagraphIndex, int Start, int End, double Top, double Height);

    /// <summary>
    /// A stretch of a visual line as it is drawn. <see cref="Image"/> is set only
    /// for a picture, and then <see cref="Text"/> is the placeholder character it
    /// occupies rather than anything to draw as glyphs.
    /// </summary>
    private readonly record struct LineSegment(
        string Text,
        InlineStyle Style,
        BFontStyle Font,
        double X,
        double Advance,
        InlineImage? Image = null)
    {
        public static LineSegment ForImage(InlineImage image, InlineStyle style, double x, double width) =>
            new(InlineImage.PlaceholderText, style, BFontStyle.Default, x, width, image);
    }
}
