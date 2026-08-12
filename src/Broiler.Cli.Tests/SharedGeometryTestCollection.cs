namespace Broiler.Cli.Tests;

/// <summary>
/// Serializes the RF-BRIDGE-1b shared-geometry tests. Several of them mutate process-wide
/// static flags via try/finally — the nine <c>NativeAnchor*</c>/<c>NativePositionTry*</c>
/// pipeline suites toggle <c>NativeAnchorPlacement.Enabled</c> and
/// <c>ZoomBakeVsEngineEquivalenceTests</c> toggles <c>NativeZoom.Enabled</c> — while others
/// assert geometry values that depend on those flags' defaults. xUnit runs test classes in
/// parallel by default, so without a shared collection a concurrent flag toggle can bleed
/// into a reader and cause spurious failures. Membership in one collection makes them run
/// sequentially.
/// </summary>
[Xunit.CollectionDefinition("SharedGeometryStatics", DisableParallelization = true)]
public sealed class SharedGeometryTestCollection;
