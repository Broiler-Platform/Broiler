using System;
using System.Collections.Generic;
using System.Text;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;

namespace YantraJS.Utils
{
    public class YantraConsole
    {

        public static JSValue Log(in Arguments a)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a.TryGetAt(i, out var ai))
                {
                    Console.Write(ai);
                    (JSEngine.CurrentContext as JSContext)?.ReportLog(ai);
                }
            }
            Console.WriteLine();
            return JSUndefined.Value;
        }

    }
}
