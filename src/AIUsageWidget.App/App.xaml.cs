using System.Windows;
using AIUsageWidget.App.Services;
using AIUsageWidget.App.ViewModels;
using AIUsageWidget.Infrastructure;
using AIUsageWidget.Infrastructure.Logging;
using AIUsageWidget.Providers;
using AIUsageWidget.Providers.Claude;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIUsageWidget.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureServices(services =>
            {
                services.AddInfrastructure();
                services.AddUsageProviders();
                services.AddSingleton<UsageMonitorService>();
                services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<UsageMonitorService>());
                services.AddSingleton<TrayIconService>();
                services.AddSingleton<WindowPlacementService>();
                services.AddSingleton<IClaudeBrowserSessionClient, ClaudeWebViewSessionClient>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<MainWindow>();
                services.AddTransient<SettingsWindow>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.Services.AddSingleton<ILoggerProvider, RotatingFileLoggerProvider>();
            })
            .Build();

        await _host.StartAsync();
        var window = _host.Services.GetRequiredService<MainWindow>();
        _host.Services.GetRequiredService<TrayIconService>().Attach(window);
        MainWindow = window;
        window.Show();
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services.GetRequiredService<TrayIconService>().Dispose();
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
