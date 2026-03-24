using System;
using Broiler.JavaScript.Core.Core;

namespace Broiler.JavaScript.Core;

public delegate void JSPromiseDelegate(Action<JSValue> resolve, Action<JSValue> reject);
