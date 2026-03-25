using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Runtime;
using System;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitCallExpression(AstCallExpression callExpression)
    {
        var ce = VisitCallExpression(callExpression.Callee, callExpression.Arguments, callExpression.Coalesce);
        return ce;
    }

    protected (IFastEnumerable<YExpression> args, bool hasSpread) VisitArguments(IFastEnumerable<AstExpression> arguments)
    {
        var args = new Sequence<YExpression>(arguments.Count);
        bool hasSpread = false;
        var e = arguments.GetFastEnumerator();

        while (e.MoveNext(out var ae))
        {
            if (ae.Type != FastNodeType.SpreadElement)
            {
                args.Add(Visit(ae));
                continue;
            }

            // spread....
            var sae = (ae as AstSpreadElement)!.Argument;
            args.Add(JSSpreadValueBuilder.New(Visit(sae)));
            hasSpread = true;
        }

        var result = args.Any() ? (args, hasSpread) : (Sequence<YExpression>.Empty, false);
        return result;
    }

    protected YExpression VisitArguments(YExpression? thisArg, IFastEnumerable<AstExpression> arguments, YExpression? newTarget = null)
    {
        var args = new Sequence<YExpression>(arguments.Count);
        bool hasSpread = false;
        var e = arguments.GetFastEnumerator();

        while (e.MoveNext(out var ae))
        {
            if (ae.Type != FastNodeType.SpreadElement)
            {
                args.Add(Visit(ae));
                continue;
            }

            // spread....
            var sae = (ae as AstSpreadElement)!.Argument;
            args.Add(JSSpreadValueBuilder.New(Visit(sae)));
            hasSpread = true;
        }

        if (!args.Any())
        {
            if (thisArg == null)
                return ArgumentsBuilder.Empty();

            return ArgumentsBuilder.NewEmpty(thisArg);
        }

        thisArg ??= JSUndefinedBuilder.Value;
        if (hasSpread)
        {
            var r = ArgumentsBuilder.Spread(thisArg, args);
            return r;
        }

        var result = ArgumentsBuilder.New(thisArg, args);
        return result;
    }

    protected YExpression VisitCallExpression(AstExpression callee, IFastEnumerable<AstExpression> arguments, bool coalesce = false)
    {
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
                    name = Visit(me.Property);
                    break;
            }

            bool isSuper = me.Object.Type == FastNodeType.Super;
            var super = isSuper ? scope.Top.Super : null;
            var target = isSuper ? scope.Top.ThisExpression : VisitExpression(me.Object);

            if (isSuper)
            {
                var paramArray = VisitArguments(isSuper ? target : null, arguments);
                var superMethod = JSValueBuilder.Index(super, name, me.Coalesce);

                return JSFunctionBuilder.InvokeFunction(superMethod, paramArray, me.Coalesce);
            }

            var (args, spread) = VisitArguments(arguments);
            using var te = scope.Top.GetTempVariable(typeof(JSValue));
            using var te2 = scope.Top.GetTempVariable(typeof(JSValue));

            return JSValueBuilder.InvokeMethod(te.Variable, te2.Variable, target, name, args, spread, me.Coalesce || coalesce);
        }
        else
        {
            bool isSuper = callee.Type == FastNodeType.Super;
            var @this = scope.Top.ThisExpression;

            if (isSuper)
            {
                // check if there are pending member inits...
                var paramArray1 = VisitArguments(@this, arguments);
                FastFunctionScope top = scope.Top;
                var members = top.MemberInits;
                var super = top.Super;

                // we need to set this to null
                // to inform function creator that we have
                // initialized members.. and super has been called...
                if (members?.Any() ?? false)
                {
                    var initList = new Sequence<YExpression>() { JSFunctionBuilder.InvokeSuperConstructor(super, @this, paramArray1) };
                    InitMembers(initList, top);
                    top.MemberInits = null;

                    return YExpression.Block(initList);
                }
                
                return JSFunctionBuilder.InvokeSuperConstructor(super, @this, paramArray1);
            }

            var paramArray = VisitArguments(null, arguments);
            var target = VisitExpression(callee);
            return JSFunctionBuilder.InvokeFunction(target, paramArray, coalesce);
        }
    }
}
