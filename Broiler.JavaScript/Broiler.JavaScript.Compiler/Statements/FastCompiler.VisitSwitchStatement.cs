using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;
using System;
using System.Linq;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.LinqExpressions.Utils;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    class SwitchInfo(FastPool.Scope scope)
    {
        public Sequence<YExpression> Tests = [];
        public Sequence<YExpression> Body;
        public readonly YLabelTarget Label = YExpression.Label("case-start");
    }

    protected override YExpression VisitSwitchStatement(AstSwitchStatement switchStatement)
    {
        bool allStrings = true;
        bool allNumbers = true;
        bool allIntegers = true;

        var scope = pool.NewScope();

        try
        {

            Sequence<YExpression> defBody = null;
            var @continue = this.scope.Top.Loop?.Top?.Continue;
            var @break = YExpression.Label();
            var ls = new LoopScope(@break, @continue, true);
            var cases = new Sequence<SwitchInfo>(switchStatement.Cases.Count + 2);
            using var bt = this.scope.Top.Loop.Push(ls);
            SwitchInfo lastCase = new(scope);
            var casesEn = switchStatement.Cases.GetFastEnumerator();

            while (casesEn.MoveNext(out var c))
            {
                var body = new Sequence<YExpression>(c.Statements.Count);
                var en = c.Statements.GetFastEnumerator();

                while (en.MoveNext(out var es))
                {
                    switch (es)
                    {
                        case AstStatement stmt:
                            body.Add(VisitStatement(stmt));
                            break;

                        default:
                            throw new FastParseException(es.Start, $"Invalid statement {es.Type}");
                    }
                }

                if (c.Test == null)
                {
                    defBody = body;
                    lastCase = new SwitchInfo(scope);

                    continue;
                }

                YExpression test = null;
                switch (c.Test.Type)
                {
                    case FastNodeType.UnaryExpression:
                        var unary = c.Test as AstUnaryExpression;
                        var isTestSet = false;

                        switch (unary.Operator)
                        {
                            case UnaryOperator.Plus:
                            case UnaryOperator.Minus:
                                if (unary.Argument.Type == FastNodeType.Literal)
                                {
                                    var l = unary.Argument as AstLiteral;

                                    if (l.TokenType == TokenTypes.Number)
                                    {
                                        var n = l.NumericValue;
                                        if ((n % 1) != 0)
                                            allIntegers = false;

                                        var ln = l.NumericValue;
                                        if (unary.Operator == UnaryOperator.Minus)
                                            ln = -ln;

                                        test = YExpression.Constant(ln);
                                        isTestSet = true;
                                        break;
                                    }
                                }

                                break;
                        }

                        if (!isTestSet)
                        {
                            test = VisitExpression(c.Test);
                            allNumbers = false;
                            allStrings = false;
                            allIntegers = false;
                        }

                        break;

                    case FastNodeType.Literal:
                        var literal = c.Test as AstLiteral;

                        switch (literal.TokenType)
                        {
                            case TokenTypes.String:
                                allNumbers = false;
                                // allStrings = allStrings && true ;
                                test = YExpression.Constant(literal.StringValue);
                                break;

                            case TokenTypes.Number:
                                var n = literal.NumericValue;
                                if ((n % 1) != 0)
                                    allIntegers = false;

                                test = YExpression.Constant(literal.NumericValue);
                                break;

                            case TokenTypes.True:
                                allNumbers = false;
                                allStrings = false;
                                allIntegers = false;
                                test = JSBooleanBuilder.True;
                                break;

                            case TokenTypes.False:
                                allNumbers = false;
                                allStrings = false;
                                allIntegers = false;
                                test = JSBooleanBuilder.False;
                                break;

                            default:
                                throw new NotImplementedException();
                        }

                        break;
                    default:
                        test = VisitExpression(c.Test);
                        allNumbers = false;
                        allStrings = false;
                        allIntegers = false;

                        break;
                }

                lastCase.Tests.Add(test);

                if (body.Count > 0)
                {
                    cases.Add(lastCase);
                    body.Insert(0, YExpression.Label(lastCase.Label));
                    lastCase.Body = body;
                    lastCase = new SwitchInfo(scope);
                }
            }

            System.Reflection.MethodInfo equalsMethod = null;

            SwitchInfo last = null;
            foreach (var @case in cases)
            {
                // if last one is not break statement... make it fall through...
                last?.Body.Add(YExpression.Goto(@case.Label));
                last = @case;

                if (allNumbers)
                {
                    if (allIntegers)
                    {
                        @case.Tests = @case.Tests.ConvertToInteger(scope);
                    }
                    else
                    {
                        // convert every case to double..
                        @case.Tests = @case.Tests.ConvertToNumber(scope);
                    }
                }
                else
                {
                    if (allStrings)
                    {
                        // force everything to string if it isn't
                        @case.Tests = @case.Tests.ConvertToString(scope);
                    }
                    else
                    {
                        @case.Tests = @case.Tests.ConvertToJSValue(scope);
                        equalsMethod = JSValueBuilder.StaticEquals;
                    }
                }
            }

            var testTarget = VisitExpression(switchStatement.Target);
            if (allNumbers)
            {
                if (allIntegers)
                {
                    testTarget = JSValueBuilder.IntValue(testTarget);
                }
                else
                {
                    testTarget = JSValueBuilder.DoubleValue(testTarget);
                }
            }
            else
            {
                if (allStrings)
                {
                    testTarget = ObjectBuilder.ToString(testTarget);
                }
                else
                {

                }
            }

            YExpression d = null;
            var lastLine = switchStatement.Start.Start.Line;

            if (defBody != null)
            {
                var defLabel = YExpression.Label($"default-start-{lastLine}");
                last?.Body.Add(YExpression.Goto(defLabel));

                defBody.Insert(0, YExpression.Label(defLabel));
                d = YExpression.Block(defBody);
            }

            var r = YExpression.Block(
                YExpression.Switch(testTarget, d.ToJSValue() ?? JSUndefinedBuilder.Value, equalsMethod, [.. cases.Select(x =>
                YExpression.SwitchCase(YExpression.Block(x.Body).ToJSValue(), x.Tests))]), YExpression.Label(@break));
            return r;
        }
        finally
        {
            scope.Dispose();
        }
    }
}
