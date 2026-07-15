namespace AIUsageWidget.Infrastructure;

public sealed class LocalAppPaths
{
    public LocalAppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIUsageWidget");
        History = Path.Combine(Root, "history");
        Logs = Path.Combine(Root, "logs");
        SettingsFile = Path.Combine(Root, "settings.json");
        NotificationsFile = Path.Combine(Root, "notifications.json");
    }

    public string Root { get; }
    public string History { get; }
    public string Logs { get; }
    public string SettingsFile { get; }
    public string NotificationsFile { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(History);
        Directory.CreateDirectory(Logs);
    }
}
