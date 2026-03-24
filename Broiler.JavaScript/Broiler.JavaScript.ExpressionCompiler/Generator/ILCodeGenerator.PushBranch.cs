using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{

    private void Goto(ILWriterLabel label) => il.Branch(label);

    internal void EmitConstructor(YLambdaExpression cnstrLambda)
    {
        il.EmitLoadArg(0);
        Emit(cnstrLambda);
    }
}
