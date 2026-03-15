using System.Collections.Generic;
using YantraJS.Core;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public class LabelInfo(ILWriter il)
{
    private Dictionary<YLabelTarget, ILWriterLabel> labels = [];

    public ILWriterLabel this[YLabelTarget target] => Create(target);



    private ILWriterLabel Create(YLabelTarget target)
    {
        if (labels.TryGetValue(target, out var l))
            return l;
        l = il.DefineLabel(target.Name);
        labels[target] = l;
        return l;
    }

    public ILWriterLabel Create(YLabelTarget target, ILTryBlock tryBlock, bool throwIfFail = true)
    {
        if (labels.TryGetValue(target, out var l))
        {
            if (throwIfFail)
                throw new System.InvalidOperationException();
            return l;
        }
        l = il.DefineLabel(target.Name, tryBlock);
        labels[target] = l;
        return l;
    }

}
