#nullable enable
using System;
using System.Reflection;
using YantraJS.Core;
using YantraJS.Core.Clr;

namespace Yantra.Core;

public class GlobalAttribute(string? name = null) : Attribute
{
    public readonly string? Name = name;
}

public static class GlobalAttributeExtensions
{
    public static void RegisterTypes(this JSContext context, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            var ga = type.GetCustomAttribute<GlobalAttribute>();
            if (ga == null)
            {
                continue;
            }
            context[ga.Name ?? type.Name] = ClrType.From(type);
        }
    }
}
