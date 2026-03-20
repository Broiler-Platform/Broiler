using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Extensions;
using Broiler.JavaScript.Core.LinqExpressions;
using System.Reflection;

namespace Broiler.JavaScript.Clr;

internal static class ClrTypeExtensions
{
    public static (string name, bool export) GetJSName(ClrMemberNamingConvention namingConvention, MemberInfo member)
    {
        var export = member.GetCustomAttribute<JSExportAttribute>();

        if (export == null)
            return (namingConvention.Convert(member.Name), false);

        var n = export.Name ?? (export.AsCamel ? member.Name.ToCamelCase() : member.Name);
        return (n, true);
    }

    public static bool IsJSFunctionDelegate(this MethodInfo method)
    {
        var p = method.GetParameters();
        return p.Length == 1 && typeof(JSValue).IsAssignableFrom(method.ReturnType) && p[0].ParameterType == ArgumentsBuilder.refType;
    }
}
