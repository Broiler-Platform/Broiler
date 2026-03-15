using YantraJS.Core.LinqExpressions;
using YantraJS.ExpHelper;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    public Expression KeyOfName(string name)
    {
        // search for variable...
        if (KeyStringsBuilder.Fields.TryGetValue(name, out var fx))
            return fx;

        var i = _keyStrings.GetOrAdd(name);
        return ScriptInfoBuilder.KeyString(scriptInfo, (int)i);
    }

    public Expression KeyOfName(in StringSpan name)
    {
        // search for variable...
        if (KeyStringsBuilder.Fields.TryGetValue(name, out var fx))
            return fx;

        var i = _keyStrings.GetOrAdd(name);
        return ScriptInfoBuilder.KeyString(scriptInfo, (int)i);
    }
}
