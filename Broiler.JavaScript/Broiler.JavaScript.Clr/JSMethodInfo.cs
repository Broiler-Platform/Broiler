using System.Reflection;
using System.ComponentModel;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;

namespace Broiler.JavaScript.Clr;

internal class JSMethodInfo
{
    public readonly MethodInfo Method;

    public readonly string Name;
    public readonly bool Export;

    public JSMethodInfo(ClrMemberNamingConvention namingConvention, MethodInfo method)
    {
        Method = method;
        var (name, export) = ClrTypeExtensions.GetJSName(namingConvention, method);
        Name = name;
        Export = export;
    }

    internal JSValue GenerateInvokeJSFunction() => this.InvokeAs(Method.DeclaringType, ToInstanceJSFunctionDelegate<object>);

    public delegate JSValue InstanceDelegate<T>(T @this, in Arguments a);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public JSFunction ToInstanceJSFunctionDelegate<T>() => new(Method.CompileToJSFunctionDelegate(), Name);

    public JSFunctionDelegate GenerateMethod() => Method.CompileToJSFunctionDelegate();

}
