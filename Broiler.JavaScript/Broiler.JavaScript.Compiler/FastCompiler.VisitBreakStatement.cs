using Broiler.JavaScript.Core.Core;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;


namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitBreakStatement(AstBreakStatement breakStatement)
    {
        var ls = LoopScope;
        string name = breakStatement.Label?.Name.Value;
        
        if (name != null)
        {
            var target = LoopScope.Get(name);
            return target == null ? throw JSContext.NewSyntaxError($"No label found for {name}") : Exp.Break(target.Break);
        }

        if (ls.IsSwitch)
            return Exp.Goto(ls.Break);

        return Exp.Break(ls.Break);
    }
}
