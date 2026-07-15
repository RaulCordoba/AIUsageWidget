using AIUsageWidget.Core.Interfaces;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace AIUsageWidget.Infrastructure.SystemServices;

[SupportedOSPlatform("windows")]
public sealed class RegistryStartupService : IStartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AIUsageWidget";
    private readonly string _executablePath;

    public RegistryStartupService() : this(Environment.ProcessPath ?? string.Empty)
    {
    }

    public RegistryStartupService(string executablePath)
    {
        _executablePath = executablePath;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        return string.Equals(key?.GetValue(ValueName)?.ToString(), Quote(_executablePath), StringComparison.OrdinalIgnoreCase);
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(ValueName, Quote(_executablePath));
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string Quote(string value) => "\"" + value + "\"";
}
