using AIUsageWidget.App;
using AIUsageWidget.App.Services;
using AIUsageWidget.App.ViewModels;
using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;
using AIUsageWidget.Infrastructure;
using AIUsageWidget.Providers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: AvaloniaTestApplication(typeof(AIUsageWidget.Desktop.Tests.TestApplication))]
namespace AIUsageWidget.Desktop.Tests;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<AIUsageWidget.App.App>()
        .UseSkia().WithInterFont().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class DesktopTests
{
    [AvaloniaTheory]
    [InlineData(true, 1.0, 260, 52)]
    [InlineData(false, 1.0, 620, 330)]
    [InlineData(true, 2.0, 520, 104)]
    [InlineData(false, 0.75, 465, 247.5)]
    public async Task WidgetRendersAndUpdatesUsage(bool compact, double scale, double width, double height)
    {
        using var fixture = new Fixture();
        var settings = fixture.Services.GetRequiredService<ISettingsStore>();
        await settings.SaveAsync(new AppSettings { CompactMode = compact, Scale = scale, MinimizeToTray = false });
        var vm = fixture.Services.GetRequiredService<MainViewModel>();
        await vm.LoadAsync();
        var window = new MainWindow(vm, fixture.Services.GetRequiredService<WindowPlacementService>());
        window.Show();
        for (var i = 0; i < 100 && vm.Snapshot is null; i++) await Task.Delay(20);
        Assert.NotNull(vm.Snapshot);
        Assert.True(vm.Snapshot.IsConnected);
        Assert.Equal("demo", vm.Snapshot.ProviderId);
        Assert.Equal(width, window.Width);
        Assert.Equal(height, window.Height);
        Assert.InRange(vm.PrimaryUsage, 0, 100);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var output = Path.Combine(RepositoryRoot(), "artifacts", "ui");
        Directory.CreateDirectory(output);
        frame.Save(Path.Combine(output, $"{(compact ? "compact" : "expanded")}-{scale:0.##}.png"));
        await vm.ToggleModeCommand.ExecuteAsync(null);
        Assert.Equal(!compact, (await settings.LoadAsync()).CompactMode);
        Assert.Equal(!compact, vm.Settings.CompactMode);
        window.Close();
    }

    [AvaloniaFact]
    public async Task SettingsKeepStoredSecretOutOfTextBoxAndPersistChanges()
    {
        using var fixture = new Fixture();
        var store = fixture.Services.GetRequiredService<ISecureCredentialStore>();
        await store.SaveAsync("claude.sessionKey", "existing-secret");
        var vm = fixture.Services.GetRequiredService<SettingsViewModel>();
        await vm.Initialization;
        var window = new SettingsWindow(vm);
        window.Show();
        for (var i = 0; i < 100 && !vm.HasClaudeCredentials; i++) await Task.Delay(20);
        var input = window.FindControl<TextBox>("ClaudeSessionBox")!;
        Assert.True(vm.HasClaudeCredentials);
        Assert.True(string.IsNullOrEmpty(input.Text));
        Assert.Equal('●', input.PasswordChar);
        vm.ClaudeSessionKey = "sessionKey=replacement; path=/";
        vm.Settings.RefreshIntervalSeconds = 60;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("replacement", await store.GetAsync("claude.sessionKey"));
        Assert.Equal(60, (await fixture.Services.GetRequiredService<ISettingsStore>().LoadAsync()).RefreshIntervalSeconds);
        Assert.Equal(string.Empty, vm.ClaudeSessionKey);
        await vm.DeleteClaudeCredentialsCommand.ExecuteAsync(null);
        Assert.Null(await store.GetAsync("claude.sessionKey"));
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        var output = Path.Combine(RepositoryRoot(), "artifacts", "ui");
        Directory.CreateDirectory(output);
        frame!.Save(Path.Combine(output, "settings.png"));
        window.Close();
    }

    [AvaloniaFact]
    public async Task DeniedKeychainAccessIsReportedWithoutExposingErrorsOrLosingEnteredSecret()
    {
        using var fixture = new Fixture(new DeniedCredentials());
        var vm = fixture.Services.GetRequiredService<SettingsViewModel>();
        await vm.Initialization;
        vm.ClaudeSessionKey = "new-secret";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.LastSaveSucceeded);
        Assert.Equal("new-secret", vm.ClaudeSessionKey);
        Assert.DoesNotContain("SECRET", vm.Status);
        Assert.Contains("permisos", vm.Status);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AIUsageWidget.sln"))) directory = directory.Parent;
        return directory?.FullName ?? Path.GetTempPath();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "AIUsageWidget-UI-" + Guid.NewGuid().ToString("N"));
        public ServiceProvider Services { get; }
        public Fixture(ISecureCredentialStore? credentials = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure();
            services.AddSingleton(new LocalAppPaths(_root));
            services.AddSingleton<ISecureCredentialStore>(credentials ?? new MemoryCredentials());
            services.AddSingleton<IStartupService, MemoryStartup>();
            services.AddUsageProviders();
            services.AddSingleton<UsageMonitorService>();
            services.AddSingleton<WindowPlacementService>();
            services.AddSingleton<MainViewModel>();
            services.AddTransient<SettingsViewModel>();
            Services = services.BuildServiceProvider();
        }
        public void Dispose()
        {
            Services.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }

    private sealed class MemoryCredentials : ISecureCredentialStore
    {
        private readonly Dictionary<string, string> _values = [];
        public Task SaveAsync(string key, string value, CancellationToken cancellationToken = default) { _values[key] = value; return Task.CompletedTask; }
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(_values.GetValueOrDefault(key));
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { _values.Remove(key); return Task.CompletedTask; }
    }
    private sealed class DeniedCredentials : ISecureCredentialStore
    {
        public Task SaveAsync(string key, string value, CancellationToken cancellationToken = default) => throw new InvalidOperationException("SECRET");
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => throw new InvalidOperationException("SECRET");
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => throw new InvalidOperationException("SECRET");
    }

    private sealed class MemoryStartup : IStartupService
    {
        private bool _enabled;
        public bool IsEnabled() => _enabled;
        public void Enable() => _enabled = true;
        public void Disable() => _enabled = false;
    }
}
