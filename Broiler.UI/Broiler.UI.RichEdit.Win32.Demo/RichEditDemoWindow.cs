using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Versioning;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.Input.Keyboard;
using Broiler.UI.Button.Standard;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel;
using Broiler.UI.Panel.Standard;
using Broiler.UI.RichEdit;
using Broiler.UI.RichEdit.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.RichEdit.Win32.Demo;

/// <summary>
/// A standalone Win32 + Direct2D demo for the <see cref="StandardRichEdit"/>
/// control. The whole surface — caret, selection, per-run styling, and the
/// formatting toolbar — is painted through Broiler.Graphics and driven through
/// the <see cref="UiRichEdit"/> command surface; no native EDIT/RICHEDIT control
/// or OS text API is involved.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class RichEditDemoWindow : Direct2DWindow
{
    private readonly DemoHost _host;
    private readonly UiSession _session;
    private readonly StandardRichEdit _edit;
    private readonly StandardLabel _status;
    private readonly List<(StandardButton Button, RichEditCommand Command)> _toggleButtons = [];
    private readonly List<(StandardButton Button, RichEditCommand Command)> _actionButtons = [];
    private RichEditCommand _lastCommand = RichEditCommand.None;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new("broiler-ui-richedit-win32-demo");
#pragma warning restore CS0618

    public RichEditDemoWindow()
        : base(new BWindowOptions
        {
            Title = "Broiler.UI RichEdit Win32 Demo",
            ClientWidth = 1040,
            ClientHeight = 720,
            ClearColor = Palette.Canvas,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
        })
    {
        _host = new DemoHost(this);
        _session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(_host);

        _edit = new StandardRichEdit
        {
            PreferredSize = new BSize(720, 360),
            PlaceholderText = "Start typing. Select text and press Ctrl+B / Ctrl+I / Ctrl+U, or use the toolbar.",
            Font = new BFontStyle("Segoe UI", 16),
        };

        _status = new StandardLabel
        {
            Text = "Ready.",
            Font = new BFontStyle("Segoe UI", 13),
            Foreground = Palette.StatusText,
            Wrapping = UiTextWrapping.Wrap,
        };

        StandardWindow window = BuildTree();
        SeedDocument();

        _session.AddRoot(window);
        _session.SetFocus(_edit);

        _edit.SelectionChanged += (_, _) => RefreshUi();
        _edit.DocumentChanged += (_, _) => RefreshUi();
        _edit.CommandExecuted += (_, e) =>
        {
            if (e.Command != RichEditCommand.InsertText)
                _lastCommand = e.Command;
            RefreshUi();
        };

        RefreshUi();
    }

    protected override BRenderList? BuildRenderList(BSize clientSize)
    {
        _host.Update(clientSize, DpiScale);
        return _session.RenderFrame();
    }

    protected override void OnResized(BSize clientSize, double dpiScale)
    {
        _host.Update(clientSize, dpiScale);
        Invalidate();
    }

    protected override void OnPointerDown(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnPointerMove(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerMove(e));

    protected override void OnPointerUp(BPointerEventArgs e) =>
        Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnMouseWheel(BMouseWheelEventArgs e) =>
        Dispatch(_legacyInput.FromMouseWheel(e));

    protected override void OnKeyDown(BKeyEventArgs e) =>
        Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Down));

    protected override void OnKeyUp(BKeyEventArgs e) =>
        Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Up));

    protected override void OnTextInput(BTextInputEventArgs e) =>
        Dispatch(_legacyInput.FromText(e));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _session.Dispose();

        base.Dispose(disposing);
    }

    private StandardWindow BuildTree()
    {
        var title = new StandardLabel
        {
            Text = "Broiler.UI RichEdit",
            Font = new BFontStyle("Segoe UI", 26, BFontWeight.Bold),
            Foreground = Palette.Title,
        };

        var subtitle = new StandardLabel
        {
            Text = "A standalone Win32 demo of the Broiler-drawn StandardRichEdit control, driven entirely through the UiRichEdit command surface.",
            Font = new BFontStyle("Segoe UI", 14),
            Foreground = Palette.Muted,
            Wrapping = UiTextWrapping.Wrap,
        };

        StandardPanel toolbar = BuildToolbar();

        var caption = new StandardLabel
        {
            Text = "Bold, italic, underline, and strikethrough render live. Alignment, lists, and indent update command state now; their visual layout arrives in a later phase.",
            Font = new BFontStyle("Segoe UI", 12),
            Foreground = Palette.Muted,
            Wrapping = UiTextWrapping.Wrap,
        };

        var content = new DemoContent(title, subtitle, toolbar, caption, _edit, _status);

        var window = new StandardWindow
        {
            Title = "Broiler.UI RichEdit Win32 Demo",
            Background = Palette.Canvas,
            BorderColor = Palette.Divider,
            BorderThickness = 1,
        };
        window.AddChild(content);
        return window;
    }

    private StandardPanel BuildToolbar()
    {
        StandardPanel history = Row(
            Action("Undo", RichEditCommand.Undo, 64),
            Action("Redo", RichEditCommand.Redo, 64),
            Action("Cut", RichEditCommand.Cut, 60),
            Action("Copy", RichEditCommand.Copy, 60),
            Action("Paste", RichEditCommand.Paste, 62),
            Action("Select all", RichEditCommand.SelectAll, 92));

        StandardPanel inline = Row(
            Toggle("Bold", RichEditCommand.Bold, 72),
            Toggle("Italic", RichEditCommand.Italic, 72),
            Toggle("Underline", RichEditCommand.Underline, 96),
            Toggle("Strike", RichEditCommand.Strikethrough, 76),
            Action("Clear format", RichEditCommand.ClearFormatting, 116));

        StandardPanel paragraph = Row(
            Toggle("Left", RichEditCommand.AlignLeft, 68),
            Toggle("Center", RichEditCommand.AlignCenter, 74),
            Toggle("Right", RichEditCommand.AlignRight, 68),
            Toggle("Bullets", RichEditCommand.BulletList, 82),
            Toggle("Numbered", RichEditCommand.NumberedList, 96),
            Action("Indent", RichEditCommand.Indent, 76),
            Action("Outdent", RichEditCommand.Outdent, 82));

        var toolbar = new StandardPanel
        {
            LayoutMode = UiPanelLayoutMode.Stack,
            StackOrientation = UiStackOrientation.Vertical,
            Spacing = 8,
            Background = BColor.Transparent,
        };
        toolbar.AddChild(history);
        toolbar.AddChild(inline);
        toolbar.AddChild(paragraph);
        return toolbar;
    }

    private static StandardPanel Row(params StandardButton[] buttons)
    {
        var row = new StandardPanel
        {
            LayoutMode = UiPanelLayoutMode.Stack,
            StackOrientation = UiStackOrientation.Horizontal,
            Spacing = 6,
            Background = BColor.Transparent,
        };
        foreach (StandardButton button in buttons)
            row.AddChild(button);
        return row;
    }

    private StandardButton Action(string text, RichEditCommand command, double width)
    {
        StandardButton button = MakeButton(text, command, width);
        _actionButtons.Add((button, command));
        return button;
    }

    private StandardButton Toggle(string text, RichEditCommand command, double width)
    {
        StandardButton button = MakeButton(text, command, width);
        _toggleButtons.Add((button, command));
        return button;
    }

    private StandardButton MakeButton(string text, RichEditCommand command, double width)
    {
        var button = new StandardButton
        {
            Text = text,
            PreferredSize = new BSize(width, 32),
            Font = new BFontStyle("Segoe UI", 13),
            Background = Palette.ButtonSurface,
            Foreground = Palette.ButtonText,
            BorderColor = Palette.ButtonBorder,
            SecondaryHoverBackground = Palette.ButtonHover,
        };
        button.Clicked += (_, _) => RunToolbarCommand(command);
        return button;
    }

    private void RunToolbarCommand(RichEditCommand command)
    {
        _edit.ExecuteCommand(command);
        _session.SetFocus(_edit); // keep typing focus on the editor after a toolbar click
        RefreshUi();
    }

    private void RefreshUi()
    {
        foreach ((StandardButton button, RichEditCommand command) in _toggleButtons)
        {
            RichEditCommandState state = _edit.GetCommandState(command);
            button.IsEnabled = state.IsEnabled;
            button.Background = state.IsToggled ? Palette.ButtonActive : Palette.ButtonSurface;
        }

        foreach ((StandardButton button, RichEditCommand command) in _actionButtons)
            button.IsEnabled = _edit.GetCommandState(command).IsEnabled;

        _status.Text = BuildStatus();
        Invalidate();
    }

    private string BuildStatus()
    {
        RichTextRange selection = _edit.Selection;
        InlineStyle style = _edit.CaretInlineStyle;

        var styles = new List<string>();
        if (style.Bold) styles.Add("bold");
        if (style.Italic) styles.Add("italic");
        if (style.Underline) styles.Add("underline");
        if (style.Strikethrough) styles.Add("strike");
        string styleText = styles.Count == 0 ? "plain" : string.Join(" + ", styles);

        // Positions are opaque (ADR 0014); paragraph/list/alignment state is read
        // from the public command surface rather than from caret coordinates.
        string align = IsToggled(RichEditCommand.AlignCenter) ? "center"
            : IsToggled(RichEditCommand.AlignRight) ? "right"
            : "left";
        string list = IsToggled(RichEditCommand.BulletList) ? "bullet list"
            : IsToggled(RichEditCommand.NumberedList) ? "numbered list"
            : "no list";

        int paragraphs = _edit.Document.ParagraphCount;
        int chars = _edit.GetPlainText().Length;
        string document = paragraphs.ToString(CultureInfo.InvariantCulture) + " paragraph" + (paragraphs == 1 ? "" : "s") +
                          ", " + chars.ToString(CultureInfo.InvariantCulture) + " chars";
        string selectionText = selection.IsEmpty ? "no selection" : "text selected";
        string last = _lastCommand == RichEditCommand.None
            ? string.Empty
            : "   |   last: " + _lastCommand;

        return document + "   |   " + selectionText + "   |   style: " + styleText +
               "   |   align: " + align + "   |   " + list + last;
    }

    private bool IsToggled(RichEditCommand command) => _edit.GetCommandState(command).IsToggled;

    private void SeedDocument()
    {
        _edit.SetPlainText(
            "Broiler.UI RichEdit\n" +
            "This is the standard, Broiler-drawn rich text editor. Layout, caret, selection, and per-run styling are all painted through Broiler.Graphics.\n" +
            "Select text and press Ctrl+B, Ctrl+I, or Ctrl+U, or use the toolbar above. Ctrl+Z and Ctrl+Y undo and redo. Enter starts a new paragraph; Shift+Enter inserts a soft line break.\n" +
            "Clipboard (Ctrl+X/C/V), word navigation (Ctrl+Left/Right), double-click word selection, and mouse-drag selection all work.");

        // Seed a little styling through the public command surface so per-run
        // rendering is visible the moment the window opens. Positions are opaque
        // (ADR 0014), so every range is built from the document's navigation API.
        BoldFirstParagraph();
        ItalicizePhrase(1, "Broiler.Graphics");

        _edit.Selection = RichTextRange.Caret(_edit.Document.End);
        _lastCommand = RichEditCommand.None;
    }

    private void BoldFirstParagraph()
    {
        RichTextPosition start = _edit.Document.Start;
        RichTextPosition end = _edit.Document.ParagraphEnd(start);
        SelectAndRun(start, end, RichEditCommand.Bold);
    }

    private void ItalicizePhrase(int paragraphIndex, string phrase)
    {
        RichTextDocument document = _edit.Document;
        if (paragraphIndex < 0 || paragraphIndex >= document.ParagraphCount)
            return;

        int column = document.Paragraphs[paragraphIndex].Text.IndexOf(phrase, StringComparison.Ordinal);
        if (column < 0)
            return;

        RichTextPosition paragraphStart = ParagraphStartAt(paragraphIndex);
        RichTextPosition start = AdvanceRight(paragraphStart, column);
        RichTextPosition end = AdvanceRight(start, phrase.Length);
        SelectAndRun(start, end, RichEditCommand.Italic);
    }

    private void SelectAndRun(RichTextPosition start, RichTextPosition end, RichEditCommand command)
    {
        if (start == end)
            return;

        _edit.Selection = new RichTextRange(start, end);
        _edit.ExecuteCommand(command);
    }

    /// <summary>
    /// Walks the document's public navigation API to the start of paragraph
    /// <paramref name="index"/>. Positions are opaque (ADR 0014), so they cannot be
    /// constructed from a (paragraph, offset) pair directly.
    /// </summary>
    private RichTextPosition ParagraphStartAt(int index)
    {
        RichTextDocument document = _edit.Document;
        RichTextPosition position = document.Start;
        for (int i = 0; i < index; i++)
        {
            RichTextPosition paragraphEnd = document.ParagraphEnd(position);
            RichTextPosition next = document.PositionRightOf(paragraphEnd);
            if (next == paragraphEnd)
                break;
            position = next;
        }

        return document.ParagraphStart(position);
    }

    private RichTextPosition AdvanceRight(RichTextPosition position, int count)
    {
        RichTextDocument document = _edit.Document;
        for (int i = 0; i < count; i++)
        {
            RichTextPosition next = document.PositionRightOf(position);
            if (next == position)
                break;
            position = next;
        }

        return position;
    }

    private void Dispatch(UiInputEvent input)
    {
        if (_session.DispatchInput(input))
            Invalidate();
    }

    /// <summary>
    /// Lays the title, subtitle, formatting toolbar, editor, and status bar out
    /// vertically. The editor stretches to fill the space between the toolbar and
    /// the pinned status line, so it grows and shrinks with the native window.
    /// </summary>
    private sealed class DemoContent : UiElement
    {
        private const double Margin = 26;
        private const double Gap = 14;
        private const double MinWidth = 900;
        private const double MinHeight = 560;

        private readonly StandardLabel _title;
        private readonly StandardLabel _subtitle;
        private readonly StandardPanel _toolbar;
        private readonly StandardLabel _caption;
        private readonly StandardRichEdit _edit;
        private readonly StandardLabel _status;
        private double _dividerY;

        public DemoContent(
            StandardLabel title,
            StandardLabel subtitle,
            StandardPanel toolbar,
            StandardLabel caption,
            StandardRichEdit edit,
            StandardLabel status)
        {
            _title = title;
            _subtitle = subtitle;
            _toolbar = toolbar;
            _caption = caption;
            _edit = edit;
            _status = status;

            AddChild(_title);
            AddChild(_subtitle);
            AddChild(_toolbar);
            AddChild(_caption);
            AddChild(_edit);
            AddChild(_status);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? MinWidth : Math.Max(MinWidth, availableSize.Width);
            double height = double.IsInfinity(availableSize.Height) ? MinHeight : Math.Max(MinHeight, availableSize.Height);
            var childSize = new BSize(Math.Max(0, width - (Margin * 2)), height);
            foreach (UiElement child in Children)
                child.Measure(childSize);

            return new BSize(width, height);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            double x = finalRect.Left + Margin;
            double w = Math.Max(0, finalRect.Width - (Margin * 2));
            double y = finalRect.Top + Margin;

            _title.Arrange(new BRect(x, y, w, _title.DesiredSize.Height));
            y += _title.DesiredSize.Height + 6;

            _subtitle.Arrange(new BRect(x, y, w, _subtitle.DesiredSize.Height));
            y += _subtitle.DesiredSize.Height + Gap;

            double toolbarHeight = _toolbar.DesiredSize.Height;
            _toolbar.Arrange(new BRect(x, y, w, toolbarHeight));
            y += toolbarHeight + 10;

            _dividerY = y;
            y += Gap;

            double statusHeight = Math.Max(22, _status.DesiredSize.Height);
            double captionHeight = Math.Max(18, _caption.DesiredSize.Height);
            double statusTop = finalRect.Bottom - Margin - statusHeight;
            double captionTop = statusTop - captionHeight - 8;
            double editHeight = Math.Max(160, captionTop - Gap - y);

            _edit.Arrange(new BRect(x, y, w, editHeight));
            _caption.Arrange(new BRect(x, captionTop, w, captionHeight));
            _status.Arrange(new BRect(x, statusTop, w, statusHeight));
        }

        protected override void RenderCore(UiRenderContext context)
        {
            context.RenderList.FillRect(Bounds, Palette.Canvas);
            context.RenderList.FillRect(new BRect(Bounds.Left + Margin, _dividerY, Math.Max(0, Bounds.Width - (Margin * 2)), 1), Palette.Divider);
            base.RenderCore(context);
        }
    }

    private sealed class DemoHost : IUiHost, IUiClipboardHost, IUiTextInputHost
    {
        private readonly RichEditDemoWindow _window;
        private string _clipboard = string.Empty;
        private UiTextCaretInfo? _caret;

        public DemoHost(RichEditDemoWindow window)
        {
            _window = window;
        }

        public BSize ViewportSize { get; private set; } = new(1040, 720);

        public double Scale { get; private set; } = 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation) => _window.Invalidate();

        public void Present(BRenderList renderList)
        {
        }

        public bool TryGetText(out string text)
        {
            text = _clipboard;
            return true;
        }

        public void SetText(string text) => _clipboard = text ?? string.Empty;

        public void PublishCaret(UiTextCaretInfo caret) => _caret = caret;

        public void ClearCaret(UiElement owner)
        {
            if (_caret?.Owner == owner)
                _caret = null;
        }

        public void Update(BSize viewportSize, double scale)
        {
            ViewportSize = viewportSize;
            Scale = scale;
        }
    }

    private static class Palette
    {
        public static readonly BColor Canvas = BColor.FromArgb(0xFF, 0xF6, 0xF8, 0xFB);
        public static readonly BColor Title = BColor.FromArgb(0xFF, 0x14, 0x2A, 0x40);
        public static readonly BColor Muted = BColor.FromArgb(0xFF, 0x5B, 0x6B, 0x82);
        public static readonly BColor Divider = BColor.FromArgb(0xFF, 0xDD, 0xE4, 0xEE);
        public static readonly BColor ButtonSurface = BColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly BColor ButtonHover = BColor.FromArgb(0xFF, 0xEC, 0xF3, 0xFF);
        public static readonly BColor ButtonActive = BColor.FromArgb(0xFF, 0xCF, 0xE4, 0xFF);
        public static readonly BColor ButtonText = BColor.FromArgb(0xFF, 0x27, 0x37, 0x4B);
        public static readonly BColor ButtonBorder = BColor.FromArgb(0xFF, 0xCB, 0xD5, 0xE2);
        public static readonly BColor StatusText = BColor.FromArgb(0xFF, 0x33, 0x42, 0x55);
    }
}
