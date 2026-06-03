using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace SilenceSwitch;

/// <summary>
/// "Is anything actually playing right now?" — the one question the silence logic
/// needs answered. Kept as an interface so the timing state machine can be unit-tested
/// with a fake, without any COM or real hardware (see SilenceStateMachine + tests).
/// </summary>
public interface IAudioActivityProbe
{
    /// <summary>
    /// True if ANY active render device either has an audio session in the Active
    /// state, or is metering output above <paramref name="peakThreshold"/>.
    /// </summary>
    bool IsAnyDeviceActive(float peakThreshold);
}

/// <summary>
/// Real implementation against the Windows CoreAudio stack via NAudio.
///
/// WHY SESSION STATE, NOT JUST PEAK:
///   MasterPeakValue reads ~0 during silent passages of active content — dialogue
///   gaps, quiet ambient game audio, paused-but-held streams. Polling peak alone
///   trips the silence timer mid-movie. An app that is *playing* holds an
///   AudioSessionState.AudioSessionStateActive session even when momentarily quiet,
///   so session state is the reliable "something is playing" signal. Peak is kept
///   only as a secondary catch.
///
/// THREADING:
///   CoreAudio COM objects do not survive being shared across apartments/threads.
///   Create and call this from ONE background thread. The recommended host is a
///   System.Threading.Timer callback (runs MTA on the thread pool) — NOT a
///   System.Windows.Forms.Timer (UI/STA thread, and it blocks the tray if
///   enumeration stalls). Marshal any UI/tray updates back with Control.BeginInvoke.
/// </summary>
public sealed class CoreAudioActivityProbe : IAudioActivityProbe
{
    public bool IsAnyDeviceActive(float peakThreshold)
    {
        // Fresh enumerator each poll on purpose: USB unplug, Bluetooth pairing, and
        // 24H2-style default resets otherwise leave you holding stale COM objects.
        using var enumerator = new MMDeviceEnumerator();

        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
                if (DeviceHasActiveSession(device))
                    return true;

                if (device.AudioMeterInformation.MasterPeakValue > peakThreshold)
                    return true;
            }
            catch
            {
                // One flaky device (driver glitch, race with removal) must never take
                // down the probe. Skip it — the remaining devices still get counted.
            }
            finally
            {
                device.Dispose();
            }
        }

        return false;
    }

    private static bool DeviceHasActiveSession(MMDevice device)
    {
        var sessions = device.AudioSessionManager?.Sessions;
        if (sessions == null)
            return false;

        for (int i = 0; i < sessions.Count; i++)
        {
            using var session = sessions[i];
            if (session.State == AudioSessionState.AudioSessionStateActive)
                return true;
        }

        return false;
    }
}
