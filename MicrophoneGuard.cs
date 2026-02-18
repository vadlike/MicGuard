using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

public sealed class MicrophoneGuard : IDisposable
{
    private static readonly Role[] CaptureRoles = [Role.Console, Role.Multimedia, Role.Communications];

    private readonly GuardConfig _config;
    private readonly MMDeviceEnumerator _enumerator;
    private readonly EndpointNotificationSink _notificationSink;
    private readonly object _gate = new();

    private CancellationTokenSource? _scheduledEnforceCts;
    private bool _disposed;

    public MicrophoneGuard(GuardConfig config)
    {
        _config = config;
        _enumerator = new MMDeviceEnumerator();
        _notificationSink = new EndpointNotificationSink(reason => ScheduleEnforce(reason));
    }

    public void Start()
    {
        _enumerator.RegisterEndpointNotificationCallback(_notificationSink);
        ScheduleEnforce("startup", immediate: true);
    }

    public void EnforceNow(string reason)
    {
        ScheduleEnforce(reason, immediate: true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_gate)
        {
            _scheduledEnforceCts?.Cancel();
            _scheduledEnforceCts?.Dispose();
            _scheduledEnforceCts = null;
        }

        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notificationSink);
        }
        catch
        {
        }

        _enumerator.Dispose();
    }

    private void ScheduleEnforce(string reason, bool immediate = false)
    {
        if (_disposed)
        {
            return;
        }

        CancellationToken token;
        lock (_gate)
        {
            _scheduledEnforceCts?.Cancel();
            _scheduledEnforceCts?.Dispose();
            _scheduledEnforceCts = new CancellationTokenSource();
            token = _scheduledEnforceCts.Token;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                if (!immediate)
                {
                    await Task.Delay(_config.EventDebounceMs, token);
                }

                Enforce(reason);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] MicGuard error: {ex.Message}");
            }
        }, token);
    }

    private void Enforce(string reason)
    {
        if (!_config.GuardEnabled)
        {
            return;
        }

        var preferredMicDeviceId = _config.PreferredMicDeviceId;
        var preferredMicNameContains = _config.PreferredMicNameContains;
        var blockedMicDeviceIds = _config.BlockedMicDeviceIds;
        var blockedMicNameContains = _config.BlockedMicNameContains;

        var activeCaptureDevices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        var devices = activeCaptureDevices.ToList();
        if (devices.Count == 0)
        {
            return;
        }

        var target = FindTargetDevice(
            devices,
            preferredMicDeviceId,
            preferredMicNameContains,
            blockedMicDeviceIds,
            blockedMicNameContains);
        if (target is null)
        {
            return;
        }

        var switchedRoles = new List<Role>();
        foreach (var role in CaptureRoles)
        {
            var currentDefault = TryGetDefaultCaptureDevice(role);
            if (currentDefault is null || !IsBlockedDevice(currentDefault, blockedMicDeviceIds, blockedMicNameContains))
            {
                continue;
            }

            if (string.Equals(currentDefault.ID, target.ID, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            PolicyConfigInterop.SetDefaultCaptureEndpoint(target.ID, (int)role);
            switchedRoles.Add(role);
        }

        if (switchedRoles.Count > 0)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {reason}: switched default microphone to '{target.FriendlyName}' for {string.Join(", ", switchedRoles)}.");
        }
    }

    private MMDevice? FindTargetDevice(
        IEnumerable<MMDevice> activeCaptureDevices,
        string? preferredMicDeviceId,
        string preferredMicNameContains,
        string[] blockedMicDeviceIds,
        string[] blockedMicNameContains)
    {
        if (!string.IsNullOrWhiteSpace(preferredMicDeviceId))
        {
            var preferredById = activeCaptureDevices
                .Where(device => !IsBlockedDevice(device, blockedMicDeviceIds, blockedMicNameContains))
                .FirstOrDefault(device =>
                    string.Equals(device.ID, preferredMicDeviceId, StringComparison.OrdinalIgnoreCase));

            if (preferredById is not null)
            {
                return preferredById;
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredMicNameContains))
        {
            var preferred = activeCaptureDevices
                .Where(device => !IsBlockedDevice(device, blockedMicDeviceIds, blockedMicNameContains))
                .FirstOrDefault(device => Matches(device, preferredMicNameContains));

            if (preferred is not null)
            {
                return preferred;
            }
        }

        return activeCaptureDevices.FirstOrDefault(device => !IsBlockedDevice(device, blockedMicDeviceIds, blockedMicNameContains));
    }

    private MMDevice? TryGetDefaultCaptureDevice(Role role)
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, role);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsBlockedDevice(MMDevice device, string[] blockedMicDeviceIds, string[] blockedMicNameContains)
    {
        if (blockedMicDeviceIds.Any(id => string.Equals(id, device.ID, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        foreach (var marker in blockedMicNameContains)
        {
            if (Matches(device, marker))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(MMDevice device, string marker)
    {
        return Contains(device.FriendlyName, marker) || Contains(device.ID, marker);
    }

    private static bool Contains(string source, string marker)
    {
        return source.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class EndpointNotificationSink : IMMNotificationClient
    {
        private readonly Action<string> _onChanged;

        public EndpointNotificationSink(Action<string> onChanged)
        {
            _onChanged = onChanged;
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
            _onChanged($"state:{newState}");
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            _onChanged("added");
        }

        public void OnDeviceRemoved(string deviceId)
        {
            _onChanged("removed");
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Capture)
            {
                _onChanged($"default:{role}");
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            _onChanged("property");
        }
    }
}
