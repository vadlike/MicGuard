using System.Runtime.InteropServices;

internal static class PolicyConfigInterop
{
    public static void SetDefaultCaptureEndpoint(string deviceId, int roleValue)
    {
        var role = (ERole)roleValue;
        var policyConfig = (IPolicyConfig)new PolicyConfigClientComObject();

        try
        {
            var hResult = policyConfig.SetDefaultEndpoint(deviceId, role);
            if (hResult != 0)
            {
                Marshal.ThrowExceptionForHR(hResult);
            }
        }
        finally
        {
            if (Marshal.IsComObject(policyConfig))
            {
                Marshal.ReleaseComObject(policyConfig);
            }
        }
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClientComObject
    {
    }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig]
        int GetMixFormat(string deviceName, IntPtr formatPointer);

        [PreserveSig]
        int GetDeviceFormat(string deviceName, [MarshalAs(UnmanagedType.Bool)] bool defaultFormat, IntPtr formatPointer);

        [PreserveSig]
        int ResetDeviceFormat(string deviceName);

        [PreserveSig]
        int SetDeviceFormat(string deviceName, IntPtr endpointFormat, IntPtr mixFormat);

        [PreserveSig]
        int GetProcessingPeriod(string deviceName, [MarshalAs(UnmanagedType.Bool)] bool defaultFormat, IntPtr defaultPeriod, IntPtr minimumPeriod);

        [PreserveSig]
        int SetProcessingPeriod(string deviceName, IntPtr period);

        [PreserveSig]
        int GetShareMode(string deviceName, IntPtr mode);

        [PreserveSig]
        int SetShareMode(string deviceName, IntPtr mode);

        [PreserveSig]
        int GetPropertyValue(string deviceName, IntPtr propertyKey, IntPtr propertyValue);

        [PreserveSig]
        int SetPropertyValue(string deviceName, IntPtr propertyKey, IntPtr propertyValue);

        [PreserveSig]
        int SetDefaultEndpoint(string deviceName, ERole role);

        [PreserveSig]
        int SetEndpointVisibility(string deviceName, [MarshalAs(UnmanagedType.Bool)] bool visible);
    }
}
