namespace SilenceSwitch;

public sealed class SilenceMonitor : IDisposable
{
    private readonly AudioDeviceManager _deviceManager;
    private readonly AppSettings _settings;
    private readonly IAudioActivityProbe _probe;
    private SilenceStateMachine? _stateMachine;
    private System.Threading.Timer? _timer;
    private DateTime _lastActivityDetected;
    private const float PeakThreshold = 0.003f;

    public event Action? DeviceSwitched;
    public event Action<TimeSpan>? SilenceUpdated;
    public event Action<TrayIconState>? IconStateChanged;

    public TimeSpan SilenceDuration => DateTime.UtcNow - _lastActivityDetected;
    public bool IsRunning => _timer != null;

    public SilenceMonitor(AudioDeviceManager deviceManager, AppSettings settings)
    {
        _deviceManager = deviceManager;
        _settings = settings;
        _probe = new CoreAudioActivityProbe();
        _lastActivityDetected = DateTime.UtcNow;
    }

    public void Start()
    {
        _lastActivityDetected = DateTime.UtcNow;
        _stateMachine = new SilenceStateMachine(TimeSpan.FromMinutes(_settings.SilenceTimeoutMinutes));
        var interval = TimeSpan.FromSeconds(_settings.PollingIntervalSeconds);
        _timer = new System.Threading.Timer(OnTick, null, interval, interval);
    }

    public void Stop()
    {
        var t = Interlocked.Exchange(ref _timer, null);
        t?.Dispose();
        _stateMachine = null;
    }

    public void Resume()
    {
        _stateMachine ??= new SilenceStateMachine(TimeSpan.FromMinutes(_settings.SilenceTimeoutMinutes));
        var interval = TimeSpan.FromSeconds(_settings.PollingIntervalSeconds);
        _timer = new System.Threading.Timer(OnTick, null, interval, interval);
    }

    public void ResetSilenceTimer()
    {
        _lastActivityDetected = DateTime.UtcNow;
        _stateMachine?.Reset();
    }

    private void OnTick(object? state)
    {
        if (string.IsNullOrEmpty(_settings.PreferredDeviceId))
            return;

        if (!_deviceManager.IsDeviceAvailable(_settings.PreferredDeviceId))
        {
            IconStateChanged?.Invoke(TrayIconState.DeviceUnavailable);
            SilenceUpdated?.Invoke(SilenceDuration);
            return;
        }

        bool audioActive = _probe.IsAnyDeviceActive(PeakThreshold);

        if (audioActive)
            _lastActivityDetected = DateTime.UtcNow;

        string? currentDefault = _deviceManager.GetDefaultRenderDeviceId();
        bool isPreferred = currentDefault == _settings.PreferredDeviceId;

        var silenceDuration = SilenceDuration;
        var timeout = TimeSpan.FromMinutes(_settings.SilenceTimeoutMinutes);

        if (isPreferred)
        {
            _stateMachine?.Reset();
            IconStateChanged?.Invoke(TrayIconState.Monitoring);
            SilenceUpdated?.Invoke(silenceDuration);
            return;
        }

        bool isSilentLong = silenceDuration > timeout / 2;
        IconStateChanged?.Invoke(isSilentLong ? TrayIconState.SilenceDetected : TrayIconState.Monitoring);
        SilenceUpdated?.Invoke(silenceDuration);

        if (_stateMachine?.Sample(audioActive) == true)
        {
            bool success = _deviceManager.SetDefaultDevice(_settings.PreferredDeviceId);
            if (success)
            {
                _lastActivityDetected = DateTime.UtcNow;
                DeviceSwitched?.Invoke();
            }
        }
    }

    public void Dispose() => Stop();
}
