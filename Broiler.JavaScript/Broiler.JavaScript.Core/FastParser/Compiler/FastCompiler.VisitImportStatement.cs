using YantraJS.ExpHelper;

using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{

    protected override Exp VisitImportStatement(AstImportStatement importStatement)
    {
        var tempRequire = Exp.Parameter(typeof(JSValue));
        var require = scope.Top.GetVariable("import");
        var source = VisitExpression(importStatement.Source);
        var args = ArgumentsBuilder.New(JSUndefinedBuilder.Value, source);
        var stmts = new Sequence<Exp>
        {
            Exp.Assign(tempRequire, Expression.Yield(JSFunctionBuilder.InvokeFunction(require.Expression, args)))
        };
        FastFunctionScope.VariableScope imported;

        var all = importStatement.All;

        if (all != null)
        {
            imported = scope.Top.CreateVariable(all.Name);
            stmts.Add(Exp.Assign(imported.Expression, tempRequire));
        }

        if(importStatement.Default != null)
        {
            imported = scope.Top.CreateVariable(importStatement.Default.Name);
            var prop = JSValueBuilder.Index(tempRequire, KeyOfName("default"));
            stmts.Add(Exp.Assign(imported.Expression, prop));

        }

        if(importStatement.Members != null)
        {
            var ve = importStatement.Members.GetFastEnumerator();
            while(ve.MoveNext(out var item)) {
                imported = scope.Top.CreateVariable(item.asName);
                var prop = JSValueBuilder.Index(tempRequire, KeyOfName(item.name));
                stmts.Add(Exp.Assign(imported.Expression, prop));
            }
        }

        var importExp =  Exp.Block(
            tempRequire.AsSequence(),
            stmts);
        return importExp;
    }

}
