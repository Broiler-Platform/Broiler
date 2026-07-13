using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.Runtime;
using Broiler.CSS;

namespace Broiler.HtmlBridge.Dom;

public sealed partial class DomBridge
{

    private JSValue JsCssGetPropertyValue001Core(Dictionary<string, string>? computed, in Arguments a)
    {
        if (a.Length > 0)
        {
            var name = a[0].ToString();
            if (computed.TryGetValue(name, out var val))
                return new JSString(CssPriority.Strip(val));
            
            // Try kebab-case conversion for camelCase input
            var kebab = CssPropertyNames.ToCssPropertyName(name);
            if (kebab != name && computed.TryGetValue(kebab, out val))
                return new JSString(CssPriority.Strip(val));
            
            // Try camelCase conversion for kebab-case input
            var camel = CssPropertyNames.ToDomPropertyName(name);
            if (camel != name && computed.TryGetValue(camel, out val))
                return new JSString(CssPriority.Strip(val));
        }

        return new JSString(string.Empty);
    }


    private JSValue JsCssItem003Core(List<string>? propertyNames, in Arguments a)
    {
        if (a.Length > 0 && int.TryParse(a[0].ToString(), out var index))
        {
            if (index >= 0 && index < propertyNames.Count)
                return new JSString(propertyNames[index]);
        }

        return new JSString(string.Empty);
    }

}
