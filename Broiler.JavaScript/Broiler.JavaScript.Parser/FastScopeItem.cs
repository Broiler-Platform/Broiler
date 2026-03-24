using System.Collections.Generic;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Parser;

public partial class FastScopeItem(FastNodeType nodeType) : LinkedStackItem<FastScopeItem>
{
    private Dictionary<string, (StringSpan name, FastVariableKind kind)> Variables = new();
    public readonly FastNodeType NodeType = nodeType;

    public void AddVariable(FastToken token, in StringSpan name, FastVariableKind kind = FastVariableKind.Var, bool throwError = true)
    {
        if (name.IsNullOrWhiteSpace())
            return;

        var n = this;

        while (n != null)
        {
            if (n.Variables.TryGetValue(name.Value, out var pn))
            {
                if (pn.kind != FastVariableKind.Var)
                {
                    if (throwError)
                    {
                        throw new FastParseException(token, $"{name} is already defined in current scope at {token.Start}");
                    }
                    return;
                }
            }

            break;
        }

        n = this;

        // all `var` variables must be hoisted to
        // to top most scope
        if (kind == FastVariableKind.Var)
        {
            // in case of var...
            // find the top most declaration... if exists..
            var it = n;

            while (it != null)
            {
                if (it.Variables.TryGetValue(name.Value, out var v))
                    return;

                it = it.Parent;
            }

            while (true)
            {
                if (n.Parent == null)
                    break;

                if (n.NodeType == FastNodeType.Block && n.Parent.NodeType == FastNodeType.Block)
                {
                    n = n.Parent;
                    continue;
                }

                break;
            }
        }

        n.Variables[name.Value] = (name, kind);
    }

    public IFastEnumerable<StringSpan> GetVariables()
    {
        var list = new Sequence<StringSpan>();

        foreach (var (_, Value) in Variables)
            list.Add(Value.name);

        if (list.Count == 0)
            return Sequence<StringSpan>.Empty;

        return list;
    }
}
