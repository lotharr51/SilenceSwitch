namespace SilenceSwitch;

/// <summary>
/// Pure decision logic: given periodic activity observations, decide when the system
/// has been continuously silent long enough to trigger a device switch.
///
/// No COM, no NAudio, no Timer, no clock dependency that can't be faked — so this is
/// fully unit-testable. The host (a System.Threading.Timer) calls <see cref="Sample"/>
/// each tick with the probe's result and fires the switch when it returns true.
/// </summary>
public sealed class SilenceStateMachine
{
    private readonly TimeSpan _silenceTimeout;
    private readonly Func<DateTime> _now;
    private DateTime? _silentSince;

    /// <param name="silenceTimeout">How long continuous silence must last before triggering.</param>
    /// <param name="clock">Injectable clock for testing. Defaults to UTC now.</param>
    public SilenceStateMachine(TimeSpan silenceTimeout, Func<DateTime>? clock = null)
    {
        if (silenceTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(silenceTimeout));

        _silenceTimeout = silenceTimeout;
        _now = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Record one observation.
    /// Returns true EXACTLY ONCE when the silence threshold is first crossed, so the
    /// caller fires the switch a single time per silence episode rather than every tick.
    /// </summary>
    public bool Sample(bool audioActive)
    {
        var now = _now();

        if (audioActive)
        {
            _silentSince = null;            // any activity resets the silence clock
            return false;
        }

        _silentSince ??= now;               // first silent sample starts the clock

        if (now - _silentSince.Value >= _silenceTimeout)
        {
            _silentSince = null;            // consume the trigger; require fresh silence to re-fire
            return true;
        }

        return false;
    }

    /// <summary>Clear any in-progress silence timing (e.g. after a manual switch).</summary>
    public void Reset() => _silentSince = null;
}
