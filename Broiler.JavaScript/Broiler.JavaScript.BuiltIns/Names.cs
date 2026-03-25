using Broiler.JavaScript.Engine;
using Broiler.JavaScript.ExpressionCompiler;

namespace Broiler.JavaScript.BuiltIns;

/// <summary>
/// Source-generated registration class for built-in types in the BuiltIns assembly.
/// The <c>[JSRegistrationGenerator]</c> attribute causes the Roslyn source generator
/// to produce <c>KeyString</c> constants and a <c>RegisterAll</c> method for all
/// types decorated with <c>[JSClassGenerator]</c> / <c>[JSFunctionGenerator]</c>
/// in this assembly.
/// </summary>
/// <remarks>
/// This class is named <c>Names</c> (same as the Core assembly's class) because the
/// <c>JSClassGenerator</c> hard-codes references to <c>Names.{propertyName}</c> in
/// generated partial class code. The two <c>Names</c> classes do not conflict because
/// Core's version is <c>internal</c> and the BuiltIns assembly does not use
/// <c>InternalsVisibleTo</c> from Core, so only this local definition is visible.
/// </remarks>
[JSRegistrationGenerator]
internal static partial class Names
{
    /// <summary>
    /// Registers all built-in types defined in the BuiltIns assembly
    /// into the given <see cref="JSContext"/>.
    /// </summary>
    public static void RegisterBuiltInClasses(this JSContext context) => RegisterAll(context);
}
