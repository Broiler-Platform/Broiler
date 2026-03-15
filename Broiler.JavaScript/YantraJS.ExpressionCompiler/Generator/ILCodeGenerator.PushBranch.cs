using YantraJS.Core;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{

    private void Goto(ILWriterLabel label) => il.Branch(label);

    internal void EmitConstructor(YLambdaExpression cnstrLambda)
    {
        il.EmitLoadArg(0);
        Emit(cnstrLambda);
    }
}
