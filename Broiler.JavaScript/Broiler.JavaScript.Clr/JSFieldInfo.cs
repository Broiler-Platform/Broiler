using Broiler.JavaScript.ExpressionCompiler.Runtime;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using System.Reflection;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.LinqExpressions.Utils;

namespace Broiler.JavaScript.Clr;

internal readonly struct JSFieldInfo
{
    private readonly FieldInfo field;

    public readonly string Name;
    public readonly bool Export;

    public JSFieldInfo(ClrMemberNamingConvention namingConvention, FieldInfo field)
    {
        this.field = field;
        var (name, export) = ClrTypeExtensions.GetJSName(namingConvention, field);
        Name = name;
        Export = export;
    }

    public JSFunction GenerateFieldGetter()
    {
        var name = $"get {Name}";
        var field = this.field;
        return new JSFunction(() =>
        {
            var args = YExpression.Parameter(typeof(Arguments).MakeByRefType());
            YExpression convertedThis = field.IsStatic ? null : JSValueToClrConverter.Get(ArgumentsBuilder.This(args), field.DeclaringType, "this");

            var body = ClrProxyBuilder.Marshal(YExpression.Field(convertedThis, field));
            var lambda = YExpression.Lambda<JSFunctionDelegate>(name, body, args);

            return lambda.Compile();
        }, name);
    }

    public JSFunction GenerateFieldSetter()
    {
        var name = $"set {Name}";
        var field = this.field;
        return new JSFunction(() =>
        {
            var args = YExpression.Parameter(typeof(Arguments).MakeByRefType());
            var a1 = ArgumentsBuilder.Get1(args);
            var convert = field.IsStatic ? null : JSValueToClrConverter.Get(ArgumentsBuilder.This(args), field.DeclaringType, "this");

            var clrArg1 = JSValueToClrConverter.Get(a1, field.FieldType, "value");
            var fieldExp = YExpression.Field(convert, field);

            // todo
            // not working for `char`
            var assign = YExpression.Assign(fieldExp, clrArg1).ToJSValue();

            var body = assign;
            var lambda = YExpression.Lambda<JSFunctionDelegate>(name, body, args);

            return lambda.Compile();
        }, name);
    }
}
