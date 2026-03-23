using System.Runtime.CompilerServices;

#if !WEB_ATOMS
[assembly: InternalsVisibleTo("Broiler.JavaScript.Core.Tests")]

// used by Dynamic Assembly to access internals
[assembly: InternalsVisibleTo("Broiler.JavaScript.Runtime")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.BuiltIns")]
[assembly: InternalsVisibleTo("WebAtoms.XF")]
#endif
