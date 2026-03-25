#nullable enable
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.ExpressionCompiler;

namespace Broiler.JavaScript.Globals;

/// <summary>
/// Source-generated registration class for global types in the Globals assembly.
/// The <c>[JSRegistrationGenerator]</c> attribute causes the Roslyn source generator
/// to produce <c>KeyString</c> constants and a <c>RegisterAll</c> method for all
/// types decorated with <c>[JSClassGenerator]</c> / <c>[JSFunctionGenerator]</c>
/// in this assembly.
/// </summary>
/// <remarks>
/// This class is placed in the <c>Broiler.JavaScript.Core.Core.Global</c> namespace
/// to match JSGlobalStatic's namespace, ensuring the generated code can resolve
/// <c>Names.{propertyName}</c> references. It does not conflict with Core's internal
/// <c>Names</c> class because C# resolves unqualified names in the current namespace first.
/// </remarks>
[JSRegistrationGenerator]
internal static partial class Names
{
    /// <summary>
    /// Registers all global types defined in the Globals assembly
    /// into the given <see cref="JSContext"/>.
    /// </summary>
    public static void RegisterGlobalClasses(this JSContext context) => RegisterAll(context);
}
