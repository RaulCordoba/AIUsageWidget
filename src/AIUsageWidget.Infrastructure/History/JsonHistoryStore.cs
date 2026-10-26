using System.Globalization;
using System.Text;
using System.Text.Json;
using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;
using AIUsageWidget.Core.Services;
using AIUsageWidget.Infrastructure.Json;
using Microsoft.Extensions.Logging;

namespace AIUsageWidget.Infrastructure.History;

public sealed class JsonHistoryStore : IHistoryStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly LocalAppPaths _paths;
    private readonly AtomicJsonFileStore _store;
    private readonly HistoryReductionService _reduction;
    private readonly ILogger<JsonHistoryStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonHistoryStore(LocalAppPaths paths, AtomicJsonFileStore store, HistoryReductionService reduction, ILogger<JsonHistoryStore> logger)
    {
        _paths = paths;
        _store = store;
        _reduction = reduction;
        _logger = logger;
    }

    public async Task AddAsync(string providerId, UsageSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var month = DateOnly.FromDateTime(snapshot.RetrievedAt.LocalDateTime);
            var path = GetPath(providerId, month);
            var entries = (await _store.ReadAsync<List<UsageHistoryEntry>>(path, cancellationToken).ConfigureAwait(false)) ?? [];
            var previous = entries.LastOrDefault();
            if (!_reduction.ShouldStore(previous, snapshot, TimeSpan.FromMinutes(5)))
            {
                return;
            }

            entries.Add(_reduction.ToEntry(snapshot));
            await _store.WriteAsync(path, entries, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo escribir histórico para {ProviderId}", providerId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<UsageHistoryEntry>> ReadAsync(string providerId, DateOnly month, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        return await _store.ReadAsync<List<UsageHistoryEntry>>(GetPath(providerId, month), cancellationToken).ConfigureAwait(false)
               ?? [];
    }

    public async Task<string> ExportJsonAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var entries = await ReadAllAsync(providerId, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(entries, Options);
    }

    public async Task<string> ExportCsvAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var entries = await ReadAllAsync(providerId, cancellationToken).ConfigureAwait(false);
        var builder = new StringBuilder();
        builder.AppendLine("timestamp,usagePercentage,requestsUsed,tokensUsed,estimatedCost,modelName,statusMessage");
        foreach (var entry in entries)
        {
            builder.Append(Csv(entry.Timestamp.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(entry.UsagePercentage?.ToString(CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(entry.RequestsUsed?.ToString(CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(entry.TokensUsed?.ToString(CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(entry.EstimatedCost?.ToString(CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(entry.ModelName)).Append(',')
                .Append(Csv(entry.StatusMessage)).AppendLine();
        }

        return builder.ToString();
    }

    public Task PruneAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var threshold = DateTimeOffset.Now.AddDays(-retentionDays);
        foreach (var file in Directory.EnumerateFiles(_paths.History, "*.json"))
        {
            if (File.GetLastWriteTime(file) < threshold.LocalDateTime)
            {
                File.Delete(file);
            }
        }

        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<UsageHistoryEntry>> ReadAllAsync(string providerId, CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();
        var entries = new List<UsageHistoryEntry>();
        foreach (var file in Directory.EnumerateFiles(_paths.History, $"{providerId}-*.json").Order())
        {
            entries.AddRange(await _store.ReadAsync<List<UsageHistoryEntry>>(file, cancellationToken).ConfigureAwait(false) ?? []);
        }

        return entries.OrderBy(x => x.Timestamp).ToArray();
    }

    private string GetPath(string providerId, DateOnly month) =>
        Path.Combine(_paths.History, $"{providerId}-{month:yyyy-MM}.json");

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
