using Xunit;

namespace SilenceSwitch.Tests;

public class SilenceStateMachineTests
{
    // Fixed epoch keeps test failure messages readable.
    private static readonly DateTime Epoch = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime At(int seconds) => Epoch.AddSeconds(seconds);

    [Fact]
    public void AudioActive_NeverTriggers()
    {
        var clock = At(0);
        var sm = new SilenceStateMachine(TimeSpan.FromMinutes(5), () => clock);

        for (int i = 0; i < 100; i++)
        {
            clock = At(i * 10);
            Assert.False(sm.Sample(audioActive: true));
        }
    }

    [Fact]
    public void ContinuousSilence_BelowTimeout_DoesNotTrigger()
    {
        var clock = At(0);
        var sm = new SilenceStateMachine(TimeSpan.FromMinutes(5), () => clock);

        sm.Sample(false);      // silence clock starts at T=0
        clock = At(299);       // 4m59s — just under the 5-minute timeout
        Assert.False(sm.Sample(false));
    }

    [Fact]
    public void ContinuousSilence_AtExactTimeout_Triggers()
    {
        var clock = At(0);
        var sm = new SilenceStateMachine(TimeSpan.FromMinutes(5), () => clock);

        sm.Sample(false);      // clock starts at T=0
        clock = At(300);       // exactly 5 minutes
        Assert.True(sm.Sample(false));
    }

    [Fact]
    public void ActivityMidSilence_ResetsClock()
    {
        var clock = At(0);
        var sm = new SilenceStateMachine(TimeSpan.FromMinutes(5), () => clock);

        sm.Sample(false);      // silence starts at T=0
        clock = At(240);
        sm.Sample(false);      // 4 min in, not triggered

        clock = At(241);
        sm.Sample(true);       // audio — resets _silentSince

        // 299 s after the activity: still under the 5-minute threshold
        clock = At(540);
        Assert.False(sm.Sample(false));
    }

    [Fact]
    public void TriggerFiresExactlyOnce_PerSilenceEpisode()
    {
        var clock = At(0);
        var sm = new SilenceStateMachine(TimeSpan.FromMinutes(5), () => clock);

        sm.Sample(false);
        clock = At(300);
        Assert.True(sm.Sample(false));   // fires once

        // Subsequent silent ticks must NOT re-fire
        clock = At(310);
        Assert.False(sm.Sample(false));
        clock = At(600);
        Assert.False(sm.Sample(false));
    }

    [Fact]
    public void AfterTrigger_FreshSilenceEpisode_TriggersAgain()
    {
        var clock = At(0);
        var sm = new SilenceStateMachine(TimeSpan.FromMinutes(5), () => clock);

        // First episode
        sm.Sample(false);
        clock = At(300);
        Assert.True(sm.Sample(false));

        // Activity clears state
        clock = At(301);
        sm.Sample(true);

        // Second episode
        clock = At(302);
        sm.Sample(false);      // new clock starts here
        clock = At(602);       // 300 s from T=302
        Assert.True(sm.Sample(false));
    }

    [Fact]
    public void Reset_ClearsSilenceClock_RequiresFreshSilence()
    {
        var clock = At(0);
        var sm = new SilenceStateMachine(TimeSpan.FromMinutes(5), () => clock);

        sm.Sample(false);      // silence starts at T=0
        clock = At(240);
        sm.Sample(false);      // 4 min in, not triggered

        sm.Reset();            // manual reset — discards in-progress timing

        clock = At(241);
        sm.Sample(false);      // new clock starts at T=241

        clock = At(540);       // 299 s from T=241 — just under 5 min
        Assert.False(sm.Sample(false));

        clock = At(541);       // exactly 300 s from T=241
        Assert.True(sm.Sample(false));
    }

    [Fact]
    public void BoundaryCase_OneMinuteTimeout()
    {
        var clock = At(0);
        var sm = new SilenceStateMachine(TimeSpan.FromMinutes(1), () => clock);

        sm.Sample(false);
        clock = At(59);
        Assert.False(sm.Sample(false));  // 59 s — not yet

        clock = At(60);
        Assert.True(sm.Sample(false));   // exactly 1 minute
    }

    [Fact]
    public void Constructor_ZeroTimeout_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SilenceStateMachine(TimeSpan.Zero));

    [Fact]
    public void Constructor_NegativeTimeout_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SilenceStateMachine(TimeSpan.FromMinutes(-1)));
}

/// <summary>
/// Stub satisfying IAudioActivityProbe — lets future SilenceMonitor integration
/// tests control "is audio active?" without any real COM or hardware.
/// </summary>
internal sealed class FakeActivityProbe : IAudioActivityProbe
{
    public bool Active { get; set; }
    public bool IsAnyDeviceActive(float peakThreshold) => Active;
}
