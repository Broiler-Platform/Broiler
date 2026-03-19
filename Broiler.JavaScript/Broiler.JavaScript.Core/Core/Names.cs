#nullable enable
using Broiler.JavaScript.ExpressionCompiler;

namespace Broiler.JavaScript.Core.Core;

[JSRegistrationGenerator]
internal static partial class Names
{
    public static void RegisterGeneratedClasses(this JSContext context) => RegisterAll(context);
}
