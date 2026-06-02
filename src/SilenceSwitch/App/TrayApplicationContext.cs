namespace SilenceSwitch;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AudioDeviceManager _deviceManager;
    private readonly SilenceMonitor _silenceMonitor;
    private readonly AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _silenceItem;
    private bool _disposed;

    public TrayApplicationContext(
        AudioDeviceManager deviceManager,
        SilenceMonitor silenceMonitor,
        AppSettings settings)
    {
        _deviceManager = deviceManager;
        _silenceMonitor = silenceMonitor;
        _settings = settings;

        _statusItem = new ToolStripMenuItem("Status: Monitoring") { Enabled = false };
        _silenceItem = new ToolStripMenuItem("Silence: 0m") { Enabled = false };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_statusItem);
        contextMenu.Items.Add(_silenceItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Switch to Headphones Now", null, OnSwitchNow);
        contextMenu.Items.Add("Settings...", null, OnSettings);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Run at Startup", null, OnToggleStartup);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, OnExit);

        UpdateStartupMenuItem(contextMenu);

        _trayIcon = new NotifyIcon
        {
            Icon = TrayIconHelper.GetIcon(TrayIconState.Monitoring),
            Text = "SilenceSwitch — Monitoring",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
        _trayIcon.DoubleClick += OnSettings;

        _silenceMonitor.SilenceUpdated += OnSilenceUpdated;
        _silenceMonitor.DeviceSwitched += OnDeviceSwitched;
        _silenceMonitor.IconStateChanged += OnIconStateChanged;
    }

    private void OnSilenceUpdated(TimeSpan duration)
    {
        _silenceItem.Text = $"Silence: {(int)duration.TotalMinutes}m {duration.Seconds}s / {_settings.SilenceTimeoutMinutes}m";

        string? currentDevice = _deviceManager.GetDefaultRenderDeviceId();
        string? currentName = currentDevice != null ? _deviceManager.GetDeviceName(currentDevice) : "Unknown";
        bool isPreferred = currentDevice == _settings.PreferredDeviceId;
        _statusItem.Text = isPreferred
            ? $"Output: {currentName} (preferred)"
            : $"Output: {currentName}";

        // Tooltip max is 128 chars
        string tooltip = isPreferred
            ? $"SilenceSwitch — {currentName}"
            : $"SilenceSwitch — Silence: {(int)duration.TotalMinutes}m / {_settings.SilenceTimeoutMinutes}m";
        _trayIcon.Text = tooltip.Length > 127 ? tooltip[..127] : tooltip;
    }

    private void OnIconStateChanged(TrayIconState state)
    {
        _trayIcon.Icon = TrayIconHelper.GetIcon(state);
    }

    private void OnDeviceSwitched()
    {
        _trayIcon.ShowBalloonTip(3000, "SilenceSwitch",
            $"Switched to {_settings.PreferredDeviceName} after {_settings.SilenceTimeoutMinutes}m of silence.",
            ToolTipIcon.Info);
    }

    private void OnSwitchNow(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_settings.PreferredDeviceId))
            return;

        bool success = _deviceManager.SetDefaultDevice(_settings.PreferredDeviceId);
        if (success)
            _silenceMonitor.ResetSilenceTimer();
        else
            _trayIcon.ShowBalloonTip(3000, "SilenceSwitch",
                "Failed to switch device. It may be disconnected.",
                ToolTipIcon.Warning);
    }

    private void OnSettings(object? sender, EventArgs e)
    {
        _silenceMonitor.Stop();

        using var form = new DeviceSelectionForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
            _silenceMonitor.Start();
        else
            _silenceMonitor.Resume();
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        bool currentlyEnabled = StartupManager.IsEnabled();
        if (currentlyEnabled)
            StartupManager.Disable();
        else
            StartupManager.Enable();

        UpdateStartupMenuItem(_trayIcon.ContextMenuStrip!);
    }

    private void UpdateStartupMenuItem(ContextMenuStrip menu)
    {
        foreach (ToolStripItem item in menu.Items)
        {
            if (item.Text == "Run at Startup" && item is ToolStripMenuItem menuItem)
            {
                menuItem.Checked = StartupManager.IsEnabled();
                break;
            }
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        Dispose(true);
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            _silenceMonitor.Dispose();
            _deviceManager.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
