using Broiler.JavaScript.BuiltIns.Null;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.BuiltIns.Array.Typed;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Json;
using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.HtmlBridge;

public sealed partial class DomBridge
{

    private JSValue JsCssGetPropertyValue001Core(global::System.Collections.Generic.Dictionary<global::System.String, global::System.String>? computed, in Arguments a)
    {
        if (a.Length > 0)
        {
            var name = a[0].ToString();
            if (computed.TryGetValue(name, out var val))
                return new JSString(StripCssPriority(val));
            // Try kebab-case conversion for camelCase input
            var kebab = ToKebabCase(name);
            if (kebab != name && computed.TryGetValue(kebab, out val))
                return new JSString(StripCssPriority(val));
            // Try camelCase conversion for kebab-case input
            var camel = ToCamelCaseStatic(name);
            if (camel != name && computed.TryGetValue(camel, out val))
                return new JSString(StripCssPriority(val));
        }

        return new JSString(string.Empty);
    }


    private JSValue JsCssItem003Core(global::System.Collections.Generic.List<global::System.String>? propertyNames, in Arguments a)
    {
        if (a.Length > 0 && int.TryParse(a[0].ToString(), out var index))
        {
            if (index >= 0 && index < propertyNames.Count)
                return new JSString(propertyNames[index]);
        }

        return new JSString(string.Empty);
    }

}
