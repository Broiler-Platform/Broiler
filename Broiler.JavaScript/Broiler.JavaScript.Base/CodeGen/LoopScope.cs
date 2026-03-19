using YantraJS.Core;
using LabelTarget = YantraJS.Expressions.YLabelTarget;

namespace Broiler.JavaScript.Core.CodeGen;

public class LoopScope(LabelTarget breakTarget, LabelTarget continueTarget, bool isSwitch = false, string name = null) : LinkedStackItem<LoopScope>
{
    public readonly LabelTarget Break = breakTarget;
    public readonly LabelTarget Continue = continueTarget;
    public readonly string Name = name;
    public readonly bool IsSwitch = isSwitch;

    public LoopScope Get(string name)
    {
        var start = this;
        while (start != null && start.Name != name)
            start = start.Parent;
        return start;
    }
}
