using Exp = YantraJS.Expressions.YExpression;


namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitBreakStatement(AstBreakStatement breakStatement)
    {
        var ls = LoopScope;

        string name = breakStatement.Label?.Name.Value;
        if (name != null)
        {
            var target = LoopScope.Get(name);
            if (target == null)
                throw JSContext.Current.NewSyntaxError($"No label found for {name}");
            return Exp.Break(target.Break);
        }

        if (ls.IsSwitch)
            return Exp.Goto(ls.Break);

        return Exp.Break(ls.Break);
    }
}
