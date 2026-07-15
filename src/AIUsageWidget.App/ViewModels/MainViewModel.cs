using System.Globalization;
using System.Windows;
using AIUsageWidget.App.Services;
using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;
using AIUsageWidget.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushConverter = System.Windows.Media.BrushConverter;
using MediaBrushes = System.Windows.Media.Brushes;

namespace AIUsageWidget.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly UsageColorThresholds CompactColorThresholds = new()
    {
        NormalColor = "#36D399",
        WarningColor = "#3B82F6",
        HighColor = "#FB923C",
        CriticalColor = "#EF4444"
    };

    private readonly UsageMonitorService _monitorService;
    private readonly ISettingsStore _settingsStore;
    private readonly IHistoryStore _historyStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly UsageColorService _colorService = new();

    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private UsageSnapshot? _snapshot;
    [ObservableProperty] private MediaBrush _usageBrush = MediaBrushes.Gray;
    [ObservableProperty] private MediaBrush _connectionBrush = MediaBrushes.Gray;
    [ObservableProperty] private System.Windows.Media.PointCollection _historyPoints = [];

    public MainViewModel(
        UsageMonitorService monitorService,
        ISettingsStore settingsStore,
        IHistoryStore historyStore,
        IServiceProvider serviceProvider)
    {
        _monitorService = monitorService;
        _settingsStore = settingsStore;
        _historyStore = historyStore;
        _serviceProvider = serviceProvider;
        _monitorService.SnapshotUpdated += OnSnapshotUpdated;
        _ = LoadAsync();
    }

    public double WindowWidth => Settings.CompactMode ? 260 * Settings.Scale : 620 * Settings.Scale;
    public double WindowHeight => Settings.CompactMode ? 52 * Settings.Scale : 330 * Settings.Scale;
    public string ProviderName => Snapshot?.ProviderName ?? Settings.SelectedProviderId;
    public string ProviderInitial => string.IsNullOrWhiteSpace(ProviderName) ? "A" : ProviderName[..1].ToUpperInvariant();
    public string HeaderText => $"{ProviderName} {(Settings.SelectedProviderId == "demo" ? "(Demo)" : "")}";
    public double PrimaryUsage => SafePercentage(Snapshot?.PrimaryUsagePercentage);
    public double SessionUsage => SafePercentage(Snapshot?.SessionUsagePercentage);
    public double WeeklyUsage => SafePercentage(Snapshot?.WeeklyUsagePercentage);
    public string StatusMessage => Snapshot?.StatusMessage ?? "Esperando datos";
    public string ResetLine => Snapshot?.NextResetAt is { } reset
        ? $"Reset {reset.ToLocalTime().ToString(Settings.TimeFormat, CultureInfo.CurrentCulture)} · {FormatRemaining(Snapshot.TimeUntilReset)}"
        : "Reset no disponible";
    public string CompactResetLine => Snapshot?.NextResetAt is { } compactReset
        ? $"reset {compactReset.ToLocalTime().ToString(Settings.TimeFormat, CultureInfo.CurrentCulture)} ({FormatRemaining(Snapshot.TimeUntilReset)})"
        : "reset N/D";
    public string RequestLine => Snapshot?.RequestsUsed is { } used
        ? $"Solicitudes {used:N0} / {(Snapshot.RequestsLimit?.ToString("N0", CultureInfo.CurrentCulture) ?? "N/D")}"
        : "Solicitudes N/D";
    public string TokenLine => Snapshot?.TokensUsed is { } used
        ? $"Tokens {used:N0} / {(Snapshot.TokensLimit?.ToString("N0", CultureInfo.CurrentCulture) ?? "N/D")}"
        : "Tokens N/D";
    public string CostLine => Snapshot?.EstimatedCost is { } cost ? $"Coste estimado {cost:C}" : "Coste estimado N/D";
    public string ModelLine => $"Modelo {Snapshot?.ModelName ?? "N/D"}";

    [RelayCommand]
    private void Refresh() => _monitorService.RequestRefresh();

    [RelayCommand]
    private async Task ToggleModeAsync()
    {
        Settings = Settings with { CompactMode = !Settings.CompactMode };
        await _settingsStore.SaveAsync(Settings);
        NotifyLayoutChanged();
    }

    [RelayCommand]
    private void Hide() => System.Windows.Application.Current.MainWindow?.Hide();

    [RelayCommand]
    private void OpenSettings()
    {
        var window = _serviceProvider.GetRequiredService<SettingsWindow>();
        window.Owner = System.Windows.Application.Current.MainWindow;
        window.ShowDialog();
        _ = LoadAsync();
    }

    public async Task<bool> HandleClosingAsync()
    {
        if (System.Windows.Application.Current.MainWindow is { } window)
        {
            await SaveWindowPlacementAsync(window.Left, window.Top);
        }

        return Settings.MinimizeToTray;
    }

    public async Task SaveWindowPlacementAsync(double left, double top)
    {
        Settings = Settings with { WindowLeft = left, WindowTop = top };
        await _settingsStore.SaveAsync(Settings);
    }

    private async Task LoadAsync()
    {
        Settings = await _settingsStore.LoadAsync();
        NotifyLayoutChanged();
        Refresh();
    }

    private async void OnSnapshotUpdated(object? sender, UsageSnapshot snapshot)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            Snapshot = snapshot;
            UsageBrush = ToBrush(_colorService.GetColor(snapshot.PrimaryUsagePercentage, CompactColorThresholds));
            ConnectionBrush = snapshot.IsConnected ? ToBrush("#36D399") : ToBrush("#EF4444");
            await UpdateHistoryPointsAsync(snapshot.ProviderId);
            OnPropertyChanged(string.Empty);
        });
    }

    private async Task UpdateHistoryPointsAsync(string providerId)
    {
        var month = DateOnly.FromDateTime(DateTime.Now);
        var entries = await _historyStore.ReadAsync(providerId, month);
        var recent = entries.Where(x => x.UsagePercentage is not null).TakeLast(40).ToArray();
        if (recent.Length == 0)
        {
            HistoryPoints = [];
            return;
        }

        var points = new System.Windows.Media.PointCollection();
        for (var i = 0; i < recent.Length; i++)
        {
            var x = recent.Length == 1 ? 0 : i * 100.0 / (recent.Length - 1);
            var y = 100 - Math.Clamp(recent[i].UsagePercentage!.Value, 0, 100);
            points.Add(new System.Windows.Point(x, y));
        }

        HistoryPoints = points;
    }

    private void NotifyLayoutChanged()
    {
        OnPropertyChanged(nameof(WindowWidth));
        OnPropertyChanged(nameof(WindowHeight));
    }

    private static MediaBrush ToBrush(string color)
    {
        var converter = new MediaBrushConverter();
        return (MediaBrush)(converter.ConvertFromString(color) ?? MediaBrushes.Gray);
    }

    private static double SafePercentage(double? value)
    {
        if (value is not { } number || double.IsNaN(number) || double.IsInfinity(number))
        {
            return 0;
        }

        return Math.Clamp(number, 0, 100);
    }

    private static string FormatRemaining(TimeSpan? remaining)
    {
        if (remaining is null)
        {
            return "N/D";
        }

        if (remaining.Value.TotalDays >= 1)
        {
            return $"{(int)remaining.Value.TotalDays} d {remaining.Value.Hours} h";
        }

        if (remaining.Value.TotalHours >= 1)
        {
            return $"{(int)remaining.Value.TotalHours} h {remaining.Value.Minutes} min";
        }

        return $"{Math.Max(0, remaining.Value.Minutes)} min";
    }
}
