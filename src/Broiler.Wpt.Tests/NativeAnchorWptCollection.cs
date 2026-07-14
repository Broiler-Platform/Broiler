namespace Broiler.Wpt.Tests;

/// <summary>
/// Serializes the native anchor-placement WPT render tests. They all toggle the
/// process-wide <see cref="WptTestRunner.NativeAnchorPlacement"/> static (and the
/// <c>[ThreadStatic]</c> engine lever) around a render, so running the classes in parallel
/// lets one class's baked (lever-off) render stomp another's native render — a shared-static
/// race, not a product bug. Membership in this collection makes xUnit run them sequentially.
/// </summary>
[Xunit.CollectionDefinition("NativeAnchorWpt", DisableParallelization = true)]
public sealed class NativeAnchorWptCollection { }
