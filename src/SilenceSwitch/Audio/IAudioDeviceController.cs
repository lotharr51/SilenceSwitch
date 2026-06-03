namespace SilenceSwitch;

/// <summary>
/// The minimum surface the silence-monitoring logic needs to query and change
/// the default audio endpoint. Concrete implementation: AudioDeviceManager.
/// Mock this in tests — no COM, no NAudio, no real hardware required.
/// </summary>
public interface IAudioDeviceController : IDisposable
{
    List<AudioDevice> GetActiveRenderDevices();
    string? GetDefaultRenderDeviceId();
    bool IsDeviceAvailable(string deviceId);
    bool SetDefaultDevice(string deviceId);
    string? GetDeviceName(string deviceId);
}

/// <summary>Immutable value identifying a Windows audio render endpoint.</summary>
public record AudioDevice(string Id, string FriendlyName);
