using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.JavaScript.Debugger;

public class DelayTask
{
    private readonly Timer timer;
    private readonly CancellationTokenRegistration registration;
    private readonly TaskCompletionSource<bool> completionSource;

    public DelayTask(int timeInMS, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeInMS);

        completionSource = new TaskCompletionSource<bool>();
        timer = new Timer(OnTimer, null, timeInMS, Timeout.Infinite);
        registration = token.Register(Cancel);
    }

    public void Cancel()
    {
        completionSource.TrySetResult(false);
        registration.Dispose();
        
        try
        {
            timer.Dispose();
        }
        catch (Exception ex) 
        {
            Debug.WriteLine($"[Broiler.JavaScript] DelayTask timer dispose error: {ex.Message}"); 
        }
    }

    public void OnTimer(object a)
    {
        completionSource.TrySetResult(true);
        registration.Dispose();
    }

    public static Task<bool> For(TimeSpan timeout, CancellationToken token) => new DelayTask((int)timeout.TotalMilliseconds, token).completionSource.Task;

    public static Task<bool> For(int timeoutMS, CancellationToken token) => new DelayTask(timeoutMS, token).completionSource.Task;
}
