namespace YantraJS.Core;

/// <summary>
/// Default implementation of <see cref="IBuiltInRegistry"/> that delegates
/// to the source-generated <c>Names.RegisterGeneratedClasses</c> method.
/// This preserves the existing registration behaviour where every built-in
/// object decorated with <c>[JSFunctionGenerator]</c> /
/// <c>[JSClassGenerator]</c> is registered automatically.
/// </summary>
public sealed class DefaultBuiltInRegistry : IBuiltInRegistry
{
    /// <summary>
    /// Shared singleton instance — the default registry is stateless.
    /// </summary>
    public static readonly DefaultBuiltInRegistry Instance = new();

    /// <inheritdoc />
    public void Register(JSContext context)
    {
        context.RegisterGeneratedClasses();
    }
}
