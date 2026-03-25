using System;

namespace Broiler.JavaScript.Runtime;

public delegate void JSPromiseDelegate(Action<JSValue> resolve, Action<JSValue> reject);
