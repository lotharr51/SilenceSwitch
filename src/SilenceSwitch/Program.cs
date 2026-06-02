namespace SilenceSwitch;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, "SilenceSwitch_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("SilenceSwitch is already running.", "SilenceSwitch",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var settings = AppSettings.Load();

        if (string.IsNullOrEmpty(settings.PreferredDeviceId))
        {
            using var form = new DeviceSelectionForm(settings);
            if (form.ShowDialog() != DialogResult.OK)
                return;
        }

        var deviceManager = new AudioDeviceManager();
        var silenceMonitor = new SilenceMonitor(deviceManager, settings);

        if (settings.SwitchOnStartup)
            deviceManager.SetDefaultDevice(settings.PreferredDeviceId!);

        silenceMonitor.Start();

        Application.Run(new TrayApplicationContext(deviceManager, silenceMonitor, settings));
    }
}
