using System;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitTaggedTemplateExpression(AstTaggedTemplateExpression template)
    {
        var callee = template.Tag;

        var args = new Sequence<YExpression>(template.Arguments.Count);
        var parts = new Sequence<YElementInit>(template.Arguments.Count);
        var raw = new Sequence<YExpression>(template.Arguments.Count);

        var e = template.Arguments.GetFastEnumerator();
        args.Add(null);

        while (e.MoveNext(out var p))
        {
            if (p.Type == FastNodeType.Literal)
            {
                var l = p as AstLiteral;
                if (l.TokenType == TokenTypes.TemplatePart)
                {
                    var r = l.Start.Span.Value;
                    r = r.Trim('`');

                    if (r.StartsWith("}"))
                        r = r.TrimStart('}');

                    if (r.EndsWith("${"))
                        r = r.Substring(0, r.Length - 2);

                    raw.Add(JSStringBuilder.New(YExpression.Constant(r)));
                    parts.Add(new YElementInit(JSArrayBuilder._Add, JSStringBuilder.New(YExpression.Constant(l.StringValue))));
                    continue;
                }
            }

            args.Add(VisitExpression(p));
        }

        // replace first node...
        var rawArray = JSArrayBuilder.New(raw);
        parts.Add(new YElementInit(JSObjectBuilder._FastAddValueKeyString, KeyOfName("raw"), rawArray, JSPropertyAttributesBuilder.EnumerableConfigurableValue));

        var partsArray = JSArrayBuilder.New(parts);
        args[0] = partsArray;

        if (callee.Type == FastNodeType.MemberExpression && callee is AstMemberExpression me)
        {
            YExpression name;

            switch (me.Property.Type)
            {
                case FastNodeType.Identifier:
                    var id = (me.Property as AstIdentifier)!;
                    name = me.Computed ? VisitExpression(id) : KeyOfName(id.Name);
                    break;

                case FastNodeType.Literal:
                    var l = (me.Property as AstLiteral)!;
                    if (l.TokenType == TokenTypes.String)
                        name = KeyOfName(l.Start.CookedText);
                    else if (l.TokenType == TokenTypes.Number)
                        name = YExpression.Constant((uint)l.NumericValue);
                    else
                        throw new NotImplementedException();
                    break;

                case FastNodeType.MemberExpression:
                    name = VisitMemberExpression(me.Property as AstMemberExpression);
                    break;

                default:
                    throw new NotImplementedException($"{me.Property}");
            }

            bool isSuper = me.Object.Type == FastNodeType.Super;
            var super = isSuper ? scope.Top.Super : null;
            var target = isSuper ? scope.Top.ThisExpression : VisitExpression(me.Object);

            if (isSuper)
            {
                var superMethod = JSValueBuilder.Index(super, name, me.Coalesce);
                return JSFunctionBuilder.InvokeFunction(superMethod, ArgumentsBuilder.New(JSUndefinedBuilder.Value, args), me.Coalesce);
            }

            using var te = scope.Top.GetTempVariable(typeof(JSValue));
            using var te2 = scope.Top.GetTempVariable(typeof(JSValue));
            return JSValueBuilder.InvokeMethod(te.Variable, te2.Variable, target, name, args, false, me.Coalesce);
        }
        else
        {
            bool isSuper = callee.Type == FastNodeType.Super;

            if (isSuper)
            {
                var paramArray1 = ArgumentsBuilder.New(JSUndefinedBuilder.Value, args);
                return JSFunctionBuilder.InvokeSuperConstructor(scope.Top.Super, scope.Top.ThisExpression, paramArray1);
            }

            var target = VisitExpression(callee);
            return JSFunctionBuilder.InvokeFunction(target, ArgumentsBuilder.New(JSUndefinedBuilder.Value, args));
        }
    }
}
