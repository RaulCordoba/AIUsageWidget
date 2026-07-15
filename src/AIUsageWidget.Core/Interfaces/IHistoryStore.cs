using AIUsageWidget.Core.Models;

namespace AIUsageWidget.Core.Interfaces;

public interface IHistoryStore
{
    Task AddAsync(string providerId, UsageSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UsageHistoryEntry>> ReadAsync(string providerId, DateOnly month, CancellationToken cancellationToken = default);
    Task<string> ExportJsonAsync(string providerId, CancellationToken cancellationToken = default);
    Task<string> ExportCsvAsync(string providerId, CancellationToken cancellationToken = default);
    Task PruneAsync(int retentionDays, CancellationToken cancellationToken = default);
}
