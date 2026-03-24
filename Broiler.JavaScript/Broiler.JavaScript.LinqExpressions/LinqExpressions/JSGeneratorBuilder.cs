using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LinqExpressions.GeneratorsV2;
using System;

namespace Broiler.JavaScript.Core.LinqExpressions;

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
