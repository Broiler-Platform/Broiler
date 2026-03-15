using YantraJS.Expressions;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitAwaitExpression(AstAwaitExpression node)
    {
        var target = VisitExpression(node.Argument);
        return YExpression.Yield(target);
    }
}
