using AIUsageWidget.Core.Models;

namespace AIUsageWidget.Core.Services;

public sealed class SettingsMigrator
{
    public AppSettings Migrate(AppSettings? settings)
    {
        if (settings is null)
        {
            return new AppSettings();
        }

        return settings.SchemaVersion switch
        {
            1 => settings.Validate(),
            _ => new AppSettings()
        };
    }
}
