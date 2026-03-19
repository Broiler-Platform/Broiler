using System.Runtime.CompilerServices;
using Broiler.JavaScript.Storage;

// Type forwarding for types moved to Broiler.JavaScript.Storage assembly.
// These ensure binary compatibility for downstream consumers that reference
// Broiler.JavaScript.Core.

[assembly: TypeForwardedTo(typeof(VirtualMemory<>))]
[assembly: TypeForwardedTo(typeof(VirtualArray))]
[assembly: TypeForwardedTo(typeof(SAUint32Map<>))]
[assembly: TypeForwardedTo(typeof(StringMap<>))]
[assembly: TypeForwardedTo(typeof(HashedString))]
[assembly: TypeForwardedTo(typeof(ConcurrentNameMap))]
[assembly: TypeForwardedTo(typeof(ConcurrentStringMap<>))]
[assembly: TypeForwardedTo(typeof(ConcurrentUInt32Map<>))]
[assembly: TypeForwardedTo(typeof(ConcurrentTypeCache))]
[assembly: TypeForwardedTo(typeof(ConcurrentTypeTrie<>))]
