using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;
using AIUsageWidget.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIUsageWidget.App.Services;

public sealed class UsageMonitorService : BackgroundService
{
    private readonly IEnumerable<IUsageProvider> _providers;
    private readonly ISettingsStore _settingsStore;
    private readonly IHistoryStore _historyStore;
    private readonly INotificationStateStore _notificationStateStore;
    private readonly AlertService _alertService = new();
    private readonly ILogger<UsageMonitorService> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    public event EventHandler<UsageSnapshot>? SnapshotUpdated;
    public event EventHandler<(UsageSnapshot Snapshot, int Threshold)>? AlertRaised;
    public bool IsRefreshing { get; private set; }

    public UsageMonitorService(
        IEnumerable<IUsageProvider> providers,
        ISettingsStore settingsStore,
        IHistoryStore historyStore,
        INotificationStateStore notificationStateStore,
        ILogger<UsageMonitorService> logger)
    {
        _providers = providers;
        _settingsStore = settingsStore;
        _historyStore = historyStore;
        _notificationStateStore = notificationStateStore;
        _logger = logger;
    }

    public void RequestRefresh() => _ = RefreshAsync(CancellationToken.None);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = await _settingsStore.LoadAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(settings.RefreshIntervalSeconds), stoppingToken).ConfigureAwait(false);
            await RefreshAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!await _refreshGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            IsRefreshing = true;
            var settings = await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            var provider = _providers.FirstOrDefault(x => x.Id == settings.SelectedProviderId) ?? _providers.First(x => x.Id == "demo");
            var snapshot = await provider.GetUsageAsync(cancellationToken).ConfigureAwait(false);
            SnapshotUpdated?.Invoke(this, snapshot);

            if (settings.HistoryEnabled)
            {
                await _historyStore.AddAsync(provider.Id, snapshot, cancellationToken).ConfigureAwait(false);
            }

            var state = await _notificationStateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            var decision = _alertService.Evaluate(snapshot, settings, state);
            if (decision.ShouldNotify && decision.Threshold is { } threshold)
            {
                AlertRaised?.Invoke(this, (snapshot, threshold));
                await _notificationStateStore.SaveAsync(_alertService.MarkShown(state, decision), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la actualización de uso");
        }
        finally
        {
            IsRefreshing = false;
            _refreshGate.Release();
        }
    }
}
