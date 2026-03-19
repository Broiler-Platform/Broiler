#nullable enable
using System;

namespace Broiler.JavaScript.Ast;

public class FastToken
{
    public static FastToken? Empty;

    public readonly TokenTypes Type;
    public readonly StringSpan Span;
    public readonly double Number;
    public readonly string? CookedText;
    public readonly string? Flags;
    public readonly bool IsKeyword;
    public readonly FastKeywords Keyword;
    public readonly FastKeywords ContextualKeyword;

    public readonly SpanLocation Start;
    public readonly SpanLocation End;

    public FastToken? Next;
    public FastToken? Previous;

    public FastToken AsString() => new(TokenTypes.String, Span.Source, CookedText ?? Span.Value, Flags, Span.Offset, Span.Length, Start, End, contextualKeyword: ContextualKeyword);

    /// <summary>
    /// General-purpose constructor. Accepts all pre-computed values.
    /// Number parsing and keyword classification are handled by the caller
    /// (typically the scanner/lexer).
    /// </summary>
    public FastToken(TokenTypes type, string? source = null, string? cooked = null, string? flags = null,
        int start = 0, int length = 0, in SpanLocation startLocation = default, in SpanLocation endLocation = default,
        double number = 0, bool isKeyword = false, FastKeywords keyword = FastKeywords.none,
        FastKeywords contextualKeyword = FastKeywords.none)
    {
        Type = type;
        Start = startLocation;
        End = endLocation;
        Span = source != null ? new StringSpan(source, start, Math.Min(source.Length - start, length)) : default;
        CookedText = cooked;
        Flags = flags;
        Number = number;
        IsKeyword = isKeyword;
        Keyword = keyword;
        ContextualKeyword = contextualKeyword;
    }

    public override string ToString() => $"{Type} {Span}";
}
