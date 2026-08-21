namespace Broiler.Wpt.Tests;

/// <summary>
/// Serializes the classes that toggle the process-wide <see cref="WptTestRunner.DomRender"/>
/// lever around a render or a script pass. It selects which form
/// <c>WptTestRunner.ExecuteScriptsWithDom</c> hands back — the serialised HTML, or the
/// <c>DomDocument</c> itself — so a class that sets it can change what another class's
/// concurrently-running test is given back. That is a shared-static race rather than a product
/// bug; membership in this collection makes xUnit run these classes sequentially, the same way
/// <see cref="NativeAnchorWptCollection"/> does for the anchor-placement lever.
/// </summary>
[Xunit.CollectionDefinition("DomRender", DisableParallelization = true)]
public sealed class DomRenderCollection { }
