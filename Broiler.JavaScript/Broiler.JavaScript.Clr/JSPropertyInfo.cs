using Broiler.JavaScript.ExpressionCompiler.Runtime;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

using System;
using System.Reflection;
using Broiler.JavaScript.BuiltIns.Function;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.LinqExpressions.Utils;

namespace Broiler.JavaScript.Clr;


internal class JSPropertyInfo
{
    public readonly PropertyInfo Property;
    public readonly string Name;
    public readonly bool Export;

    public readonly Type PropertyType;
    public readonly MethodInfo GetMethod;
    public readonly MethodInfo SetMethod;

    public readonly bool CanRead;
    public readonly bool CanWrite;

    public JSPropertyInfo(ClrMemberNamingConvention namingConvention, PropertyInfo property)
    {
        Property = property;
        var (name, export) = ClrTypeExtensions.GetJSName(namingConvention, property);
        Name = name;
        Export = export;
        PropertyType = property.PropertyType;
        GetMethod = property.GetMethod;
        SetMethod = property.SetMethod;
        CanRead = property.CanRead;
        CanWrite = property.CanWrite;
    }

    public JSFunction GeneratePropertyGetter()
    {
        var name = $"get {Name}";
        return new JSFunction(Property.GetMethod.CompileToJSFunctionDelegate(name), name);
    }

    public JSFunction GeneratePropertySetter()
    {
        var name = $"set {Name}";
        return new JSFunction(Property.SetMethod.CompileToJSFunctionDelegate(name), name);
    }

    internal Func<object, uint, JSValue> GenerateIndexedGetter()
    {
        var @this = YExpression.Parameter(typeof(object));
        var index = YExpression.Parameter(typeof(uint));
        var indexParameter = Property.GetMethod.GetParameters()[0];

        YExpression indexAccess = index.Type != indexParameter.ParameterType ? YExpression.Convert(index, indexParameter.ParameterType) : index;
        YExpression indexExpression;
        YExpression convertThis = YExpression.TypeAs(@this, Property.DeclaringType);

        if (Property.DeclaringType.IsArray)
        {
            // this is direct array.. cast and get.. 
            indexExpression = YExpression.ArrayIndex(convertThis, indexAccess);
        }
        else
        {
            indexExpression = YExpression.MakeIndex(convertThis, Property, [indexAccess]);
        }

        YExpression body = JSExceptionBuilder.Wrap(ClrProxyBuilder.Marshal(indexExpression));
        var lambda = YExpression.Lambda<Func<object, uint, JSValue>>($"set {Property.Name}", body, @this, index);

        return lambda.Compile();
    }

    internal Func<object, uint, object, JSValue> GenerateIndexedSetter()
    {
        if (!Property.CanWrite)
            return null;

        var type = Property.DeclaringType;
        var elementType = type.GetElementTypeOrGeneric() ?? Property.PropertyType;

        var @this = YExpression.Parameter(typeof(object));
        var index = YExpression.Parameter(typeof(uint));
        var value = YExpression.Parameter(typeof(object));
        var indexParameter = Property.SetMethod.GetParameters()[0];

        YExpression indexAccess = index.Type != indexParameter.ParameterType ? YExpression.Convert(index, indexParameter.ParameterType) : index;
        YExpression indexExpression;
        YExpression convertThis = YExpression.TypeAs(@this, Property.DeclaringType);

        if (Property.DeclaringType.IsArray)
        {
            // this is direct array.. cast and get.. 
            indexExpression = YExpression.ArrayIndex(convertThis, indexAccess);
        }
        else
        {
            indexExpression = YExpression.MakeIndex(convertThis, Property, [indexAccess]);
        }

        YExpression body = YExpression.Block(JSExceptionBuilder.Wrap(YExpression.Assign(indexExpression, YExpression.TypeAs(value, elementType)).ToJSValue()));
        var lambda = YExpression.Lambda<Func<object, uint, object, JSValue>>("get " + Property.Name, body, @this, index, value);
        
        return lambda.Compile();
    }
}
