using System;
using System.Collections.Generic;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Extensions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSObjectBuilder
{
    readonly static Type type = typeof(JSObject);
    readonly static Type typeExtensions = typeof(JSObjectExtensions);
    readonly static ConstructorInfo _New = type.Constructor();

    readonly static MethodInfo _FastAddSetterUInt =
        typeExtensions.PublicMethod(nameof(JSObjectExtensions.FastAddSetter), typeof(JSObject), typeof(uint), typeof(JSFunction), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddGetterUInt =
        typeExtensions.PublicMethod(nameof(JSObjectExtensions.FastAddGetter), typeof(JSObject), typeof(uint), typeof(JSFunction), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddSetterKeyString =
        typeExtensions.PublicMethod(nameof(JSObjectExtensions.FastAddSetter), typeof(JSObject), typeof(KeyString), typeof(JSFunction), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddGetterKeyString =
        typeExtensions.PublicMethod(nameof(JSObjectExtensions.FastAddGetter), typeof(JSObject), typeof(KeyString), typeof(JSFunction), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddSetterValue =
        typeExtensions.PublicMethod(nameof(JSObjectExtensions.FastAddSetter), typeof(JSObject), typeof(JSValue), typeof(JSFunction), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddGetterValue =
        typeExtensions.PublicMethod(nameof(JSObjectExtensions.FastAddGetter), typeof(JSObject), typeof(JSValue), typeof(JSFunction), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddValueUInt =
        type.PublicMethod(nameof(JSObject.FastAddValue), typeof(uint), typeof(JSValue), typeof(JSPropertyAttributes));

    public readonly static MethodInfo _FastAddValueKeyString =
        type.PublicMethod(nameof(JSObject.FastAddValue), typeof(KeyString), typeof(JSValue), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddValueKeySymbol =
        type.PublicMethod(nameof(JSObject.FastAddValue), typeof(IJSSymbol), typeof(JSValue), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddValueKeyValue =
        type.PublicMethod(nameof(JSObject.FastAddValue), typeof(JSValue), typeof(JSValue), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddPropertyUInt =
        type.PublicMethod(nameof(JSObject.FastAddProperty), typeof(uint), typeof(JSFunction), typeof(JSFunction), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddPropertyKeyString =
        type.PublicMethod(nameof(JSObject.FastAddProperty), typeof(KeyString), typeof(JSFunction), typeof(JSFunction), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddPropertySymbol =
        type.PublicMethod(nameof(JSObject.FastAddProperty), typeof(IJSSymbol), typeof(JSFunction), typeof(JSFunction), typeof(JSPropertyAttributes));

    readonly static MethodInfo _FastAddPropertyValue =
        type.PublicMethod(nameof(JSObject.FastAddProperty), typeof(JSValue), typeof(JSFunction), typeof(JSFunction), typeof(JSPropertyAttributes));

    public readonly static MethodInfo _FastAddRange =
        type.PublicMethod(nameof(JSObject.FastAddRange), typeof(JSValue));

    readonly static MethodInfo _NewWithProperties =
        type.PublicMethod(nameof(JSObject.NewWithProperties));

    readonly static MethodInfo _NewWithElements =
        type.PublicMethod(nameof(JSObject.NewWithElements));

    readonly static MethodInfo _NewWithPropertiesAndElements =
        type.PublicMethod(nameof(JSObject.NewWithPropertiesAndElements));

    public static YElementInit AddValue(Expression key, Expression value, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue)
    {
        if (key.Type.IsJSValueType())
            return new YElementInit(_FastAddValueKeyValue, key, value, Expression.Constant(attributes));

        if (key.Type == typeof(uint))
            return new YElementInit(_FastAddValueUInt, key, value, Expression.Constant(attributes));

        if (key.Type == typeof(int))
            return new YElementInit(_FastAddValueUInt, Expression.Convert(key, typeof(uint)), value, Expression.Constant(attributes));

        return new YElementInit(_FastAddValueKeyString, key, value, Expression.Constant(attributes));
    }

    public static YElementInit AddSetter(Expression key, Expression setter, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty)
    {
        if (key.Type.IsJSValueType())
            return new YElementInit(_FastAddSetterValue, key, setter, Expression.Constant(attributes));

        if (key.Type == typeof(uint))
            return new YElementInit(_FastAddSetterUInt, key, setter, Expression.Constant(attributes));

        if (key.Type == typeof(int))
            return new YElementInit(_FastAddSetterUInt, Expression.Convert(key, typeof(uint)), setter, Expression.Constant(attributes));

        return new YElementInit(_FastAddSetterKeyString, key, setter, Expression.Constant(attributes));
    }

    public static YElementInit AddGetter(Expression key, Expression getter, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty)
    {
        if (key.Type.IsJSValueType())
            return new YElementInit(_FastAddGetterValue, key, getter, Expression.Constant(attributes));

        if (key.Type == typeof(uint))
            return new YElementInit(_FastAddGetterUInt, key, getter, Expression.Constant(attributes));

        if (key.Type == typeof(int))
            return new YElementInit(_FastAddGetterUInt, Expression.Convert(key, typeof(uint)), getter, Expression.Constant(attributes));

        return new YElementInit(_FastAddGetterKeyString, key, getter, Expression.Constant(attributes));
    }

    public static Expression New() => Expression.New(_New);

    public static Expression New(IFastEnumerable<YElementInit> elements) => Expression.ListInit(Expression.New(_New), elements);

    public static Expression New(IList<ExpressionHolder> keyValues)
    {
        var list = new Sequence<YElementInit>();

        foreach (var v in keyValues)
        {
            if (v.Spread)
            {
                list.Add(Expression.ElementInit(_FastAddRange, v.Value));
                continue;
            }

            if (v.Key.Type == typeof(uint))
            {
                if (v.Value != null)
                {
                    list.Add(Expression.ElementInit(_FastAddValueUInt, v.Key, v.Value, JSPropertyAttributesBuilder.EnumerableConfigurableValue));
                    continue;
                }

                list.Add(Expression.ElementInit(_FastAddPropertyUInt, v.Key, v.Getter, v.Setter, JSPropertyAttributesBuilder.EnumerableConfigurableProperty));
                continue;
            }

            if (v.Key.Type == typeof(KeyString))
            {
                if (v.Value != null)
                {
                    list.Add(Expression.ElementInit(_FastAddValueKeyString, v.Key, v.Value, JSPropertyAttributesBuilder.EnumerableConfigurableValue));
                    continue;
                }

                list.Add(Expression.ElementInit(_FastAddPropertyKeyString, v.Key, v.Getter, v.Setter, JSPropertyAttributesBuilder.EnumerableConfigurableProperty));
                continue;
            }

            if (v.Value != null)
            {
                list.Add(Expression.ElementInit(_FastAddValueKeyValue, v.Key, v.Value, JSPropertyAttributesBuilder.EnumerableConfigurableValue));
                continue;
            }

            list.Add(Expression.ElementInit(_FastAddPropertyValue, v.Key, v.Getter, v.Setter, JSPropertyAttributesBuilder.EnumerableConfigurableProperty));
        }

        return Expression.ListInit(Expression.New(_New), list);
    }
}
