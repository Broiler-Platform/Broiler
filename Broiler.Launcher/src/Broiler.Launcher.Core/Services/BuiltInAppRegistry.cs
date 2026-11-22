using System;
using System.Collections.Generic;
using Broiler.Launcher.Models;

namespace Broiler.Launcher.Services;

/// <summary>
/// Registry of applications compiled directly into an all-in-one Broiler Office executable.
/// </summary>
public static class BuiltInAppRegistry
{
    private static readonly Dictionary<BroilerAppId, Func<string[], int>> Runners = new();
    private static readonly Dictionary<BroilerAppId, Action<string[]>> WindowOpeners = new();
    private static readonly object Gate = new();

    /// <summary>
    /// Registers an in-process CLI entry point and optional non-modal window opener for a built-in application.
    /// </summary>
    public static void Register(BroilerAppId id, Func<string[], int> runner, Action<string[]>? windowOpener = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        lock (Gate)
        {
            Runners[id] = runner;
            if (windowOpener is not null)
            {
                WindowOpeners[id] = windowOpener;
            }
            else
            {
                WindowOpeners.Remove(id);
            }
        }
    }

    /// <summary>Gets a snapshot of all currently registered built-in application runners.</summary>
    public static IReadOnlyDictionary<BroilerAppId, Func<string[], int>> Snapshot()
    {
        lock (Gate)
        {
            return new Dictionary<BroilerAppId, Func<string[], int>>(Runners);
        }
    }

    /// <summary>Gets a snapshot of all currently registered in-process non-modal window openers.</summary>
    public static IReadOnlyDictionary<BroilerAppId, Action<string[]>> SnapshotWindowOpeners()
    {
        lock (Gate)
        {
            return new Dictionary<BroilerAppId, Action<string[]>>(WindowOpeners);
        }
    }

    /// <summary>Attempts to retrieve the in-process runner for <paramref name="id"/>.</summary>
    public static bool TryGetRunner(BroilerAppId id, out Func<string[], int> runner)
    {
        lock (Gate)
        {
            return Runners.TryGetValue(id, out runner!);
        }
    }

    /// <summary>Attempts to retrieve the non-modal window opener for <paramref name="id"/>.</summary>
    public static bool TryGetWindowOpener(BroilerAppId id, out Action<string[]> opener)
    {
        lock (Gate)
        {
            return WindowOpeners.TryGetValue(id, out opener!);
        }
    }

    /// <summary>Clears all registrations (intended for isolated unit tests).</summary>
    internal static void Clear()
    {
        lock (Gate)
        {
            Runners.Clear();
            WindowOpeners.Clear();
        }
    }
}
