using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Services;
using AIUsageWidget.Infrastructure.History;
using AIUsageWidget.Infrastructure.Json;
using AIUsageWidget.Infrastructure.Notifications;
using AIUsageWidget.Infrastructure.Security;
using AIUsageWidget.Infrastructure.Settings;
using AIUsageWidget.Infrastructure.SystemServices;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;

namespace AIUsageWidget.Infrastructure;

public static class DependencyInjection
{
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<LocalAppPaths>();
        services.AddSingleton<AtomicJsonFileStore>();
        services.AddSingleton<SettingsMigrator>();
        services.AddSingleton<HistoryReductionService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<INotificationStateStore, JsonNotificationStateStore>();
        services.AddSingleton<IHistoryStore, JsonHistoryStore>();
        services.AddSingleton<ISecureCredentialStore, WindowsCredentialStore>();
        services.AddSingleton<IStartupService, RegistryStartupService>();
        return services;
    }
}
