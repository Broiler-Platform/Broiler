using System.Collections.Generic;
using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Converters;

public class LabelMap
{
    private readonly Dictionary<LabelTarget, YLabelTarget> labels = [];

    public YLabelTarget this[LabelTarget label]
    {
        get
        {
            if (labels.TryGetValue(label, out var r))
                return r;

            r = YExpression.Label(label.Name + labels.Count, label.Type);
            labels[label] = r;
            return r;
        }
    }
}
