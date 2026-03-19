using System.Runtime.CompilerServices;
using Broiler.JavaScript.Parser;

// Type forwarding for types moved to Broiler.JavaScript.Parser assembly.
// These ensure binary compatibility for downstream consumers that reference
// Broiler.JavaScript.Core.

[assembly: TypeForwardedTo(typeof(FastParser))]
[assembly: TypeForwardedTo(typeof(FastScanner))]
[assembly: TypeForwardedTo(typeof(FastKeywordMap))]
[assembly: TypeForwardedTo(typeof(FastTokenStream))]
[assembly: TypeForwardedTo(typeof(FastScope))]
[assembly: TypeForwardedTo(typeof(FastScopeItem))]
[assembly: TypeForwardedTo(typeof(IParser))]
[assembly: TypeForwardedTo(typeof(FastPool))]
[assembly: TypeForwardedTo(typeof(FastList<>))]
[assembly: TypeForwardedTo(typeof(FastStack<>))]
[assembly: TypeForwardedTo(typeof(QueueExtensions))]
[assembly: TypeForwardedTo(typeof(CharExtensions))]
