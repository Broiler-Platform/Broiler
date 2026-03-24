using System;
using System.Reflection;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{
    private Action EmitParameters(MethodBase method, IFastEnumerable<YExpression> args, Type returnType)
    {
        Sequence<(int temp, YExpression exp)>? saveList = null;

        var pa = method.GetParameters();
        for (int i = 0; i < pa.Length; i++)
        {

            var p = pa[i];

            if (i < args.Count)
            {
                var a = args[i];

                if (p.IsOut)
                {
                    if(a.NodeType == YExpressionType.Property)
                    {
                        // BROILER-PATCH: Use element type for byref parameters;
                        // DeclareLocal does not accept byref types directly.
                        var localType = p.ParameterType.IsByRef
                            ? p.ParameterType.GetElementType()
                            : p.ParameterType;
                        var temp = tempVariables[localType];
                        saveList ??= [];
                        saveList.Add((temp.LocalIndex, a));
                        Visit(a);
                        il.EmitSaveLocal(temp.LocalIndex);
                        il.EmitLoadLocalAddress(temp.LocalIndex);
                        continue;
                    }
                    LoadAddress(a);
                    continue;
                }

                if (p.IsIn || p.IsOut) { 
                    if(p.ParameterType.IsValueType)
                    {
                        LoadAddress(a);
                        continue;
                    }
                }

                if (p.ParameterType.IsByRef)
                {
                    LoadAddress(a);
                    continue;
                }

                Visit(a);

                continue;
            }
            if (!p.HasDefaultValue)
                throw new ArgumentException($"Not enough arguments to create object");
            il.EmitConstant(p.RawDefaultValue);
        }

        return Save;

        void Save()
        {
            if (saveList == null)
                return;
            var rtIndex = 0;
            ILWriter.TempVariable t = null;

            if (returnType != typeof(void))
            {
                t = il.NewTemp(returnType);
                rtIndex = t.LocalIndex;

                il.EmitSaveLocal(rtIndex);
            }

            foreach (var (temp, exp) in saveList)
            {
                Assign(exp, temp);
            }

            if (rtIndex != 0)
            {
                il.EmitLoadLocal(rtIndex);
                t.Dispose();
            }
        }
    }

}
