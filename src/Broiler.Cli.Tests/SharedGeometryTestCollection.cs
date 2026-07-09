namespace Broiler.Cli.Tests;

/// <summary>
/// Serializes the RF-BRIDGE-1b shared-geometry tests. Several of them mutate process-wide
/// static flags (<c>DomBridge.UseSharedLayoutGeometry</c>,
/// <c>DomBridge.UseSharedGeometryExclusively</c>) via try/finally, while others assert
/// geometry values that depend on those flags' defaults. xUnit runs test classes in
/// parallel by default, so without a shared collection a concurrent flag toggle can bleed
/// into a reader and cause spurious failures. Membership in one collection makes them run
/// sequentially.
/// </summary>
[Xunit.CollectionDefinition("SharedGeometryStatics", DisableParallelization = true)]
public sealed class SharedGeometryTestCollection;
