
using Broiler.JavaScript.Core.LinqExpressions;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitNewExpression(AstNewExpression newExpression) 
    {
        var constructor = VisitExpression(newExpression.Callee);
        var args = VisitArguments(null, newExpression.Arguments);
    
        return JSValueBuilder.CreateInstance(constructor, args);
    }
}
