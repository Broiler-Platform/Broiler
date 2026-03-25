using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Broiler.JavaScript.JSClassGenerator;

internal class RegistrationGenerator(
    ITypeSymbol type,
    JSGeneratorContext gc)
{
    internal static string GenerateNames(ITypeSymbol type, JSGeneratorContext gc)
    {
        var rg = new RegistrationGenerator(type, gc);
        return rg.GenerateNames();
    }

    private List<string> names => gc.Names;

    private string GenerateNames()
    {
        var sb = new StringBuilder();

        sb = sb.AppendLine("using System.Collections.Generic;")
            .AppendLine("using System.Runtime.CompilerServices;")
            .AppendLine("using Broiler.JavaScript.Runtime;")
            .AppendLine("using Broiler.JavaScript.Storage;")
                            .AppendLine("using Broiler.JavaScript.Engine.Core;")


            .AppendLine("using System.Text;");

        var ns = type.ContainingNamespace.ToString();

        sb = sb.AppendLine($"namespace {ns} {{ ");

        sb = sb.AppendLine($"partial class {type.Name} {{");

        foreach (var name in names)
        {
            if (type.GetMembers(name).Any(x => x.Name == name))
                continue;
            sb.AppendLine($"public static readonly KeyString {name};");
        }

        sb.AppendLine($"static {type.Name}() {{");
        foreach (var name in names)
        {
            if (name.StartsWith("@"))
            {
                sb.AppendLine($"{type.Name}.{name} = \"{name.Substring(1)}\";");
                continue;
            }
            sb.AppendLine($"{type.Name}.{name} = \"{name}\";");
        }
        sb.AppendLine("}");

        // add registerall
        sb.AppendLine("static private void RegisterAll(JSObject context) {");

        foreach(var type in gc.RegistrationOrder)
        {
            if (type.Register)
            {
                sb.AppendLine($"{type.ContainingNamespace}.{type.ClrClassName}.CreateClass(context);");
            }
        }

        sb.AppendLine("}");

        sb.AppendLine("}");
        sb.AppendLine("}");


        return sb.ToString();
    }
}
