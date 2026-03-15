#nullable enable
namespace YantraJS.Core.FastParser;

public class AstIfStatement(FastToken start, FastToken end, AstExpression test, AstStatement @true, AstStatement? @false = null) : AstStatement(start, FastNodeType.IfStatement, end)
{
    public readonly AstExpression Test = test;
    public readonly AstStatement True = @true;
    public readonly AstStatement? False = @false;

    public override string ToString()
    {
        if(False!=null) {
            return $"if({Test}) {True} else {False}";
        }
        return $"if({Test}) {True}";
    }
}
