using System.Reflection.Emit;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitJumpSwitch(YJumpSwitchExpression node)
    {

        Visit(node.Target);
        var cases = node.Cases;
        int length = cases.Count;
        var labels = new Label[length];
        var en = cases.GetFastEnumerator();
        while(en.MoveNext(out var item, out var i))
        {
            labels[i] = this.labels[item].Value;
        }
        il.Emit(OpCodes.Switch, labels);
        return true;
    }
}
