using System;
using System.Threading;
using System.Threading.Tasks;

namespace YantraJS.Core;

public class DelayTask
{
    private readonly Timer timer;
    private readonly CancellationTokenRegistration registration;
    private readonly TaskCompletionSource<bool> completionSource;

    public DelayTask(int timeInMS, CancellationToken token)
    {
        if (timeInMS <= 0)
            throw new ArgumentOutOfRangeException($"Delay cannot be zero or less");
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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[YantraJS] DelayTask timer dispose error: {ex.Message}"); }
    }

    public void OnTimer(object a)
    {
        completionSource.TrySetResult(true);
        registration.Dispose();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="timeout"></param>
    /// <param name="token"></param>
    /// <returns>true if it was not cancelled</returns>
    public static Task<bool> For(TimeSpan timeout, CancellationToken token) => new DelayTask((int)timeout.TotalMilliseconds, token).completionSource.Task;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="timeoutMS"></param>
    /// <param name="token"></param>
    /// <returns>true if it was not cancelled</returns>
    public static Task<bool> For(int timeoutMS, CancellationToken token) => new DelayTask(timeoutMS, token).completionSource.Task;
}
