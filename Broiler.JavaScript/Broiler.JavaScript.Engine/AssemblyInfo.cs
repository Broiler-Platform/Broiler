using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Broiler.JavaScript.BuiltIns")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Modules")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Globals")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Extensions")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Clr")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Debugger")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.Network")]

#if !WEB_ATOMS
[assembly: InternalsVisibleTo("Broiler.JavaScript.Core.Tests")]

// used by Dynamic Assembly to access internals
[assembly: InternalsVisibleTo("Broiler.JavaScript.Runtime")]
[assembly: InternalsVisibleTo("Broiler.JavaScript.LinqExpressions")]
[assembly: InternalsVisibleTo("WebAtoms.XF")]
#endif
