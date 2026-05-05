using System.Reflection;
using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

public class HtmlBridgeBoundaryGuardTests
{
    private static readonly string[] AllowedDomBridgeEngineInternalLeaks =
    [
        "Method:Attach(JSContext, String)",
        "Method:Attach(JSContext, String, String)",
        "Method:RegisterNamedElementGlobals(JSContext)",
        "Property:DocumentElement",
        "Property:Elements",
    ];

    [Fact]
    public void IScriptEngine_Surface_Does_Not_Expose_Engine_Internal_Types()
    {
        var exposedMembers = typeof(IScriptEngine)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(static member => member.MemberType is MemberTypes.Method or MemberTypes.Property)
            .Where(ExposesEngineInternalType)
            .Select(DescribeMember)
            .OrderBy(static member => member, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(exposedMembers);
    }

    [Fact]
    public void DomBridge_Engine_Internal_Leaks_Are_Frozen_To_The_Known_Compatibility_Surface()
    {
        var exposedMembers = typeof(DomBridge)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(static member => member.DeclaringType == typeof(DomBridge))
            .Where(static member => member.MemberType is MemberTypes.Method or MemberTypes.Property)
            .Where(ExposesEngineInternalType)
            .Select(DescribeMember)
            .OrderBy(static member => member, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedDomBridgeEngineInternalLeaks, exposedMembers);
    }

    private static bool ExposesEngineInternalType(MemberInfo member) => member switch
    {
        MethodInfo method when !method.IsSpecialName =>
            HasEngineInternalType(method.ReturnType) || method.GetParameters().Any(parameter => HasEngineInternalType(parameter.ParameterType)),
        PropertyInfo property =>
            HasEngineInternalType(property.PropertyType),
        _ => false,
    };

    private static bool HasEngineInternalType(Type type)
    {
        if (type == typeof(DomElement))
            return true;

        if (type.Namespace?.StartsWith("Broiler.JavaScript.", StringComparison.Ordinal) == true)
            return true;

        if (type.IsArray)
            return HasEngineInternalType(type.GetElementType()!);

        return type.IsGenericType && type.GetGenericArguments().Any(HasEngineInternalType);
    }

    private static string DescribeMember(MemberInfo member) => member switch
    {
        MethodInfo method => $"Method:{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})",
        PropertyInfo property => $"Property:{property.Name}",
        _ => member.Name,
    };
}
