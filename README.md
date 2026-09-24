# WinOptimizer

WinOptimizer is a Windows 11 optimization tool built with C# and WPF.

It provides tools for monitoring system resources, managing startup applications,
cleaning unnecessary files, disabling selected Windows 11 features, and improving
privacy by controlling certain Windows settings.

The project is currently under active development.

## Features

### System Dashboard
- Display Windows version and edition
- Display CPU information
- Live CPU usage monitoring
- Display GPU information
- Display motherboard and BIOS information
- Live RAM usage monitoring
- Display physical drives
- Display logical storage volumes

### Privacy
- Disable Windows Advertising ID
- Disable Tailored Experiences
- Disable Input Personalization
- Disable Activity History
- Detect the current state of privacy settings
- Automatically back up registry values before applying changes
- Restore previous registry settings
- Administrator privilege handling for protected settings

### Startup Manager
- Detect startup applications from:
  - Current user registry
  - Local machine registry
  - 32-bit startup registry
  - User Startup folder
  - Common Startup folder
  - Packaged Windows startup tasks
- Display whether startup applications are enabled or disabled
- Enable and disable supported startup applications
- Administrator privilege handling for system-wide startup entries
- Open Windows Startup Settings for packaged applications
- Backup startup configuration before changes
- Restore previous startup configuration

### Storage Analyzer
- Analyze User Temp files
- Analyze Windows Temp files
- Analyze the Downloads folder
- Analyze the Windows Recycle Bin
- Display file count and storage usage
- Detect potential cleanup locations
- Separate cleanup candidates from analysis-only locations
- Detect locations that require administrator privileges

### Restore
- Restore previous privacy changes
- Restore previous startup changes
- Keep a local history of applied changes

## Planned Features

- Safe temporary file cleanup
- Windows Temp cleanup with automatic UAC elevation
- Recycle Bin cleanup
- Improved storage cleanup statistics
- Windows 11 debloat tools
- Additional performance optimizations
- More privacy controls
- Improved error handling and logging
- Installer / release builds

## Technology

- C#
- .NET 10
- WPF
- Windows Registry
- WMI
- Windows APIs

## Disclaimer

WinOptimizer modifies Windows settings and registry values.

Although the application creates backups for supported configuration changes,
you should review changes before applying them. File cleanup operations may be
irreversible.

Use the application at your own risk.