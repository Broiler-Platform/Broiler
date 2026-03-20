using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core.Module;

#if !WEB_ATOMS
[assembly: InternalsVisibleTo("Broiler.JavaScript.Core.Tests")]

// used by Dynamic Assembly to access internals
[assembly: InternalsVisibleTo("Broiler.JavaScript.Runtime")]
[assembly: InternalsVisibleTo("WebAtoms.XF")]
#endif

// Type forwards for contracts moved to Broiler.JavaScript.Runtime.
[assembly: TypeForwardedTo(typeof(IJSModuleResolver))]
