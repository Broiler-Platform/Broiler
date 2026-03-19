namespace Broiler.JavaScript.Core.Core;

/// <summary>
/// Defines the contract for registering built-in JavaScript objects
/// (Array, String, Number, Date, Promise, etc.) into a <see cref="JSContext"/>.
/// Implementations allow swapping the set of built-in objects that are
/// available at runtime, enabling isolation and testability.
/// </summary>
public interface IBuiltInRegistry
{
    /// <summary>
    /// Registers built-in objects into the specified context.
    /// Implementations should call <c>CreateClass</c> for each built-in type
    /// that needs to be available in the context.
    /// </summary>
    /// <param name="context">The context to populate with built-in objects.</param>
    void Register(JSContext context);
}
