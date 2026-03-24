using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.ClosureSeparator;
using Broiler.JavaScript.ExpressionCompiler.Generator;

namespace Broiler.JavaScript.ExpressionCompiler;

public class LambdaMethodBuilder(MethodBuilder builder) : IMethodBuilder
{
    private readonly TypeBuilder typeBuilder = (TypeBuilder)builder.DeclaringType;

    public YExpression Relay(YExpression @this, IFastEnumerable<YExpression> closures, YLambdaExpression innerLambda)
    {

        var derived = (typeBuilder.Module as ModuleBuilder).DefineType(
            ExpressionCompiler.GetUniqueName(innerLambda.Name + ":" + innerLambda.Name.Line),
            TypeAttributes.Public,
            typeof(Closures));

        var (m, il, exp) = innerLambda.CompileToInstnaceMethod(derived, false);


        var cnstr = derived.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, [
            typeof(Box[])
        ]);

        var boxes = YExpression.Parameter(typeof(Box[]));

        var cnstrLambda = YExpression.Lambda(innerLambda.Type, "cnstr",
            YExpression.CallNew(Closures.constructor, YExpression.Null, boxes, YExpression.Null, YExpression.Null),
            [YExpression.Parameter(derived), boxes]);

        var cnstrIL = new ILCodeGenerator(cnstr.GetILGenerator(), null);
        cnstrIL.EmitConstructor(cnstrLambda);

        var dt = innerLambda.Type;

        var cdt = dt.GetConstructors().First(x => x.GetParameters().Length == 2);

        var cd = typeof(MethodInfo).GetMethod(nameof(MethodInfo.CreateDelegate), [typeof(Type), typeof(object)]);

        var derivedType = derived.CreateTypeInfo();
        var ct = derivedType.GetConstructors()[0];

        var im = derivedType.GetMethods().First(x => x.Name == m.Name);

        return YExpression.New(cdt, YExpression.New(ct, closures == null ? YExpression.Null : YExpression.NewArray(typeof(Box), closures)), YExpression.Constant(im));

    }
}
