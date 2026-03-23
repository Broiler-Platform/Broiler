using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitNewExpression(AstNewExpression newExpression) 
    {
        var constructor = VisitExpression(newExpression.Callee);
        var args = VisitArguments(null, newExpression.Arguments);
    
        return JSValueBuilder.CreateInstance(constructor, args);
    }
}
