using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Services;
using AIUsageWidget.Infrastructure.History;
using AIUsageWidget.Infrastructure.Json;
using AIUsageWidget.Infrastructure.Notifications;
using AIUsageWidget.Infrastructure.Security;
using AIUsageWidget.Infrastructure.Settings;
using AIUsageWidget.Infrastructure.SystemServices;
using Microsoft.Extensions.DependencyInjection;

namespace AIUsageWidget.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<LocalAppPaths>();
        services.AddSingleton<AtomicJsonFileStore>();
        services.AddSingleton<SettingsMigrator>();
        services.AddSingleton<HistoryReductionService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<INotificationStateStore, JsonNotificationStateStore>();
        services.AddSingleton<IHistoryStore, JsonHistoryStore>();
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ISecureCredentialStore, WindowsCredentialStore>();
            services.AddSingleton<IStartupService, RegistryStartupService>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<ISecureCredentialStore, MacKeychainCredentialStore>();
            services.AddSingleton<IStartupService, MacStartupService>();
        }
        else
        {
            throw new PlatformNotSupportedException("AI Usage Widget admite Windows y macOS.");
        }
        return services;
    }
}
