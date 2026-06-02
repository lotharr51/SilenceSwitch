using System.Runtime.InteropServices;

namespace SilenceSwitch;

// IPolicyConfig COM interface — undocumented but stable since Windows Vista.
// Used by every major audio-switcher tool on Windows.
[ComImport]
[Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat(string deviceId, IntPtr ppFormat);
    [PreserveSig] int GetDeviceFormat(string deviceId, bool bDefault, IntPtr ppFormat);
    [PreserveSig] int ResetDeviceFormat(string deviceId);
    [PreserveSig] int SetDeviceFormat(string deviceId, IntPtr pEndpointFormat, IntPtr pMixFormat);
    [PreserveSig] int GetProcessingPeriod(string deviceId, bool bDefault, IntPtr defaultPeriod, IntPtr minPeriod);
    [PreserveSig] int SetProcessingPeriod(string deviceId, IntPtr period);
    [PreserveSig] int GetShareMode(string deviceId, IntPtr pMode);
    [PreserveSig] int SetShareMode(string deviceId, IntPtr mode);
    [PreserveSig] int GetPropertyValue(string deviceId, bool bFxStore, IntPtr key, IntPtr pv);
    [PreserveSig] int SetPropertyValue(string deviceId, bool bFxStore, IntPtr key, IntPtr pv);
    [PreserveSig] int SetDefaultEndpoint(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceId,
        ERole role);
    [PreserveSig] int SetEndpointVisibility(string deviceId, bool visible);
}

[ComImport]
[Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfigClient { }

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2
}
