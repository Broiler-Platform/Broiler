using System.Collections.Generic;
using System.Linq.Expressions;
using YantraJS.Core;
using YantraJS.Expressions;

namespace YantraJS.Converters;


public partial class LinqConverter: LinqMap<YExpression>
{
    private Dictionary<ParameterExpression, YParameterExpression> parameters
        = [];

    private LabelMap labels
        = new();

    private IFastEnumerable<YParameterExpression> Register(IList<ParameterExpression> plist)
    {
        var list = new Sequence<YParameterExpression>();
        foreach (var p in plist)
        {
            var t = p.IsByRef && !p.Type.IsByRef ? p.Type.MakeByRefType() : p.Type;
            var yp = YExpression.Parameter(t, p.Name);
            parameters[p] = yp;
            list.Add(yp);
        }
        return list;
    }
}
