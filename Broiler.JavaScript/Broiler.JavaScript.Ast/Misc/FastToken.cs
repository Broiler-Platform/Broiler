using System;

namespace Broiler.JavaScript.Ast.Misc;

public class FastToken(TokenTypes type, string? source = null, string? cooked = null, string? flags = null,
    int start = 0, int length = 0, in SpanLocation startLocation = default, in SpanLocation endLocation = default,
    double number = 0, bool isKeyword = false, FastKeywords keyword = FastKeywords.none,
    FastKeywords contextualKeyword = FastKeywords.none)
{
    public static FastToken? Empty;

    public readonly TokenTypes Type = type;
    public readonly StringSpan Span = source != null ? new StringSpan(source, start, Math.Min(source.Length - start, length)) : default;
    public readonly double Number = number;
    public readonly string? CookedText = cooked;
    public readonly string? Flags = flags;
    public readonly bool IsKeyword = isKeyword;
    public readonly FastKeywords Keyword = keyword;
    public readonly FastKeywords ContextualKeyword = contextualKeyword;

    public readonly SpanLocation Start = startLocation;
    public readonly SpanLocation End = endLocation;

    public FastToken? Next;
    public FastToken? Previous;

    public FastToken AsString() => new(TokenTypes.String, Span.Source, CookedText ?? Span.Value, Flags, Span.Offset, Span.Length, Start, End, contextualKeyword: ContextualKeyword);

    public override string ToString() => $"{Type} {Span}";
}
