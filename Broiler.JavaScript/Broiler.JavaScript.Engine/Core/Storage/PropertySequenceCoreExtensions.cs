using System.Runtime.CompilerServices;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Engine.Core.Storage;

/// <summary>
/// Initializes <see cref="PropertySequence"/> delegates that require Core
/// runtime types (<see cref="JSEngine"/>). The <c>Put</c> extension method
/// for <see cref="JSFunctionDelegate"/>-based getters/setters has been moved
/// to <see cref="PropertySequenceRuntimeExtensions"/> in the Runtime assembly.
/// </summary>
public static class PropertySequenceCoreExtensions
{
    /// <summary>
    /// Initializes the <see cref="PropertySequence.TypeErrorFactory"/> delegate
    /// so that property deletion errors produce the correct JavaScript TypeError
    /// exception. Called during Core assembly initialization.
    /// </summary>
    [ModuleInitializer]
    internal static void InitializeTypeErrorFactory()
    {
        PropertySequence.TypeErrorFactory = msg => JSEngine.NewTypeError(msg);
    }
}
