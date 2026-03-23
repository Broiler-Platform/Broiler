using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

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
