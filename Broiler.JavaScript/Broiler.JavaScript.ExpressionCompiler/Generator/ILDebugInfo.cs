#nullable enable


using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public readonly struct ILDebugInfo(int ilOffset, in Position start, in Position end)
{
    public readonly int ILOffset = ilOffset;
    public readonly Position Start = start;
    public readonly Position End = end;
}
