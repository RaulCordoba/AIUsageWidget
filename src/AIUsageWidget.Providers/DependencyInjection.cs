using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Providers.Claude;
using AIUsageWidget.Providers.Demo;
using Microsoft.Extensions.DependencyInjection;

namespace AIUsageWidget.Providers;

public static class DependencyInjection
{
    public static IServiceCollection AddUsageProviders(this IServiceCollection services)
    {
        services.AddOptions<ClaudeUsageProviderOptions>();
        services.AddHttpClient("claude");
        services.AddSingleton<DemoUsageProvider>();
        services.AddSingleton<ClaudeUsageProvider>();
        services.AddSingleton<IUsageProvider>(sp => sp.GetRequiredService<DemoUsageProvider>());
        services.AddSingleton<IUsageProvider>(sp => sp.GetRequiredService<ClaudeUsageProvider>());
        return services;
    }
}
