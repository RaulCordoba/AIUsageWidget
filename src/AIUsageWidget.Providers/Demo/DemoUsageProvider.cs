using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;

namespace AIUsageWidget.Providers.Demo;

public sealed class DemoUsageProvider : IUsageProvider
{
    private readonly TimeProvider _timeProvider;

    public DemoUsageProvider(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Id => "demo";
    public string DisplayName => "Demo";

    public Task<ProviderAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ProviderAvailabilityResult.Available("Modo demo activo"));

    public Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetLocalNow();
        var cycleStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour / 5 * 5, 0, 0, now.Offset);
        var nextReset = cycleStart.AddHours(5);
        var elapsed = (now - cycleStart).TotalSeconds;
        var percentage = Math.Clamp(elapsed / TimeSpan.FromHours(5).TotalSeconds * 100, 0, 100);

        if (now.Minute is >= 52 and <= 53)
        {
            return Task.FromResult(new UsageSnapshot
            {
                ProviderId = Id,
                ProviderName = "Demo",
                IsConnected = false,
                StatusMessage = "Error controlado de demostración",
                RetrievedAt = now,
                NextResetAt = nextReset,
                TimeUntilReset = nextReset - now
            });
        }

        return Task.FromResult(new UsageSnapshot
        {
            ProviderId = Id,
            ProviderName = "Demo",
            ModelName = "Demo Model",
            SessionUsagePercentage = Math.Round(percentage, 1),
            WeeklyUsagePercentage = Math.Round((percentage * 0.62) % 100, 1),
            NextResetAt = nextReset,
            TimeUntilReset = nextReset - now,
            RequestsUsed = (long)(percentage * 10),
            RequestsLimit = 1000,
            TokensUsed = (long)(percentage * 23_400),
            TokensLimit = 2_340_000,
            EstimatedCost = Math.Round((decimal)percentage * 0.12m, 2),
            IsConnected = true,
            StatusMessage = "Datos simulados",
            RetrievedAt = now
        });
    }
}
