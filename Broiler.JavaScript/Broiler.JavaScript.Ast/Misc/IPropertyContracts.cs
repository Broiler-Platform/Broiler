namespace Broiler.JavaScript.Ast.Misc;

/// <summary>
/// Marker interface for types that can be stored as a property value
/// inside <c>JSProperty</c>.  Implemented by <c>JSValue</c> in the
/// Core assembly so that the Storage assembly can reference property
/// values without a direct dependency on the runtime type system.
/// </summary>
public interface IPropertyValue { }

/// <summary>
/// Marker interface for types that can act as a property getter or
/// setter inside <c>JSProperty</c>.  Implemented by <c>JSFunction</c>
/// in the Core assembly.  Extends <see cref="IPropertyValue"/> because
/// every accessor is also a valid property value.
/// </summary>
public interface IPropertyAccessor : IPropertyValue { }
