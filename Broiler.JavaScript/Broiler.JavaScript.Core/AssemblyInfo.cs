using System.Runtime.CompilerServices;

#if !WEB_ATOMS
[assembly: InternalsVisibleTo("Broiler.JavaScript.Tests")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Core.Tests")]

// used by Dynamic Assembly to access internals
[assembly: InternalsVisibleTo("Broiler.JavaScript.Runtime")]
[assembly: InternalsVisibleTo("WebAtoms.XF")]
#endif
