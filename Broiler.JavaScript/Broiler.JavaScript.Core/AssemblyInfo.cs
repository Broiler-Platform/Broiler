using System.Runtime.CompilerServices;

#if !WEB_ATOMS
[assembly: InternalsVisibleTo("Broiler.JavaScript.Tests")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Core.Tests")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Clr")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Compiler")]

// used by Dynamic Assembly to access internals
[assembly: InternalsVisibleTo("Broiler.JavaScript.Runtime")]
[assembly: InternalsVisibleTo("WebAtoms.XF")]
#endif
