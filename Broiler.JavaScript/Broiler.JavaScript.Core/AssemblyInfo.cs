using System.Runtime.CompilerServices;

#if !WEB_ATOMS
[assembly: InternalsVisibleTo("Broiler.JavaScript.Core.Tests")]

// used by Dynamic Assembly to access internals
[assembly: InternalsVisibleTo("Broiler.JavaScript.Runtime")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.BuiltIns")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Extensions")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Modules")]
[assembly: InternalsVisibleTo("WebAtoms.XF")]
#endif
