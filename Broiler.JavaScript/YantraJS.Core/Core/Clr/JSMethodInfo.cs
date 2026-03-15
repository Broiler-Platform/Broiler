using System.Reflection;
using System.ComponentModel;
using YantraJS.Core.Clr;

namespace YantraJS.Core.Core.Clr;

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
    public JSFunction ToInstanceJSFunctionDelegate<T>() => new(Method.CompileToJSFunctionDelegate(), Name);//if (Method.IsStatic)//{//    var staticDel = (JSFunctionDelegate)Method.CreateDelegate(typeof(JSFunctionDelegate));//    return new JSFunction((in Arguments a) =>//    {//        return staticDel(a);//    }, Name);//}//var del = (InstanceDelegate<T>)Method.CreateDelegate(typeof(InstanceDelegate<T>));//var type = typeof(T);//return new JSFunction((in Arguments a) =>//{//    var @this = (T)a.This.ForceConvert(type);//    return del(@this, a);//}, Name);

    public JSFunctionDelegate GenerateMethod() => Method.CompileToJSFunctionDelegate();

}
