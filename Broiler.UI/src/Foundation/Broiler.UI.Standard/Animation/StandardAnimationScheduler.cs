using System;
using System.Collections.Generic;

namespace Broiler.UI.Standard;

public sealed class StandardAnimationScheduler
{
    private readonly UiSession _session;
    private readonly List<AnimationRegistration> _registrations = [];

    public StandardAnimationScheduler(UiSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public int Count => _registrations.Count;

    public IDisposable Register(TimeSpan interval, Action<UiTimestamp> callback)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval));
        ArgumentNullException.ThrowIfNull(callback);

        var registration = new AnimationRegistration(this, interval, _session.Clock.Now, callback);
        _registrations.Add(registration);
        return registration;
    }

    public int Tick()
    {
        UiTimestamp now = _session.Clock.Now;
        int invoked = 0;
        foreach (AnimationRegistration registration in _registrations.ToArray())
        {
            if (registration.IsDisposed)
                continue;

            if (now.Elapsed - registration.LastTick.Elapsed >= registration.Interval)
            {
                registration.LastTick = now;
                registration.Callback(now);
                invoked++;
            }
        }

        _registrations.RemoveAll(static registration => registration.IsDisposed);
        return invoked;
    }

    private void Remove(AnimationRegistration registration) => registration.IsDisposed = true;

    private sealed class AnimationRegistration : IDisposable
    {
        private readonly StandardAnimationScheduler _owner;

        public AnimationRegistration(
            StandardAnimationScheduler owner,
            TimeSpan interval,
            UiTimestamp lastTick,
            Action<UiTimestamp> callback)
        {
            _owner = owner;
            Interval = interval;
            LastTick = lastTick;
            Callback = callback;
        }

        public TimeSpan Interval { get; }

        public UiTimestamp LastTick { get; set; }

        public Action<UiTimestamp> Callback { get; }

        public bool IsDisposed { get; set; }

        public void Dispose() => _owner.Remove(this);
    }
}

