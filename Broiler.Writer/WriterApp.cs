using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.UI;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.RichEdit;
using Broiler.UI.RichEdit.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window.Standard;

namespace Broiler.Writer;

internal sealed class WriterApp : IDisposable
{
    private readonly WriterUiHost _host;
    private readonly Action _requestClose;
    private readonly UiSession _session;
    private readonly StandardRichEdit _editor;
    private readonly StandardMenu _menu;
    private readonly StandardLabel _title;
    private readonly StandardLabel _status;
    private readonly List<(UiMenuItem Item, RichEditCommand Command)> _richEditMenuItems = [];
    private string _lastAction = "Ready";

    public WriterApp(WriterUiHost host, Action requestClose)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _requestClose = requestClose ?? throw new ArgumentNullException(nameof(requestClose));
        _session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(_host);

        _editor = new StandardRichEdit
        {
            PreferredSize = new BSize(760, 520),
            PlaceholderText = "Start writing in Broiler Writer...",
            Font = new BFontStyle("Segoe UI", 17),
            Background = WriterPalette.Page,
            BorderColor = WriterPalette.EditorBorder,
            FocusRing = WriterPalette.Accent,
            PaddingX = 18,
            PaddingY = 16,
        };

        _menu = CreateMenu();
        _title = new StandardLabel
        {
            Text = "Untitled document",
            Font = new BFontStyle("Segoe UI", 20, BFontWeight.SemiBold),
            Foreground = WriterPalette.Title,
        };
        _status = new StandardLabel
        {
            Text = "Ready",
            Font = new BFontStyle("Segoe UI", 13),
            Foreground = WriterPalette.Muted,
            Trimming = UiTextTrimming.CharacterEllipsis,
        };

        var root = new StandardWindow
        {
            Title = "Broiler Writer",
            Background = WriterPalette.Canvas,
            BorderColor = WriterPalette.WindowBorder,
            ActiveBorderColor = WriterPalette.Accent,
            BorderThickness = 1,
        };
        root.AddChild(new WriterContent(_menu, _title, _editor, _status));

        SeedDocument();
        _session.AddRoot(root);
        _session.SetFocus(_editor);

        _editor.SelectionChanged += (_, _) => RefreshUi();
        _editor.DocumentChanged += (_, _) => RefreshUi();
        _editor.CommandExecuted += (_, e) =>
        {
            if (e.Command != RichEditCommand.InsertText)
                _lastAction = FriendlyCommandName(e.Command);
            RefreshUi();
        };
        _menu.ItemInvoked += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Item.CommandName))
                RefreshUi();
        };

        RefreshUi();
    }

    public UiSession Session => _session;

    public BRenderList RenderFrame() => _session.RenderFrame();

    public void Dispatch(UiInputEvent input)
    {
        if (_session.DispatchInput(input))
            _host.RequestInvalidate();
    }

    public void Invalidate() => _host.RequestInvalidate();

    public void Dispose() => _session.Dispose();

    private StandardMenu CreateMenu()
    {
        var dispatcher = new StandardCommandDispatcher();
        dispatcher.Add(new StandardCommand("file.new", NewDocument));
        dispatcher.Add(new StandardCommand("file.exit", _requestClose));
        AddRichEditCommand(dispatcher, "edit.undo", RichEditCommand.Undo);
        AddRichEditCommand(dispatcher, "edit.redo", RichEditCommand.Redo);
        AddRichEditCommand(dispatcher, "edit.cut", RichEditCommand.Cut);
        AddRichEditCommand(dispatcher, "edit.copy", RichEditCommand.Copy);
        AddRichEditCommand(dispatcher, "edit.paste", RichEditCommand.Paste);
        AddRichEditCommand(dispatcher, "edit.select-all", RichEditCommand.SelectAll);
        AddRichEditCommand(dispatcher, "format.bold", RichEditCommand.Bold);
        AddRichEditCommand(dispatcher, "format.italic", RichEditCommand.Italic);
        AddRichEditCommand(dispatcher, "format.underline", RichEditCommand.Underline);
        AddRichEditCommand(dispatcher, "format.strike", RichEditCommand.Strikethrough);
        AddRichEditCommand(dispatcher, "format.clear", RichEditCommand.ClearFormatting);
        AddRichEditCommand(dispatcher, "paragraph.left", RichEditCommand.AlignLeft);
        AddRichEditCommand(dispatcher, "paragraph.center", RichEditCommand.AlignCenter);
        AddRichEditCommand(dispatcher, "paragraph.right", RichEditCommand.AlignRight);
        AddRichEditCommand(dispatcher, "paragraph.bullets", RichEditCommand.BulletList);
        AddRichEditCommand(dispatcher, "paragraph.numbered", RichEditCommand.NumberedList);
        AddRichEditCommand(dispatcher, "paragraph.indent", RichEditCommand.Indent);
        AddRichEditCommand(dispatcher, "paragraph.outdent", RichEditCommand.Outdent);

        var file = new UiMenuItem("file", "File") { AccessKey = 'F' };
        file.Children.Add(new UiMenuItem("new", "New document") { CommandName = "file.new", AccessKey = 'N' });
        file.Children.Add(new UiMenuItem("exit", "Exit") { CommandName = "file.exit", AccessKey = 'X' });

        var edit = new UiMenuItem("edit", "Edit") { AccessKey = 'E' };
        edit.Children.Add(RichEditItem("undo", "Undo", "edit.undo", RichEditCommand.Undo, 'U'));
        edit.Children.Add(RichEditItem("redo", "Redo", "edit.redo", RichEditCommand.Redo, 'R'));
        edit.Children.Add(RichEditItem("cut", "Cut", "edit.cut", RichEditCommand.Cut, 'T'));
        edit.Children.Add(RichEditItem("copy", "Copy", "edit.copy", RichEditCommand.Copy, 'C'));
        edit.Children.Add(RichEditItem("paste", "Paste", "edit.paste", RichEditCommand.Paste, 'P'));
        edit.Children.Add(RichEditItem("select-all", "Select all", "edit.select-all", RichEditCommand.SelectAll, 'A'));

        var format = new UiMenuItem("format", "Format") { AccessKey = 'O' };
        format.Children.Add(RichEditItem("bold", "Bold", "format.bold", RichEditCommand.Bold, 'B', checkable: true));
        format.Children.Add(RichEditItem("italic", "Italic", "format.italic", RichEditCommand.Italic, 'I', checkable: true));
        format.Children.Add(RichEditItem("underline", "Underline", "format.underline", RichEditCommand.Underline, 'U', checkable: true));
        format.Children.Add(RichEditItem("strike", "Strikethrough", "format.strike", RichEditCommand.Strikethrough, 'S', checkable: true));
        format.Children.Add(RichEditItem("clear", "Clear formatting", "format.clear", RichEditCommand.ClearFormatting, 'C'));

        var paragraph = new UiMenuItem("paragraph", "Paragraph") { AccessKey = 'P' };
        paragraph.Children.Add(RichEditItem("left", "Align left", "paragraph.left", RichEditCommand.AlignLeft, 'L', checkable: true));
        paragraph.Children.Add(RichEditItem("center", "Align center", "paragraph.center", RichEditCommand.AlignCenter, 'C', checkable: true));
        paragraph.Children.Add(RichEditItem("right", "Align right", "paragraph.right", RichEditCommand.AlignRight, 'R', checkable: true));
        paragraph.Children.Add(RichEditItem("bullets", "Bullets", "paragraph.bullets", RichEditCommand.BulletList, 'B', checkable: true));
        paragraph.Children.Add(RichEditItem("numbered", "Numbered", "paragraph.numbered", RichEditCommand.NumberedList, 'N', checkable: true));
        paragraph.Children.Add(RichEditItem("indent", "Indent", "paragraph.indent", RichEditCommand.Indent, 'I'));
        paragraph.Children.Add(RichEditItem("outdent", "Outdent", "paragraph.outdent", RichEditCommand.Outdent, 'O'));
        format.Children.Add(paragraph);

        var help = new UiMenuItem("help", "Help") { AccessKey = 'H' };
        help.Children.Add(new UiMenuItem("about", "About Broiler Writer") { CommandName = "help.about", AccessKey = 'A' });
        dispatcher.Add(new StandardCommand("help.about", ShowAbout));

        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.MenuBar,
            PreferredSize = new BSize(360, 30),
            MenuBarHeight = 30,
            ItemHeight = 28,
            PopupWidth = 210,
            Font = new BFontStyle("Segoe UI", 14),
            Background = WriterPalette.MenuSurface,
            PopupBackground = WriterPalette.MenuPopup,
            Foreground = WriterPalette.Title,
            BorderColor = WriterPalette.EditorBorder,
            SelectedBackground = WriterPalette.MenuSelected,
            CommandDispatcher = dispatcher,
        };
        menu.SetItems([file, edit, format, help]);
        return menu;
    }

    private void AddRichEditCommand(StandardCommandDispatcher dispatcher, string name, RichEditCommand command) =>
        dispatcher.Add(new StandardCommand(name, () => RunRichEditCommand(command), () => _editor.GetCommandState(command).IsEnabled));

    private UiMenuItem RichEditItem(string id, string text, string commandName, RichEditCommand command, char accessKey, bool checkable = false)
    {
        var item = new UiMenuItem(id, text)
        {
            CommandName = commandName,
            AccessKey = accessKey,
            IsCheckable = checkable,
        };
        _richEditMenuItems.Add((item, command));
        return item;
    }

    private void RunRichEditCommand(RichEditCommand command)
    {
        bool ran = _editor.ExecuteCommand(command);
        _lastAction = ran ? FriendlyCommandName(command) : FriendlyCommandName(command) + " unavailable";
        _session.SetFocus(_editor);
        RefreshUi();
    }

    private void NewDocument()
    {
        _editor.SetPlainText(string.Empty);
        _title.Text = "Untitled document";
        _lastAction = "New document";
        _session.SetFocus(_editor);
        RefreshUi();
    }

    private void ShowAbout()
    {
        _lastAction = "Broiler Writer preview: Broiler.UI window, menu, and StandardRichEdit";
        _session.SetFocus(_editor);
        RefreshUi();
    }

    private void SeedDocument()
    {
        _editor.SetPlainText(
            "Broiler Writer\n" +
            "This preview is a Broiler.UI window with a Broiler-rendered menu and StandardRichEdit document surface.\n" +
            "Use the Edit and Format menus, or keyboard shortcuts such as Ctrl+B, Ctrl+I, Ctrl+U, Ctrl+Z, and Ctrl+Y. The editor is drawn through Broiler.Graphics rather than a native RICHEDIT control.");

        RichTextPosition start = _editor.Document.Start;
        RichTextPosition end = _editor.Document.ParagraphEnd(start);
        _editor.Selection = new RichTextRange(start, end);
        _editor.ExecuteCommand(RichEditCommand.Bold);
        _editor.Selection = RichTextRange.Caret(_editor.Document.End);
        _lastAction = "Ready";
    }

    private void RefreshUi()
    {
        foreach ((UiMenuItem item, RichEditCommand command) in _richEditMenuItems)
        {
            RichEditCommandState state = _editor.GetCommandState(command);
            item.IsEnabled = state.IsEnabled;
            if (item.IsCheckable)
                item.IsChecked = state.IsToggled;
        }

        _status.Text = BuildStatus();
        _host.RequestInvalidate();
    }

    private string BuildStatus()
    {
        int paragraphs = _editor.Document.ParagraphCount;
        int chars = _editor.GetPlainText().Length;
        string selection = _editor.Selection.IsEmpty ? "No selection" : "Selection active";
        string style = CurrentStyleText();
        string paragraphText = paragraphs.ToString(CultureInfo.InvariantCulture) + (paragraphs == 1 ? " paragraph" : " paragraphs");
        string charText = chars.ToString(CultureInfo.InvariantCulture) + (chars == 1 ? " character" : " characters");
        return paragraphText + " | " + charText + " | " + selection + " | " + style + " | " + _lastAction;
    }

    private string CurrentStyleText()
    {
        InlineStyle style = _editor.CaretInlineStyle;
        var names = new List<string>();
        if (style.Bold) names.Add("bold");
        if (style.Italic) names.Add("italic");
        if (style.Underline) names.Add("underline");
        if (style.Strikethrough) names.Add("strike");
        return names.Count == 0 ? "plain" : string.Join(" + ", names);
    }

    private static string FriendlyCommandName(RichEditCommand command) =>
        command switch
        {
            RichEditCommand.SelectAll => "Select all",
            RichEditCommand.ClearFormatting => "Clear formatting",
            RichEditCommand.AlignLeft => "Align left",
            RichEditCommand.AlignCenter => "Align center",
            RichEditCommand.AlignRight => "Align right",
            RichEditCommand.BulletList => "Bullet list",
            RichEditCommand.NumberedList => "Numbered list",
            _ => command.ToString(),
        };

    private sealed class WriterContent : UiElement
    {
        private const double Margin = 24;
        private const double TitleTop = 18;
        private const double StatusHeight = 24;
        private const double MinWidth = 900;
        private const double MinHeight = 620;

        private readonly StandardMenu _menu;
        private readonly StandardLabel _title;
        private readonly StandardRichEdit _editor;
        private readonly StandardLabel _status;

        public WriterContent(StandardMenu menu, StandardLabel title, StandardRichEdit editor, StandardLabel status)
        {
            _menu = menu;
            _title = title;
            _editor = editor;
            _status = status;

            AddChild(_menu);
            AddChild(_title);
            AddChild(_editor);
            AddChild(_status);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? MinWidth : Math.Max(MinWidth, availableSize.Width);
            double height = double.IsInfinity(availableSize.Height) ? MinHeight : Math.Max(MinHeight, availableSize.Height);
            double contentWidth = Math.Max(0, width - (Margin * 2));

            _menu.Measure(new BSize(width, _menu.MenuBarHeight));
            _title.Measure(new BSize(contentWidth, double.PositiveInfinity));
            _editor.Measure(new BSize(contentWidth, Math.Max(240, height - 140)));
            _status.Measure(new BSize(contentWidth, StatusHeight));

            return new BSize(width, height);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            _menu.Arrange(new BRect(finalRect.Left, finalRect.Top, finalRect.Width, _menu.MenuBarHeight));

            double x = finalRect.Left + Margin;
            double y = finalRect.Top + _menu.MenuBarHeight + TitleTop;
            double width = Math.Max(0, finalRect.Width - (Margin * 2));

            _title.Arrange(new BRect(x, y, width, _title.DesiredSize.Height));
            y += _title.DesiredSize.Height + 14;

            double statusTop = finalRect.Bottom - Margin - StatusHeight;
            double editorHeight = Math.Max(220, statusTop - y - 14);
            _editor.Arrange(new BRect(x, y, width, editorHeight));
            _status.Arrange(new BRect(x, statusTop, width, StatusHeight));
        }

        protected override void RenderCore(UiRenderContext context)
        {
            context.RenderList.FillRect(Bounds, WriterPalette.Canvas);
            context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top, Bounds.Width, _menu.MenuBarHeight), WriterPalette.MenuSurface);
            context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top + _menu.MenuBarHeight, Bounds.Width, 1), WriterPalette.MenuRule);
            base.RenderCore(context);
        }
    }
}
