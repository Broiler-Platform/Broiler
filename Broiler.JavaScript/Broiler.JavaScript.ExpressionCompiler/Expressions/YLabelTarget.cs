#nullable enable
using System;
using System.Threading;

namespace Broiler.JavaScript.ExpressionCompiler.Expressions;

public class YLabelTarget
{
    public readonly string Name;
    public readonly Type LabelType;

    private static int id = 0;

    public YLabelTarget(string? name, Type type)
    {
        name ??= $"LABEL_{Interlocked.Increment(ref id)}";
        Name = name;
        LabelType = type;
    }
}