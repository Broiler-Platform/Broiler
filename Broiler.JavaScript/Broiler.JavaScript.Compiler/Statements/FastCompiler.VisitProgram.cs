using System;
using System.Linq;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    private YExpression Scoped(FastFunctionScope scope, IFastEnumerable<YExpression> body)
    {
        var list = new Sequence<YExpression>();
        list.AddRange(scope.InitList);
        list.AddRange(body);

        if (scope.VariableParameters.Any() && !list.Any())
            throw new InvalidOperationException();

        if (!list.Any())
            return YExpression.Empty;

        var r = YExpression.Block(scope.VariableParameters.AsSequence(), list);

        if (scope.HasDisposable)
        {
            list =
            [
                // create new disposable via factory delegate ...
                YExpression.Assign(scope.Disposable,
                    NewLambdaExpression.StaticCallExpression<IJSDisposableStack>(() => () => IJSDisposableStack.New()))
            ];

            var d = scope.Disposable;
            var dispose = d.CallExpression<IJSDisposableStack, JSValue>(() => (j) => j.Dispose());
            if (scope.Function.Async)
            {
                // we will move everything inside await dispose...
                list.Add(YExpression.TryFinally(r, YExpression.Yield(dispose)));
            }
            else
            {
                list.Add(YExpression.TryFinally(r, dispose));
            }

            return YExpression.Block(new Sequence<YParameterExpression> { scope.Disposable }, list);
        }

        return r;
    }


    protected override YExpression VisitProgram(AstProgram program)
    {
        var blockList = new Sequence<YExpression>(program.Statements.Count);
        ref var hoistingScope = ref program.HoistingScope;
        var scope = this.scope.Push(new FastFunctionScope(this.scope.Top));

        if (hoistingScope != null)
        {
            var en = hoistingScope.GetFastEnumerator();
            var top = this.scope.Top;
        
            while (en.MoveNext(out var v))
            {
                var g = JSValueBuilder.Index(top.Context, KeyOfName(v));
                var vs = scope.CreateVariable(v, null, true);

                vs.Expression = JSVariableBuilder.Property(vs.Variable);
                vs.SetInit(JSVariableBuilder.New(g, v.Value));
            }
        }

        var se = program.Statements.GetFastEnumerator();
        while (se.MoveNext(out var stmt))
        {
            var exp = Visit(stmt);
            if (exp == null)
                continue;

            blockList.Add(CallStackItemBuilder.Step(scope.StackItem, stmt.Start.Start.Line, stmt.Start.Start.Column));
            blockList.Add(exp);
        }

        var r = Scoped(scope, blockList);

        scope.Dispose();
        return r;
    }
}
