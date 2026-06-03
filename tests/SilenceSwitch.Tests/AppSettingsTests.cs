using System.Text.Json;
using Xunit;

namespace SilenceSwitch.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var s = new AppSettings();
        Assert.Equal(15, s.SilenceTimeoutMinutes);
        Assert.Equal(10, s.PollingIntervalSeconds);
        Assert.Equal(0.0001f, s.SilenceThreshold);
        Assert.True(s.SwitchOnStartup);
        Assert.Null(s.PreferredDeviceId);
        Assert.Null(s.PreferredDeviceName);
    }

    [Fact]
    public void RoundTrip_AllFields_Preserved()
    {
        var original = new AppSettings
        {
            PreferredDeviceId   = "{0.0.0.00000000}.{abc-def}",
            PreferredDeviceName = "Headphones (Realtek Audio)",
            SilenceTimeoutMinutes  = 30,
            PollingIntervalSeconds = 5,
            SilenceThreshold    = 0.001f,
            SwitchOnStartup     = false
        };

        var json     = JsonSerializer.Serialize(original);
        var restored = AppSettings.Deserialize(json);

        Assert.Equal(original.PreferredDeviceId,      restored.PreferredDeviceId);
        Assert.Equal(original.PreferredDeviceName,     restored.PreferredDeviceName);
        Assert.Equal(original.SilenceTimeoutMinutes,   restored.SilenceTimeoutMinutes);
        Assert.Equal(original.PollingIntervalSeconds,  restored.PollingIntervalSeconds);
        Assert.Equal(original.SilenceThreshold,        restored.SilenceThreshold, precision: 6);
        Assert.Equal(original.SwitchOnStartup,         restored.SwitchOnStartup);
    }

    [Fact]
    public void Deserialize_MissingFields_UsesDefaults()
    {
        const string json = """{"SilenceTimeoutMinutes": 20}""";
        var s = AppSettings.Deserialize(json);

        Assert.Equal(20,     s.SilenceTimeoutMinutes);
        Assert.Equal(10,     s.PollingIntervalSeconds);   // default
        Assert.Equal(0.0001f, s.SilenceThreshold);        // default
        Assert.True(s.SwitchOnStartup);                   // default
        Assert.Null(s.PreferredDeviceId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(120)]
    public void SilenceTimeout_ValidBoundaries_RoundTrip(int minutes)
    {
        var json     = JsonSerializer.Serialize(new AppSettings { SilenceTimeoutMinutes = minutes });
        var restored = AppSettings.Deserialize(json);
        Assert.Equal(minutes, restored.SilenceTimeoutMinutes);
    }

    [Theory]
    [InlineData(0,    1)]   // below minimum  → clamp to 1
    [InlineData(-5,   1)]   // negative       → clamp to 1
    [InlineData(121, 120)]  // above maximum  → clamp to 120
    [InlineData(999, 120)]  // far above      → clamp to 120
    public void Deserialize_OutOfRangeTimeout_IsClamped(int input, int expected)
    {
        var json = $$"""{"SilenceTimeoutMinutes": {{input}}}""";
        var s    = AppSettings.Deserialize(json);
        Assert.Equal(expected, s.SilenceTimeoutMinutes);
    }
}
