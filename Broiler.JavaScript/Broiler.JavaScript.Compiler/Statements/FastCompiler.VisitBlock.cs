using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitBlock(AstBlock block)
    {
        int count = block.Statements.Count;
        if (count == 0)
            return YExpression.Empty;

        var blockList = new Sequence<YExpression>(count);
        var hoistingScope = block.HoistingScope;
        var scope = this.scope.Push(new FastFunctionScope(this.scope.Top));
        
        if (hoistingScope != null)
        {
            var en = hoistingScope.GetFastEnumerator();
            while (en.MoveNext(out var v))
                scope.CreateVariable(v, null, true);
        }

        var se = block.Statements.GetFastEnumerator();
        while (se.MoveNext(out var stmt))
        {
            var exp = Visit(stmt);
            if (exp == null)
                continue;

            blockList.Add(CallStackItemBuilder.Step(scope.StackItem, stmt.Start.Start.Line, stmt.Start.Start.Column));
            blockList.Add(exp);
        }

        var result = Scoped(scope, blockList);

        scope.Dispose();
        return result;
    }
}
