using System.Text.Json;

namespace SilenceSwitch;

public sealed class AppSettings
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SilenceSwitch");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    public string? PreferredDeviceId { get; set; }
    public string? PreferredDeviceName { get; set; }
    public int SilenceTimeoutMinutes { get; set; } = 15;
    public int PollingIntervalSeconds { get; set; } = 10;
    public float SilenceThreshold { get; set; } = 0.0001f;
    public bool SwitchOnStartup { get; set; } = true;

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}
