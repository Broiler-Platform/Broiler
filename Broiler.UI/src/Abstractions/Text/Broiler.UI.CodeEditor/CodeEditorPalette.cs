using System;
using Broiler.Graphics;

namespace Broiler.UI.CodeEditor;

/// <summary>
/// How a classification kind and a diagnostic severity are drawn.
///
/// The palette is a record with defaults for every member, so a host that sets
/// nothing still renders, and adding a member later does not break an existing
/// initializer. That matters more here than elsewhere: the Standard theme token
/// set makes every role <c>required</c>, so a palette expressed as new required
/// tokens would fail to compile every theme preset in the repository.
/// </summary>
public sealed record CodeEditorPalette
{
    public static CodeEditorPalette Default { get; } = new();

    // Surface and chrome.
    public BColor Background { get; init; } = new BColor(0xFF, 0xFF, 0xFF);

    public BColor Foreground { get; init; } = new BColor(0x1F, 0x23, 0x28);

    public BColor GutterBackground { get; init; } = new BColor(0xF6, 0xF7, 0xF9);

    public BColor LineNumber { get; init; } = new BColor(0x8A, 0x91, 0x9B);

    public BColor CurrentLineNumber { get; init; } = new BColor(0x1F, 0x23, 0x28);

    public BColor CurrentLineBackground { get; init; } = new BColor(0xF0, 0xF3, 0xF7);

    public BColor Selection { get; init; } = new BColor(0xB3, 0xD4, 0xFC);

    public BColor InactiveSelection { get; init; } = new BColor(0xDC, 0xE3, 0xEB);

    public BColor Caret { get; init; } = new BColor(0x1F, 0x23, 0x28);

    public BColor BracketMatch { get; init; } = new BColor(0xC8, 0xE1, 0xC8);

    public BColor CompositionUnderline { get; init; } = new BColor(0x1F, 0x23, 0x28);

    // Classification.
    public BColor Comment { get; init; } = new BColor(0x1A, 0x7F, 0x37);

    public BColor DocumentationComment { get; init; } = new BColor(0x2A, 0x6F, 0x8F);

    public BColor Keyword { get; init; } = new BColor(0x00, 0x00, 0xC0);

    public BColor ControlKeyword { get; init; } = new BColor(0x8F, 0x08, 0xC4);

    public BColor PreprocessorKeyword { get; init; } = new BColor(0x6F, 0x6F, 0x6F);

    public BColor PreprocessorText { get; init; } = new BColor(0x6F, 0x6F, 0x6F);

    public BColor StringLiteral { get; init; } = new BColor(0xA3, 0x11, 0x15);

    public BColor EscapeSequence { get; init; } = new BColor(0xC9, 0x51, 0x1A);

    public BColor CharacterLiteral { get; init; } = new BColor(0xA3, 0x11, 0x15);

    public BColor NumericLiteral { get; init; } = new BColor(0x09, 0x69, 0x8A);

    public BColor Operator { get; init; } = new BColor(0x1F, 0x23, 0x28);

    public BColor Punctuation { get; init; } = new BColor(0x1F, 0x23, 0x28);

    // Diagnostics.
    public BColor ErrorAdornment { get; init; } = new BColor(0xD1, 0x24, 0x2F);

    public BColor WarningAdornment { get; init; } = new BColor(0xBF, 0x83, 0x00);

    public BColor InformationAdornment { get; init; } = new BColor(0x0A, 0x6C, 0xBD);

    /// <summary>
    /// When true, classification is conveyed by weight and slant as well as by
    /// colour, and diagnostics by adornment shape as well as by colour. High
    /// contrast and colour-vision deficiency both make colour-only encoding
    /// unreadable, so no state may depend on hue alone.
    /// </summary>
    public bool DistinguishWithoutColor { get; init; }

    public BColor GetClassificationColor(CodeClassificationKind kind) => kind switch
    {
        CodeClassificationKind.Comment => Comment,
        CodeClassificationKind.DocumentationComment => DocumentationComment,
        CodeClassificationKind.Keyword => Keyword,
        CodeClassificationKind.ControlKeyword => ControlKeyword,
        CodeClassificationKind.PreprocessorKeyword => PreprocessorKeyword,
        CodeClassificationKind.PreprocessorText => PreprocessorText,
        CodeClassificationKind.StringLiteral => StringLiteral,
        CodeClassificationKind.EscapeSequence => EscapeSequence,
        CodeClassificationKind.CharacterLiteral => CharacterLiteral,
        CodeClassificationKind.NumericLiteral => NumericLiteral,
        CodeClassificationKind.Operator => Operator,
        CodeClassificationKind.Punctuation => Punctuation,
        _ => Foreground,
    };

    public BColor GetDiagnosticColor(CodeDiagnosticSeverity severity) => severity switch
    {
        CodeDiagnosticSeverity.Error => ErrorAdornment,
        CodeDiagnosticSeverity.Warning => WarningAdornment,
        CodeDiagnosticSeverity.Information => InformationAdornment,
        _ => Foreground,
    };

    /// <summary>
    /// The adornment shape for a severity. Distinct per severity so the three
    /// remain tellable apart with no colour at all.
    /// </summary>
    public static CodeDiagnosticAdornmentShape GetDiagnosticShape(CodeDiagnosticSeverity severity) =>
        severity switch
        {
            CodeDiagnosticSeverity.Error => CodeDiagnosticAdornmentShape.Wave,
            CodeDiagnosticSeverity.Warning => CodeDiagnosticAdornmentShape.Dashed,
            CodeDiagnosticSeverity.Information => CodeDiagnosticAdornmentShape.Dotted,
            _ => CodeDiagnosticAdornmentShape.None,
        };

    /// <summary>Whether a kind is emphasized when colour cannot be relied on.</summary>
    public bool IsEmphasized(CodeClassificationKind kind) =>
        DistinguishWithoutColor &&
        kind is CodeClassificationKind.Keyword or CodeClassificationKind.ControlKeyword
            or CodeClassificationKind.PreprocessorKeyword;

    public bool IsItalic(CodeClassificationKind kind) =>
        DistinguishWithoutColor &&
        kind is CodeClassificationKind.Comment or CodeClassificationKind.DocumentationComment;
}

public enum CodeDiagnosticAdornmentShape
{
    None = 0,
    Wave,
    Dashed,
    Dotted,
}
