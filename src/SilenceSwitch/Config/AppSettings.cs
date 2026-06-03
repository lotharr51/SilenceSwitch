using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SilenceSwitch.Tests")]

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
        try
        {
            return Deserialize(File.ReadAllText(SettingsPath));
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Deserialize from JSON and clamp values to valid ranges.
    /// Internal so tests can call it directly without touching the filesystem.
    /// </summary>
    internal static AppSettings Deserialize(string json)
    {
        var s = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        s.SilenceTimeoutMinutes = Math.Clamp(s.SilenceTimeoutMinutes, 1, 120);
        return s;
    }
}
