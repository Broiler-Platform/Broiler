using System.Runtime.CompilerServices;

#if !WEB_ATOMS
[assembly: InternalsVisibleTo("YantraJS.Tests")]
[assembly: InternalsVisibleTo("YantraJS.Core.Tests")]

// used by Dynamic Assembly to access internals
[assembly: InternalsVisibleTo("YantraJS.Runtime")]
[assembly: InternalsVisibleTo("YantraJS.Runtime")]
[assembly: InternalsVisibleTo("WebAtoms.XF")]
#endif
