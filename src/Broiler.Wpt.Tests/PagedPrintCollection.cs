namespace Broiler.Wpt.Tests;

/// <summary>
/// Serializes the paged-print render tests against the rest of the assembly. They toggle the
/// process-wide <see cref="WptTestRunner.PagedPrint"/> static around a render, and while it is on
/// every file whose name ends in <c>-print</c> renders paged — including one another test class
/// happens to be rendering at that moment. Running this collection alone makes the lever mean what
/// each test says it means.
/// </summary>
[Xunit.CollectionDefinition("PagedPrint", DisableParallelization = true)]
public sealed class PagedPrintCollection { }
