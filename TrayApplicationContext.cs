using System.Drawing;
using NAudio.CoreAudioApi;
using Role = NAudio.CoreAudioApi.Role;
using System.Windows.Forms;

public sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly Role[] CaptureRoles = [Role.Console, Role.Multimedia, Role.Communications];

    private readonly GuardConfig _config;
    private readonly string _configPath;
    private readonly MMDeviceEnumerator _enumerator;
    private readonly MicrophoneGuard _guard;

    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _selectDefaultMenu;
    private readonly ToolStripMenuItem _blockedMenu;
    private readonly ToolStripMenuItem _guardToggleItem;
    private readonly ToolStripMenuItem _startupToggleItem;
    private readonly ToolStripMenuItem _aboutItem;
    private readonly Icon _trayIconImage;

    public TrayApplicationContext(GuardConfig config, string configPath)
    {
        _config = config;
        _configPath = configPath;
        _enumerator = new MMDeviceEnumerator();
        _guard = new MicrophoneGuard(_config);
        _guard.Start();

        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildDynamicMenuSections();

        _statusItem = new ToolStripMenuItem("Loading microphones...")
        {
            Enabled = false
        };

        _selectDefaultMenu = new ToolStripMenuItem("Set Default Microphone");
        _blockedMenu = new ToolStripMenuItem("Block From Default");

        _guardToggleItem = new ToolStripMenuItem("Auto Guard")
        {
            CheckOnClick = true,
            Checked = _config.GuardEnabled
        };
        _guardToggleItem.Click += (_, _) => ToggleAutoGuard();

        _startupToggleItem = new ToolStripMenuItem("Start With Windows")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled()
        };
        _startupToggleItem.Click += (_, _) => ToggleStartup();

        var refreshItem = new ToolStripMenuItem("Refresh");
        refreshItem.Click += (_, _) => RebuildDynamicMenuSections();

        _aboutItem = new ToolStripMenuItem("About MicGuard");
        _aboutItem.Click += (_, _) => ShowAbout();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_selectDefaultMenu);
        _menu.Items.Add(_blockedMenu);
        _menu.Items.Add(_guardToggleItem);
        _menu.Items.Add(_startupToggleItem);
        _menu.Items.Add(refreshItem);
        _menu.Items.Add(_aboutItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exitItem);

        _trayIconImage = TrayIconFactory.CreateSpeakerIcon();
        _trayIcon = new NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "MicGuard",
            ContextMenuStrip = _menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => RebuildDynamicMenuSections();
        RebuildDynamicMenuSections();
        _guard.EnforceNow("tray-start");
    }

    protected override void ExitThreadCore()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIconImage.Dispose();
        _menu.Dispose();
        _guard.Dispose();
        _enumerator.Dispose();
        base.ExitThreadCore();
    }

    private void ToggleAutoGuard()
    {
        _config.GuardEnabled = _guardToggleItem.Checked;
        _config.Save(_configPath);

        if (_config.GuardEnabled)
        {
            _guard.EnforceNow("guard-enabled");
        }

        RebuildDynamicMenuSections();
    }

    private void ToggleStartup()
    {
        try
        {
            StartupRegistration.SetEnabled(_startupToggleItem.Checked, BuildStartupCommand());
            _startupToggleItem.Checked = StartupRegistration.IsEnabled();
        }
        catch (Exception ex)
        {
            ShowError($"Cannot update startup setting.{Environment.NewLine}{ex.Message}");
            _startupToggleItem.Checked = StartupRegistration.IsEnabled();
        }
    }

    private void RebuildDynamicMenuSections()
    {
        List<CaptureDeviceInfo> devices;

        try
        {
            devices = LoadCaptureDevices();
        }
        catch (Exception ex)
        {
            _statusItem.Text = $"Audio error: {ex.Message}";
            _selectDefaultMenu.DropDownItems.Clear();
            _blockedMenu.DropDownItems.Clear();
            return;
        }

        if (devices.Count == 0)
        {
            _statusItem.Text = "No active microphone devices";
            _selectDefaultMenu.DropDownItems.Clear();
            _blockedMenu.DropDownItems.Clear();
            return;
        }

        var anyRoleDefault = devices.FirstOrDefault(device => device.IsDefaultAnyRole);
        _statusItem.Text = anyRoleDefault is null
            ? "Default microphone: unavailable"
            : $"Default microphone: {anyRoleDefault.Name}";

        BuildDefaultSelectionMenu(devices);
        BuildBlockedMenu(devices);

        _guardToggleItem.Checked = _config.GuardEnabled;
        _guardToggleItem.Text = _config.GuardEnabled ? "Auto Guard: ON" : "Auto Guard: OFF";
        _startupToggleItem.Checked = StartupRegistration.IsEnabled();
        _startupToggleItem.Text = _startupToggleItem.Checked ? "Start With Windows: ON" : "Start With Windows: OFF";
    }

    private void BuildDefaultSelectionMenu(List<CaptureDeviceInfo> devices)
    {
        _selectDefaultMenu.DropDownItems.Clear();
        var hasPreferred = !string.IsNullOrWhiteSpace(_config.PreferredMicDeviceId);

        foreach (var device in devices.OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase))
        {
            var isPreferred = string.Equals(device.Id, _config.PreferredMicDeviceId, StringComparison.OrdinalIgnoreCase);
            var isSelected = isPreferred || (!hasPreferred && device.IsDefaultInAllRoles);
            var item = new ToolStripMenuItem(BuildDeviceLabel(device))
            {
                Checked = isSelected
            };

            item.Click += (_, _) => SelectAsDefault(device);
            _selectDefaultMenu.DropDownItems.Add(item);
        }
    }

    private void BuildBlockedMenu(List<CaptureDeviceInfo> devices)
    {
        _blockedMenu.DropDownItems.Clear();
        var blockedSet = new HashSet<string>(_config.BlockedMicDeviceIds, StringComparer.OrdinalIgnoreCase);

        foreach (var device in devices.OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase))
        {
            var item = new ToolStripMenuItem(device.Name)
            {
                CheckOnClick = true,
                Checked = blockedSet.Contains(device.Id)
            };

            item.Click += (_, _) => ToggleBlockedDevice(device, item.Checked);
            _blockedMenu.DropDownItems.Add(item);
        }
    }

    private void SelectAsDefault(CaptureDeviceInfo device)
    {
        try
        {
            foreach (var role in CaptureRoles)
            {
                PolicyConfigInterop.SetDefaultCaptureEndpoint(device.Id, (int)role);
            }

            _config.PreferredMicDeviceId = device.Id;
            _config.PreferredMicNameContains = device.Name;
            _config.BlockedMicDeviceIds = _config.BlockedMicDeviceIds
                .Where(id => !string.Equals(id, device.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            _config.Save(_configPath);
            _guard.EnforceNow("manual-select");
            RebuildDynamicMenuSections();
        }
        catch (Exception ex)
        {
            ShowError($"Cannot set default microphone.{Environment.NewLine}{ex.Message}");
        }
    }

    private void ToggleBlockedDevice(CaptureDeviceInfo device, bool shouldBlock)
    {
        var blockedSet = new HashSet<string>(_config.BlockedMicDeviceIds, StringComparer.OrdinalIgnoreCase);

        if (shouldBlock)
        {
            blockedSet.Add(device.Id);
            if (string.Equals(_config.PreferredMicDeviceId, device.Id, StringComparison.OrdinalIgnoreCase))
            {
                _config.PreferredMicDeviceId = null;
            }
        }
        else
        {
            blockedSet.Remove(device.Id);
        }

        _config.BlockedMicDeviceIds = blockedSet.ToArray();
        _config.Save(_configPath);
        _guard.EnforceNow("blocked-updated");
        RebuildDynamicMenuSections();
    }

    private List<CaptureDeviceInfo> LoadCaptureDevices()
    {
        var defaultByRole = CaptureRoles.ToDictionary(role => role, TryGetDefaultCaptureDeviceId);
        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        var result = new List<CaptureDeviceInfo>();

        foreach (var device in devices)
        {
            var isDefaultConsole = string.Equals(device.ID, defaultByRole[Role.Console], StringComparison.OrdinalIgnoreCase);
            var isDefaultMultimedia = string.Equals(device.ID, defaultByRole[Role.Multimedia], StringComparison.OrdinalIgnoreCase);
            var isDefaultCommunications = string.Equals(device.ID, defaultByRole[Role.Communications], StringComparison.OrdinalIgnoreCase);

            result.Add(new CaptureDeviceInfo(
                device.ID,
                device.FriendlyName,
                isDefaultConsole,
                isDefaultMultimedia,
                isDefaultCommunications));
        }

        return result;
    }

    private string? TryGetDefaultCaptureDeviceId(Role role)
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, role).ID;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildDeviceLabel(CaptureDeviceInfo device)
    {
        if (device.IsDefaultInAllRoles)
        {
            return $"{device.Name} (Default)";
        }

        if (device.IsDefaultAnyRole)
        {
            return $"{device.Name} (Partial Default)";
        }

        return device.Name;
    }

    private static string BuildStartupCommand()
    {
        var preferredExePath = Path.Combine(AppContext.BaseDirectory, "MicGuard.exe");
        if (File.Exists(preferredExePath))
        {
            return $"\"{preferredExePath}\"";
        }

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            return $"\"{processPath}\"";
        }

        throw new InvalidOperationException("Cannot resolve MicGuard executable path.");
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "MicGuard\nDefault microphone tray manager\nAuthor: VADLIKE",
            "About MicGuard",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowError(string message)
    {
        MessageBox.Show(
            message,
            "MicGuard",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private sealed record CaptureDeviceInfo(
        string Id,
        string Name,
        bool IsDefaultConsole,
        bool IsDefaultMultimedia,
        bool IsDefaultCommunications)
    {
        public bool IsDefaultInAllRoles => IsDefaultConsole && IsDefaultMultimedia && IsDefaultCommunications;
        public bool IsDefaultAnyRole => IsDefaultConsole || IsDefaultMultimedia || IsDefaultCommunications;
    }
}
