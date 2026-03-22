using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Parser;


partial class FastParser
{
    /// <summary>
    /// delete SingleComputedExpression
    /// void SingleComputedExpression
    /// typeof SingleComputedExpression
    /// +SingleComputedExpression
    /// -SingleComputedExpression
    /// ~SingleComputedExpression
    /// !SingleComputedExpression
    /// ++SingleComputedExpression
    /// --SingleComputedExpression
    /// SingleComputedExpression++
    /// SingleComputedExpression--
    /// </summary>
    /// <param name="node"></param>
    /// <param name="hasAsync"></param>
    /// <param name="hasGenerator"></param>
    /// <returns></returns>
    bool SinglePrefixPostfixExpression(out AstExpression node, out bool hasAsync, out bool hasGenerator, UnaryOperator previous = UnaryOperator.None, FastToken previousToken = null)
    {
        var begin = BeginUndo();
        if (HasUnaryOperator(out var prefix, out var token))
        {
            if (!SinglePrefixPostfixExpression(out node, out hasAsync, out hasGenerator, prefix, token))
                return begin.Reset();

            if (previous != UnaryOperator.None && previous != UnaryOperator.@new)
                node = new AstUnaryExpression(previousToken, node, previous);

            return true;
        }

        hasAsync = false;
        hasGenerator = false;

        if (stream.CheckAndConsume(FastKeywords.async))
            hasAsync = true;

        if (stream.CheckAndConsume(TokenTypes.Multiply))
            hasGenerator = true;

        if (!SingleMemberExpression(out node, previous == UnaryOperator.@new))
            return begin.Reset();

        if (previous != UnaryOperator.None)
        {
            if (previous != UnaryOperator.@new)
                node = new AstUnaryExpression(previousToken, node, previous);

            return true;
        }

        while (true)
        {
            if (HasUnaryOperator(out var postfix, out var postFixToken, false))
                node = new AstUnaryExpression(postFixToken, node, postfix, false);
            else
                break;
        }

        if (node.Type == FastNodeType.FunctionExpression)
        {
            var fx = node as AstFunctionExpression;

            if (hasAsync)
                fx.Async = hasAsync;

            if (hasGenerator)
                fx.Generator = hasGenerator;
        }

        return true;

        bool HasUnaryOperator(out UnaryOperator unaryOperator, out FastToken token, bool prefix = true)
        {
            var m = stream.SkipNewLines();
            unaryOperator = UnaryOperator.None;
            token = stream.Current;

            switch (token.Type)
            {
                case TokenTypes.Plus:
                    if (prefix)
                    {
                        stream.Consume();
                        unaryOperator = UnaryOperator.Plus;

                        return true;
                    }
                    return false;

                case TokenTypes.Minus:
                    if (prefix)
                    {
                        stream.Consume();
                        unaryOperator = UnaryOperator.Minus;
                        return true;
                    }
                    m.Undo();
                    return false;

                case TokenTypes.Increment:
                    if (m.LinesSkipped)
                    {
                        m.Undo();
                        return false;
                    }
                    stream.Consume();
                    unaryOperator = UnaryOperator.Increment;
                    return true;

                case TokenTypes.Decrement:
                    if (m.LinesSkipped)
                    {
                        m.Undo();
                        return false;
                    }
                    stream.Consume();
                    unaryOperator = UnaryOperator.Decrement;
                    return true;

                case TokenTypes.Negate:
                    if (prefix)
                    {
                        stream.Consume();
                        unaryOperator = UnaryOperator.Negate;
                        return true;
                    }
                    break;

                case TokenTypes.BitwiseNot:
                    if (prefix)
                    {
                        stream.Consume();
                        unaryOperator = UnaryOperator.BitwiseNot;
                        return true;
                    }
                    break;
            }

            if (!prefix)
            {
                m.Undo();
                return false;
            }

            switch (token.Keyword)
            {
                case FastKeywords.@new:
                    if (stream.Next.Type == TokenTypes.Dot)
                    {
                        m.Undo();
                        return false;
                    }
                    stream.Consume();
                    unaryOperator = UnaryOperator.@new;
                    return true;

                case FastKeywords.@typeof:
                    stream.Consume();
                    unaryOperator = UnaryOperator.@typeof;
                    return true;

                case FastKeywords.delete:
                    stream.Consume();
                    unaryOperator = UnaryOperator.delete;
                    return true;

                case FastKeywords.@void:
                    stream.Consume();
                    unaryOperator = UnaryOperator.@void;
                    return true;

                default:
                    m.Undo();
                    return false;
            }
        }
    }
}
