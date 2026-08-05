namespace Broiler.Code.Language.CSharp.Syntax;

/// <summary>
/// The lexer state at the start of a line.
///
/// Two lines with equal start state classify identically for identical text,
/// which is what lets incremental classification stop as soon as the state
/// reconverges after an edit. Without it, opening a block comment near the top
/// of a file would have no way to know where its effect ends.
/// </summary>
internal readonly record struct LineState(LineStateKind Kind, byte QuoteCount, byte DollarCount)
{
    public static readonly LineState Default = new(LineStateKind.Default, 0, 0);
}

internal enum LineStateKind : byte
{
    Default = 0,
    BlockComment,
    DocumentationBlockComment,
    VerbatimString,

    /// <summary>
    /// A raw string, which closes only on a run of at least
    /// <see cref="LineState.QuoteCount"/> quotes — so the count is part of the
    /// state, not a fixed delimiter.
    /// </summary>
    RawString,
}
