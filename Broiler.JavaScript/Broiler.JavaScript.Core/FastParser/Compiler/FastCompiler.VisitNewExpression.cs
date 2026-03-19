using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.LinqExpressions;
using Exp = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitNewExpression(AstNewExpression newExpression) 
    {
        var constructor = VisitExpression(newExpression.Callee);
        var args = VisitArguments(null, newExpression.Arguments);
    
        return JSValueBuilder.CreateInstance(constructor, args);
    }
}
