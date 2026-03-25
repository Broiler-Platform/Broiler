using Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;
using Broiler.JavaScript.Runtime;
using System;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

/// <summary>
/// Factory delegates for creating <c>JSGenerator</c> instances without a
/// direct type reference. Wired by <c>BuiltInsAssemblyInitializer</c>.
/// </summary>
public static class JSGeneratorBuilder
{
    /// <summary>Creates a generator from an element enumerator and a description name.</summary>
    public static Func<IElementEnumerator, string, JSValue> CreateFromEnumerator;

    /// <summary>Creates a generator from a <see cref="ClrGeneratorV2"/> state machine.</summary>
    public static Func<ClrGeneratorV2, JSValue> CreateFromClrV2;
}
