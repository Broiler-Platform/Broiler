using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitAwaitExpression(AstAwaitExpression node)
    {
        var target = VisitExpression(node.Argument);
        return YExpression.Yield(target);
    }
}
