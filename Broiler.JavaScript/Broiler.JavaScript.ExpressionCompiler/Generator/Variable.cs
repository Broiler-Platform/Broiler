using System.Reflection.Emit;

namespace YantraJS.Generator;

public class Variable(LocalBuilder builder, bool isArg, short index, bool isReference, string name)
{
    public readonly LocalBuilder LocalBuilder = builder;
    public readonly bool IsArgument = isArg;
    public readonly short Index = index;
    public readonly bool IsReference = isReference;
    public readonly string Name = name;
}
