# Sentinel (**English** | [Italiano](README.it.md))

**Sentinel** is a lightweight Windows system tray application designed to prevent your computer from going into standby or sleep mode. It works by simulating a subtle keypress (F15) periodically, ensuring your session remains active without interfering with your work.

## Features

- **Prevent Sleep**: Keeps your PC awake while "Active".
- **System Tray Integration**: changing icon (Shield) or custom `sentinel.png` if available.
- **Settings Window**: Configure options easily via a dedicated window.
- **Scheduled Suspension**: Set a time range (e.g., 13:00 - 14:00) to automatically **pause** Sentinel (allowing sleep/screen lock during lunch breaks).
- **Startup**: Option to run automatically when Windows starts.
- **Low Profile**: Runs quietly in the background with minimal resource usage.

## Installation

1.  Download the latest release (or build from source).
2.  Place `Sentinel.exe` (and optionally `sentinel.png` for a custom icon) in a folder of your choice.
3.  Run `Sentinel.exe`.

## Usage

- **Active/Inactive**: The tool starts active. You can toggle this in the Settings window.
- **Settings**: Double-click the tray icon (or right-click -> "Impostazioni...") to open the configuration window.
- **Schedule**: Enable "Sospensione Programmata" in Settings to define a pause window.
- **Exit**: Right-click the tray icon and select "Esci".

## Building from Source

This project is built using C# and .NET Framework 4.0 (for maximum compatibility).

### Requirements
- Windows OS with .NET Framework installed.

### Build
Simply run the included `build.bat` script.

Alternatively, compile manually using `csc.exe`:
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:Sentinel.exe /win32icon:sentinel.ico Sentinel.cs SettingsForm.cs AssemblyInfo.cs
```

## License
MIT License
