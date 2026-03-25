using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Ast.Patterns;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitVariableDeclaration(AstVariableDeclaration variableDeclaration)
    {
        var dispose = variableDeclaration.Using;
        var async = variableDeclaration.AwaitUsing;
        var list = new Sequence<YExpression>();
        var top = scope.Top;
        var newScope = variableDeclaration.Kind == FastVariableKind.Const || variableDeclaration.Kind == FastVariableKind.Let;
        var ed = variableDeclaration.Declarators.GetFastEnumerator();
        while (ed.MoveNext(out var d))
        {
            switch (d.Identifier.Type)
            {
                case FastNodeType.Identifier:
                    var id = d.Identifier as AstIdentifier;
                    var v = top.CreateVariable(id.Name, JSVariableBuilder.New(id.Name.Value), newScope);
                    if (d.Init == null)
                    {
                        list.Add(v.Expression);
                    }
                    else
                    {
                        list.Add(YExpression.Assign(v.Expression, Visit(d.Init)));
                    }

                    if (dispose)
                    {
                        list.Add(top.Disposable.CallExpression<IJSDisposableStack, JSValue, bool>(() => (j, v, b) => 
                        j.AddDisposableResource(v, b), v.Expression, YExpression.Constant(async)));
                    }
                    break;

                case FastNodeType.ObjectPattern:
                    var objectPattern = d.Identifier as AstObjectPattern;
                    using (var temp = top.GetTempVariable())
                    {
                        if (d.Init != null)
                            list.Add(YExpression.Assign(temp.Variable, Visit(d.Init)));

                        list.Add(CreateAssignment(objectPattern, temp.Expression, true, newScope));

                        if (dispose)
                        {
                            list.Add(top.Disposable.CallExpression<IJSDisposableStack, JSValue, bool>(() => (j, v, b) => 
                            j.AddDisposableResource(v, b), temp.Variable, YExpression.Constant(async)));
                        }
                    }
                    break;

                case FastNodeType.ArrayPattern:
                    var arrayPattern = d.Identifier as AstArrayPattern;
                    using (var temp = scope.Top.GetTempVariable())
                    {
                        if (d.Init != null)
                            list.Add(YExpression.Assign(temp.Variable, Visit(d.Init)));

                        list.Add(CreateAssignment(arrayPattern, temp.Expression, true, newScope));
                        if (dispose)
                        {
                            list.Add(top.Disposable.CallExpression<IJSDisposableStack, JSValue, bool>(() => (j, v, b) => 
                            j.AddDisposableResource(v, b), temp.Variable, YExpression.Constant(async)));
                        }
                    }
                    break;

                default:
                    throw new FastParseException(d.Identifier.Start, $"Invalid pattern {d.Identifier.Type}");
            }
        }

        if (list.Count == 1)
        {
            var e = list[0];
            return e;
        }
        var r = YExpression.Block(list);
        return r;
    }
}
