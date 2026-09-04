using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.Code.Core.Review;

/// <summary>
/// Runs one short, read-only <c>git</c> command and returns its trimmed standard
/// output, or null.
///
/// It exists so the two things that ask git something — the revision a review is
/// recorded at, and the name it is attributed to — do so identically and in one
/// place. They were written twice, once per platform head, which put git inside
/// a head against the rule that a head carries rendering, input and capability
/// adapters only, and meant a fix to one copy silently missed the other.
///
/// Every failure is null, and null is an ordinary answer here: a workspace that
/// is not a repository, a host with no git, a scratch directory. Nothing about
/// the review record depends on git being present.
/// </summary>
internal static class GitCommand
{
    /// <summary>
    /// How long git gets. A repository on a cold network share can take seconds
    /// to answer, and a review pane that stalls while somebody marks a file
    /// reviewed is worse than one that records no revision.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static async ValueTask<string?> RunAsync(
        string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        Process? process = null;
        try
        {
            process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
                return null;

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(Timeout);

            // Both pipes are drained, and concurrently. Redirecting a stream and
            // then not reading it is the classic way to deadlock a child
            // process: git fills the pipe buffer, blocks writing to it, and
            // never reaches the exit this would otherwise be waiting for. It is
            // stderr that fills here, because that is where git puts the advice
            // it prints when something is wrong.
            Task<string> output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            Task<string> errors = process.StandardError.ReadToEndAsync(deadline.Token);

            await Task.WhenAll(output, errors).ConfigureAwait(false);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);

            return process.ExitCode == 0 ? output.Result.Trim() : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own deadline, not the caller's. The caller's cancellation is
            // deliberately allowed to propagate: a cancelled review action should
            // report itself as cancelled, not quietly record no revision.
            return null;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException
                or PlatformNotSupportedException or ObjectDisposedException)
        {
            // git absent, or the platform has no process API. Both mean the same
            // thing to a caller: no answer.
            return null;
        }
        finally
        {
            // Disposing a Process closes the handle; it does not stop the child.
            // A git that timed out is still running, still holding the pipes it
            // blocked on, and would otherwise be left behind once per attempt.
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or System.ComponentModel.Win32Exception
                        or NotSupportedException)
                {
                    // It exited between the check and the kill, or the platform
                    // will not let us. Neither is worth reporting from a lookup
                    // whose whole failure mode is "no answer".
                }

                process.Dispose();
            }
        }
    }
}

/// <summary>
/// Who a recorded review is attributed to.
///
/// The repository's own <c>user.name</c> first, because that is the identity the
/// resulting commit will carry, and a record whose reviewer disagrees with its
/// committer invites exactly the question the record exists to settle. The
/// account name is the fallback, and it is a fallback rather than the default
/// because two people on one machine share it.
/// </summary>
public static class GitIdentity
{
    public static async ValueTask<string> ResolveReviewerAsync(
        string workingDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);

        string? name = await GitCommand
            .RunAsync(workingDirectory, "config user.name", cancellationToken)
            .ConfigureAwait(false);

        return name is { Length: > 0 } ? name : Environment.UserName;
    }
}
