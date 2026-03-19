using System;
using System.Linq;

using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using ParameterExpression = Broiler.JavaScript.ExpressionCompiler.Expressions.YParameterExpression;
using Broiler.JavaScript.Core.Core.Disposable;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    private Expression Scoped(FastFunctionScope scope, IFastEnumerable<Expression> body)
    {
        var list = new Sequence<Exp>();
        list.AddRange(scope.InitList);
        list.AddRange(body);

        if (scope.VariableParameters.Any() && !list.Any())
            throw new InvalidOperationException();

        if (!list.Any())
            return Exp.Empty;

        var r = Exp.Block(scope.VariableParameters.AsSequence(), list);

        if (scope.HasDisposable)
        {
            list =
            [
                // create new disposable and assign ...
                Expression.Assign(scope.Disposable,Expression.New(scope.Disposable.Type))
            ];

            var d = scope.Disposable;
            var dispose = d.CallExpression<JSDisposableStack, JSValue>(() => (j) => j.Dispose());
            if (scope.Function.Async)
            {
                // we will move everything inside await dispose...
                list.Add(Exp.TryFinally(r, Exp.Yield(dispose)));
            }
            else
            {
                list.Add(Exp.TryFinally(r, dispose));
            }

            return Exp.Block(new Sequence<ParameterExpression> { scope.Disposable }, list);
        }

        return r;
    }


    protected override Expression VisitProgram(AstProgram program)
    {
        var blockList = new Sequence<Expression>(program.Statements.Count);
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
