namespace Broiler.UI;

public enum UiSemanticRole
{
    Generic = 0,
    Window,
    Panel,
    Label,
    Button,
    Edit,
    CheckBox,
    RadioButton,
    ToggleButton,
    Slider,
    ProgressBar,
    ImageView,
    ScrollView,
    ListView,
    ComboBox,
    TabView,
    Menu,
    Tooltip,
    Dialog,
    Toolbar,
    RichEdit,
    FormatCodeView,
    Splitter,

    /// <summary>
    /// A source editor. Unlike <see cref="Edit"/> and <see cref="RichEdit"/>,
    /// a node with this role does not carry its text in
    /// <see cref="UiSemanticTextInfo.Value"/> — the document can be tens of
    /// megabytes. It exposes <see cref="IUiVirtualizedTextProvider"/> instead,
    /// and every read is bounded. See Broiler.UI ADR 0022.
    /// </summary>
    CodeEditor,
}
