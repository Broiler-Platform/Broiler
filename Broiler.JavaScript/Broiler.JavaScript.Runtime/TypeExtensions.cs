using System;
using System.Linq;
using System.Reflection;

namespace Broiler.JavaScript.Runtime;

public static class TypeExtensions
{
    public static bool IsJSValueType(this Type type) => typeof(JSValue).IsAssignableFrom(type);

    public static bool HasAttribute<T>(this MemberInfo member, out T value) where T : Attribute
    {
        var a = member.GetCustomAttribute<T>();
        if (a == null)
        {
            value = default;
            return false;
        }

        value = a;
        return true;
    }

    public static bool IsIndexProperty(this PropertyInfo property) => property.GetMethod?.GetParameters()?.Length > 0;

    public static Type GetElementTypeOrGeneric(this Type type)
    {
        if (type.IsArray && type.HasElementType)
        {
            var et = type.GetElementType();
            return et != typeof(object) ? et : null;
        }

        if (type.IsConstructedGenericType)
            return type.GetGenericArguments()[0];

        return null;
    }

    public static PropertyInfo Property(this Type type, string name)
    {
        var a = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        return a == null ? throw new NullReferenceException($"Property {name} not found on {type.FullName}") : a;
    }

    public static FieldInfo PublicField(this Type type, string name)
    {
        var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
        return f == null ? throw new NullReferenceException($"Field {name} not found on {type.FullName}") : f;
    }


    public static FieldInfo InternalField(this Type type, string name)
    {
        var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
        return f == null ? throw new NullReferenceException($"Field {name} not found on {type.FullName}") : f;
    }

    public static PropertyInfo PublicIndex(this Type type, params Type[] types)
    {
        var px = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .FirstOrDefault(x => x.GetIndexParameters().Length > 0 && x.GetIndexParameters().Select(p => p.ParameterType).SequenceEqual(types));

        if (px == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Property this({tl}) not found on {type.FullName}");
        }

        return px;
    }

    public static PropertyInfo IndexProperty(this Type type, params Type[] types)
    {
        var px = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .FirstOrDefault(x => x.GetIndexParameters().Length > 0 && x.GetIndexParameters().Select(p => p.ParameterType).SequenceEqual(types));

        if (px == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Property this({tl}) not found on {type.FullName}");
        }

        return px;
    }

    public static MethodInfo PublicMethod(this Type type, string name, params Type[] types)
    {
        var m = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance, null, types, null);
        if (m == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Method {name}({tl}) not found on {type.FullName}");
        }

        return m;
    }

    public static MethodInfo InternalMethod(this Type type, string name, params Type[] types)
    {
        var m = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance, null, types, null);
        if (m == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Method {name}({tl}) not found on {type.FullName}");
        }

        return m;
    }


    public static MethodInfo StaticMethod(this Type type, string name, params Type[] types)
    {
        var m = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public, null, types, null);
        if (m == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Method {name}({tl}) not found on {type.FullName}");
        }

        return m;
    }

    public static MethodInfo StaticMethod<T1>(this Type type, string name)
    {
        var types = new Type[] { typeof(T1) };
        var m = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public, null, types, null);
        if (m == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Method {name}({tl}) not found on {type.FullName}");
        }

        return m;
    }

    public static MethodInfo StaticMethod<T1, T2>(this Type type, string name)
    {
        var types = new Type[] { typeof(T1), typeof(T2) };
        var m = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public, null, types, null);
        if (m == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Method {name}({tl}) not found on {type.FullName}");
        }

        return m;
    }

    public static MethodInfo StaticMethod<T1, T2, T3>(this Type type, string name)
    {
        var types = new Type[] { typeof(T1), typeof(T2), typeof(T3) };
        var m = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public, null, types, null);
        if (m == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Method {name}({tl}) not found on {type.FullName}");
        }

        return m;
    }

    public static ConstructorInfo PublicConstructor(this Type type, params Type[] types)
    {
        var c = type.GetConstructor(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public, null, types, null);
        if (c == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Constructor {type.Name}({tl}) not found");
        }

        return c;
    }

    public static ConstructorInfo Constructor(this Type type, params Type[] types)
    {
        var c = type.GetConstructor(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, types, null);
        if (c == null)
        {
            var tl = string.Join(",", types.Select(x => x.Name));
            throw new MethodAccessException($"Constructor {type.Name}({tl}) not found");
        }

        return c;
    }

    public static MethodInfo MethodStartsWith(this Type type, string name, params Type[] args)
    {
        var ms = type.GetMethods().Where(x => x.Name == name);
        
        foreach (var m in ms)
        {
            var pl = m.GetParameters();
            if (pl.Length <= args.Length)
                continue;
        
            int i = 0;
            bool found = true;
            
            foreach (var t in args)
            {
                if (pl[i++].ParameterType != t)
                {
                    found = false;
                    break;
                }
            }
            
            if (found)
                return m;
        }
        
        var tl = string.Join(",", args.Select(x => x.Name));
        throw new MethodAccessException($"Method not found {name}");
    }
}
