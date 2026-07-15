using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIUsageWidget.Infrastructure.Json;

public sealed class AtomicJsonFileStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<AtomicJsonFileStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AtomicJsonFileStore(ILogger<AtomicJsonFileStore> logger)
    {
        _logger = logger;
    }

    public async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            var corruptPath = $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            TryMove(path, corruptPath);
            _logger.LogWarning(ex, "JSON corrupto recuperado en {FileName}", Path.GetFileName(path));
            return default;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WriteAsync<T>(string path, T value, bool backupPrevious = true, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tempPath = $"{path}.tmp";
            var backupPath = $"{path}.bak";

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken).ConfigureAwait(false);
            }

            if (backupPrevious && File.Exists(path))
            {
                File.Copy(path, backupPath, overwrite: true);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void TryMove(string source, string destination)
    {
        try
        {
            if (File.Exists(source))
            {
                File.Move(source, destination, overwrite: true);
            }
        }
        catch
        {
            // Recovery best effort only; caller gets defaults.
        }
    }
}
