using System;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitObjectLiteral(AstObjectLiteral objectExpression)
    {
        var elements = new Sequence<YElementInit>();
        var en = objectExpression.Properties.GetFastEnumerator();

        while (en.MoveNext(out var pn))
        {
            switch (pn.Type)
            {
                case FastNodeType.SpreadElement:
                    var spread = pn as AstSpreadElement;
                    elements.Add(new YElementInit(JSObjectBuilder._FastAddRange, Visit(spread.Argument)));
                    continue;

                case FastNodeType.ClassProperty:
                    break;

                default:
                    throw new FastParseException(pn.Start, $"Invalid token {pn.Start} in object literal");
            }

            AstClassProperty p = pn as AstClassProperty;

            YExpression key = null;
            YExpression value = null;
            var pKey = p.Key;

            value = VisitExpression(p.Init);

            if (p.Computed)
            {
                // there is a possibility of numeric index
                var keyExp = pKey.IsUIntLiteral(out var num) ? YExpression.Constant(num) : Visit(pKey);

                if (p.Kind == AstPropertyKind.Get)
                {
                    elements.Add(JSObjectBuilder.AddGetter(keyExp, value));
                    continue;
                }

                if (p.Kind == AstPropertyKind.Set)
                {
                    elements.Add(JSObjectBuilder.AddSetter(keyExp, value));
                    continue;
                }

                elements.Add(JSObjectBuilder.AddValue(keyExp, value));
                continue;
            }

            switch (pKey.Type)
            {
                case FastNodeType.Identifier:
                    var id = pKey as AstIdentifier;
                    if (!p.Computed)
                    {
                        key = KeyOfName(id.Name);
                    }
                    else
                    {
                        key = scope.Top.GetVariable(id.Name).Expression;
                    }
                    break;

                case FastNodeType.Literal:
                    var l = pKey as AstLiteral;
                    if (l.TokenType == TokenTypes.String)
                    {
                        if (NumberParser.TryCoerceToUInt32(l.StringValue, out var ui))
                        {
                            key = YExpression.Constant(ui);

                        }
                        else
                        {
                            key = KeyOfName(l.StringValue);
                        }
                    }
                    else if (l.TokenType == TokenTypes.Number)
                    {
                        key = YExpression.Constant((uint)l.NumericValue);
                    }
                    else
                        throw new NotSupportedException();

                    break;

                default:
                    throw new NotSupportedException();
            }

            switch (p.Kind)
            {
                case AstPropertyKind.Get:
                    elements.Add(JSObjectBuilder.AddGetter(key, value));
                    break;

                case AstPropertyKind.Set:
                    elements.Add(JSObjectBuilder.AddSetter(key, value));
                    break;

                default:
                    elements.Add(JSObjectBuilder.AddValue(key, value));
                    break;
            }
        }

        if (elements.Any())
        {
            var r = JSObjectBuilder.New(elements);
            return r;
        }

        return JSObjectBuilder.New();
    }
}
