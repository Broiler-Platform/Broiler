using System;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.JavaScript.ExpressionCompiler;

public class YDispatcher
{
    public static object Queue(object input, Func<object,object> func)
    {
        TaskCompletionSource<object> result = new();
        ThreadPool.QueueUserWorkItem((input) => {
            result.SetResult(func(input));
        }, input);
        return result.Task.GetAwaiter().GetResult();
    }
}

public abstract class StackGuard<T,TIn> {

    private const int MaxStackSize = 1024;


    private int start = 0;

    public unsafe T Visit(TIn input)
    {

        int self;
        int address = (int)&self;
        if (start == 0)
        {
            start = address;
        }
        else
        {
            int diff = address - start;
            if (diff > MaxStackSize)
            {
                var prev = start;
                start = 0;
                var output = (T)YDispatcher.Queue(input, (i) => VisitIn((TIn)i));
                start = prev;
                return output;
            }
        }

        return VisitIn(input);
    }

    public abstract T VisitIn(TIn input);

}
