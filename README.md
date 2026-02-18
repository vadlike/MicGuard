# MicGuard 🔊

![Windows](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6?logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
![Tray App](https://img.shields.io/badge/UI-System%20Tray-00A3EE)
![Status](https://img.shields.io/badge/Status-Active-2EA44F)

MicGuard is a Windows tray app that keeps your microphone setup clean and predictable.

- 🎯 Set the microphone you want as default.
- 🚫 Block devices you do not want to stay default.
- 🔁 Auto-fix default microphone after reconnects.
- 🪟 Optional startup with Windows.
- 🔊 Custom speaker tray icon.

## ✨ Tray Menu

- `Set Default Microphone`
- `Block From Default`
- `Auto Guard: ON/OFF`
- `Start With Windows: ON/OFF`
- `Refresh`
- `About MicGuard`
- `Exit`

MicGuard applies your default microphone for:
- `Console`
- `Multimedia`
- `Communications`

## 🚀 Quick Start

```powershell
dotnet build
dotnet run --no-build
```

After launch, MicGuard runs in the system tray near the clock.

## 📦 Publish

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

Published executable:

`bin\Release\net9.0-windows\win-x64\publish\MicGuard.exe`

## ⚙️ Configuration

- Debug config: `bin\Debug\net9.0-windows\micguard.json`
- Publish config: `bin\Release\net9.0-windows\win-x64\publish\micguard.json`

Default config:

```json
{
  "PreferredMicDeviceId": null,
  "BlockedMicNameContains": [
    "OnePlus Buds Pro 3"
  ],
  "BlockedMicDeviceIds": [],
  "PreferredMicNameContains": "Realtek",
  "GuardEnabled": true,
  "EventDebounceMs": 700
}
```

## 👤 Author

`VADLIKE`
