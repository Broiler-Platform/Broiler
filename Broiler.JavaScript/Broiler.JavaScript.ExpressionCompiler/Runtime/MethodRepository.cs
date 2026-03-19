using Broiler.JavaScript.ExpressionCompiler.ClosureSeparator;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Broiler.JavaScript.ExpressionCompiler.Runtime;

public class MethodRepository : IMethodRepository
{

    public static ConstructorInfo constructor = typeof(MethodRepository).GetConstructor();

    public string IL;
    public string Exp;

    public class RuntimeMethod
    {
        public DynamicMethod Method;
        public string IL;
        public string Exp;
        public Type Type;
    }

    public ulong RegisterNew(DynamicMethod d, string il, string exp, Type type)
    {
        var x = GCHandle.Alloc(new RuntimeMethod { 
            Method = d,
            IL = il,
            Exp = exp,
            Type = type
        });
        return (ulong)(IntPtr)x;
    }

    public object Create(Box[] boxes, ulong id)
    {
        var rm = GCHandle.FromIntPtr((IntPtr)id).Target as RuntimeMethod;
        var c = new Closures(this, boxes, rm.IL, rm.Exp);
        return rm.Method.CreateDelegate(rm.Type, c);
    }
}
