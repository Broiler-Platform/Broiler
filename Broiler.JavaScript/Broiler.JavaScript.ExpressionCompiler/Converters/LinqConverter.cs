using System.Collections.Generic;
using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Converters;


public partial class LinqConverter : LinqMap<YExpression>
{
    private readonly Dictionary<ParameterExpression, YParameterExpression> parameters = [];
    private readonly LabelMap labels = new();

    private Core.IFastEnumerable<YParameterExpression> Register(IList<ParameterExpression> plist)
    {
        var list = new Core.Sequence<YParameterExpression>();
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
