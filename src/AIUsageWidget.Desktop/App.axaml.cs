using AIUsageWidget.App.Services;
using AIUsageWidget.App.ViewModels;
using AIUsageWidget.Infrastructure;
using AIUsageWidget.Infrastructure.Logging;
using AIUsageWidget.Providers;
using AIUsageWidget.Providers.Claude;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIUsageWidget.App;

public partial class App : Application
{
    private IHost? _host;
    private TrayIconService? _tray;
    private string? _smokeData;
    public static MainWindow? MainWindow { get; private set; }
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var smoke = desktop.Args?.Contains("--smoke-test") == true;
            if (smoke) _smokeData = Path.Combine(Path.GetTempPath(), "AIUsageWidget-smoke-" + Guid.NewGuid().ToString("N"));
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            _host = Host.CreateDefaultBuilder().ConfigureServices(services =>
            {
                services.AddInfrastructure();
                if (_smokeData is not null) services.AddSingleton(new LocalAppPaths(_smokeData));
                services.AddUsageProviders();
                services.AddSingleton<UsageMonitorService>();
                services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<UsageMonitorService>());
                services.AddSingleton<MacHelperClient>();
                services.AddSingleton<IClaudeBrowserSessionClient, MacClaudeBrowserSessionClient>();
                services.AddSingleton<WindowPlacementService>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SettingsWindow>();
                services.AddSingleton<MainWindow>();
            }).ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.Services.AddSingleton<ILoggerProvider, RotatingFileLoggerProvider>();
            }).Build();
            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            bool? showInDock = null;
            mainViewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.Settings) && OperatingSystem.IsMacOS() &&
                    showInDock != mainViewModel.Settings.ShowInTaskbar)
                {
                    MacApplicationService.SetShowInDock(mainViewModel.Settings.ShowInTaskbar);
                    showInDock = mainViewModel.Settings.ShowInTaskbar;
                }
            };
            desktop.MainWindow = MainWindow;
            _tray = new TrayIconService(MainWindow, _host.Services.GetRequiredService<UsageMonitorService>(),
                _host.Services.GetRequiredService<MacHelperClient>(),
                _host.Services.GetRequiredService<ILogger<TrayIconService>>(), desktop,
                _host.Services.GetRequiredService<WindowPlacementService>());
            desktop.Startup += async (_, _) =>
            {
                await _host.StartAsync();
                await _host.Services.GetRequiredService<MainViewModel>().LoadAsync();
                if (smoke)
                {
                    // Exercise the real native backend and packaged Swift helper, without a provider account.
                    try
                    {
                        await _host.Services.GetRequiredService<MacHelperClient>().InvokeAsync(new { operation = "self-test" });
                        DispatcherTimer.RunOnce(() =>
                        {
                            var vm = _host.Services.GetRequiredService<MainViewModel>();
                            desktop.Shutdown(vm.Snapshot is { IsConnected: true, ProviderId: "demo" } && MainWindow.IsVisible ? 0 : 1);
                        }, TimeSpan.FromSeconds(3));
                    }
                    catch (Exception) { desktop.Shutdown(1); }
                }
            };
            desktop.Exit += (_, _) =>
            {
                _tray.Dispose();
                // Host workers never require the UI thread to finish stopping.
                _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                _host.Dispose();
                MainWindow = null;
                if (_smokeData is not null && Directory.Exists(_smokeData)) Directory.Delete(_smokeData, true);
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
