using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Core.LinqExpressions;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{

    protected override Expression VisitBlock(AstBlock block) {

        int count = block.Statements.Count;
        if (count == 0)
            return Expression.Empty;

        var blockList = new Sequence<Expressions.YExpression>(count);
        var hoistingScope = block.HoistingScope;
        var scope = this.scope.Push(new FastFunctionScope(this.scope.Top));
        //try
        //{
            if (hoistingScope != null)
            {
                var en = hoistingScope.GetFastEnumerator();
                while (en.MoveNext(out var v))
                {
                    scope.CreateVariable(v, null, true);
                }
            }

            var se = block.Statements.GetFastEnumerator();
            while (se.MoveNext(out var stmt))
            {
                //LexicalScopeBuilder.Update(
                //    blockList, 
                //    scope.StackItem, 
                //    stmt.Start.Start.Line, 
                //    stmt.Start.Start.Column);
                var exp = Visit(stmt);
                if (exp == null)
                    continue;
                blockList.Add(CallStackItemBuilder.Step(scope.StackItem, stmt.Start.Start.Line, stmt.Start.Start.Column));
                blockList.Add(exp);
            }
            var result = Scoped(scope, blockList);
            // blockList.Clear();
            scope.Dispose();
            return result;
        //}
        //finally
        //{
        //    blockList.Clear();
        //    scope.Dispose();
        //}
    }
}
