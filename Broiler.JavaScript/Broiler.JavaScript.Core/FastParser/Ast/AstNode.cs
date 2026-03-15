namespace YantraJS.Core.FastParser;

public abstract class AstNode(FastToken start, FastNodeType type, FastToken end, bool isStatement = false, bool isBinding = false)
{
    public readonly FastNodeType Type = type;
    public readonly FastToken Start = start;
    public readonly FastToken End = end;

    public readonly bool IsStatement = isStatement;

    public readonly bool IsBinding = isBinding;

    //public (int Start, int End) Range =>
    //    (Start.Span.Offset, End.Span.Offset + End.Span.Length);

    //public (int Start, int End) Location =>
    //    (Start.Span.Offset, Start.Span.Offset + Start.Span.Length);

    public StringSpan Code
    {
        get
        {
            var startSpan = Start.Span;
            var start = startSpan.Offset;

            if(End.Type == TokenTypes.EOF)
            {
                return startSpan;
            }

            var endSpan = End.Span;
            var end = endSpan.Offset;
            var length = endSpan.Length;

            var total = end + length - start;

            return new StringSpan(startSpan.Source,
                start,
                total);
        }
    }
}
