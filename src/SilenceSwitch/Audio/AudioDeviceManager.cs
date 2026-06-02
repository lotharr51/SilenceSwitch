using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace SilenceSwitch;

public sealed class AudioDeviceManager : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public record AudioDevice(string Id, string FriendlyName);

    public List<AudioDevice> GetActiveRenderDevices()
    {
        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        return devices.Select(d => new AudioDevice(d.ID, d.FriendlyName)).ToList();
    }

    public string? GetDefaultRenderDeviceId()
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return device.ID;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public bool IsDeviceAvailable(string deviceId)
    {
        try
        {
            using var device = _enumerator.GetDevice(deviceId);
            return device.State == DeviceState.Active;
        }
        catch
        {
            return false;
        }
    }

    public float GetPeakValue(string deviceId)
    {
        try
        {
            using var device = _enumerator.GetDevice(deviceId);
            return device.AudioMeterInformation.MasterPeakValue;
        }
        catch
        {
            return 0f;
        }
    }

    public float GetMaxPeakAcrossAllDevices()
    {
        float max = 0f;
        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var device in devices)
        {
            try
            {
                float peak = device.AudioMeterInformation.MasterPeakValue;
                if (peak > max) max = peak;
            }
            catch { }
            finally
            {
                device.Dispose();
            }
        }
        return max;
    }

    public bool SetDefaultDevice(string deviceId)
    {
        try
        {
            var config = (IPolicyConfig)new PolicyConfigClient();
            Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, ERole.eConsole));
            Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, ERole.eMultimedia));
            Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, ERole.eCommunications));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? GetDeviceName(string deviceId)
    {
        try
        {
            using var device = _enumerator.GetDevice(deviceId);
            return device.FriendlyName;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _enumerator.Dispose();
}
