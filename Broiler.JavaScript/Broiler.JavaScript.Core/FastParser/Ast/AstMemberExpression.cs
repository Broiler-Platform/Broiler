#nullable enable
namespace YantraJS.Core.FastParser;

public class AstMemberExpression(
    AstExpression target,
    AstExpression node,
    bool computed = false,
    bool coalesce = false) : AstExpression(target.End, FastNodeType.MemberExpression, node.End)
{
    public readonly AstExpression Object = target;
    public readonly AstExpression Property = node;
    public readonly bool Computed = computed;
    public readonly bool Coalesce = coalesce;

    public override string ToString()
    {
        if(Computed)
            return $"{Object}[{Property}]";
        if(Coalesce)
            return $"{Object}?.{Property}";
        return $"{Object}.{Property}";
    }
}