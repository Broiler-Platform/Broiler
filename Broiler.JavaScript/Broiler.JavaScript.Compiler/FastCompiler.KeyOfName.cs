using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    public YExpression KeyOfName(string name)
    {
        // search for variable...
        if (KeyStringsBuilder.Fields.TryGetValue(name, out var fx))
            return fx;

        var i = _keyStrings.GetOrAdd(name);
        return ScriptInfoBuilder.KeyString(scriptInfo, (int)i);
    }

    public YExpression KeyOfName(in StringSpan name)
    {
        // search for variable...
        if (KeyStringsBuilder.Fields.TryGetValue(name, out var fx))
            return fx;

        var i = _keyStrings.GetOrAdd(name);
        return ScriptInfoBuilder.KeyString(scriptInfo, (int)i);
    }
}
