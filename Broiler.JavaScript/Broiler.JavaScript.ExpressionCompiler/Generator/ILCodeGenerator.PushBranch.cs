using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Generator;

namespace Broiler.JavaScript.Generator;

public partial class ILCodeGenerator
{

    private void Goto(ILWriterLabel label) => il.Branch(label);

    internal void EmitConstructor(YLambdaExpression cnstrLambda)
    {
        il.EmitLoadArg(0);
        Emit(cnstrLambda);
    }
}
