namespace SilenceSwitch;

public sealed class SilenceMonitor : IDisposable
{
    private readonly AudioDeviceManager _deviceManager;
    private readonly AppSettings _settings;
    private System.Windows.Forms.Timer? _timer;
    private DateTime _lastSoundDetected;
    private bool _switchedDueToSilence;

    public event Action? DeviceSwitched;
    public event Action<TimeSpan>? SilenceUpdated;
    public event Action<TrayIconState>? IconStateChanged;

    public TimeSpan SilenceDuration => DateTime.UtcNow - _lastSoundDetected;
    public bool IsRunning => _timer?.Enabled == true;

    public SilenceMonitor(AudioDeviceManager deviceManager, AppSettings settings)
    {
        _deviceManager = deviceManager;
        _settings = settings;
        _lastSoundDetected = DateTime.UtcNow;
    }

    public void Start()
    {
        _lastSoundDetected = DateTime.UtcNow;
        _switchedDueToSilence = false;

        _timer = new System.Windows.Forms.Timer
        {
            Interval = _settings.PollingIntervalSeconds * 1000
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    public void Resume()
    {
        _timer = new System.Windows.Forms.Timer
        {
            Interval = _settings.PollingIntervalSeconds * 1000
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void ResetSilenceTimer()
    {
        _lastSoundDetected = DateTime.UtcNow;
        _switchedDueToSilence = false;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_settings.PreferredDeviceId))
            return;

        if (!_deviceManager.IsDeviceAvailable(_settings.PreferredDeviceId))
        {
            IconStateChanged?.Invoke(TrayIconState.DeviceUnavailable);
            SilenceUpdated?.Invoke(SilenceDuration);
            return;
        }

        float peak = _deviceManager.GetMaxPeakAcrossAllDevices();

        if (peak > _settings.SilenceThreshold)
        {
            _lastSoundDetected = DateTime.UtcNow;
            _switchedDueToSilence = false;
        }

        var timeout = TimeSpan.FromMinutes(_settings.SilenceTimeoutMinutes);
        bool isSilent = SilenceDuration > timeout / 2;
        string? currentDefault = _deviceManager.GetDefaultRenderDeviceId();
        bool isPreferred = currentDefault == _settings.PreferredDeviceId;

        if (isPreferred || _switchedDueToSilence)
            IconStateChanged?.Invoke(TrayIconState.Monitoring);
        else if (isSilent)
            IconStateChanged?.Invoke(TrayIconState.SilenceDetected);
        else
            IconStateChanged?.Invoke(TrayIconState.Monitoring);

        SilenceUpdated?.Invoke(SilenceDuration);

        if (_switchedDueToSilence || isPreferred)
            return;

        if (SilenceDuration >= timeout)
        {
            bool success = _deviceManager.SetDefaultDevice(_settings.PreferredDeviceId);
            if (success)
            {
                _switchedDueToSilence = true;
                IconStateChanged?.Invoke(TrayIconState.Monitoring);
                DeviceSwitched?.Invoke();
            }
        }
    }

    public void Dispose() => Stop();
}
