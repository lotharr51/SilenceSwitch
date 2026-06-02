using Microsoft.Win32;

namespace SilenceSwitch;

public static class StartupManager
{
    private const string AppName = "SilenceSwitch";
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
        return key?.GetValue(AppName) != null;
    }

    public static void Enable()
    {
        string exePath = Application.ExecutablePath;
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true)!;
        key.SetValue(AppName, $"\"{exePath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true)!;
        key.DeleteValue(AppName, false);
    }
}
