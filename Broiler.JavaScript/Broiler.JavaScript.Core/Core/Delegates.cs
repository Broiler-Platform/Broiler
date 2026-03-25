using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core.Core;

public delegate JSValue JSClosureFunctionDelegate(ScriptInfo script, JSVariable[] closures, in Arguments a);
