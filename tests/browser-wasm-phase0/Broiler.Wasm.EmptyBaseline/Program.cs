using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("Broiler browser WebAssembly Phase 0 empty runtime baseline");
Phase0ReadyMarker.MarkReady(Environment.Version.ToString());
await Task.Delay(Timeout.InfiniteTimeSpan);

internal static partial class Phase0ReadyMarker
{
    [JSImport("baseline.markReady", "main.js")]
    internal static partial void MarkReady(string runtimeVersion);
}
