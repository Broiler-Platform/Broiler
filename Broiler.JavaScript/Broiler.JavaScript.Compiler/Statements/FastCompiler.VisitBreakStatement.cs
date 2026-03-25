using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;


namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitBreakStatement(AstBreakStatement breakStatement)
    {
        var ls = LoopScope;
        string name = breakStatement.Label?.Name.Value;
        
        if (name != null)
        {
            var target = LoopScope.Get(name);
            return target == null ? throw JSEngine.NewSyntaxError($"No label found for {name}") : YExpression.Break(target.Break);
        }

        if (ls.IsSwitch)
            return YExpression.Goto(ls.Break);

        return YExpression.Break(ls.Break);
    }
}
