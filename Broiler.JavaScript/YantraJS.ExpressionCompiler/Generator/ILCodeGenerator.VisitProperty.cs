using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{

    /// <summary>
    /// 
    /// https://sharplab.io/#gist:5048f7ec17ccf5740862929280bb306f
    /// </summary>
    /// <param name="yPropertyExpression"></param>
    /// <returns></returns>
    protected override CodeInfo VisitProperty(YPropertyExpression yPropertyExpression)
    {
        if (!yPropertyExpression.IsStatic)
        {
            Visit(yPropertyExpression.Target);
        }
        il.EmitCall(yPropertyExpression.GetMethod);
        return true;
    }

}
