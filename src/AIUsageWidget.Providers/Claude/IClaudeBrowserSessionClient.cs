using System.Text.Json;

namespace AIUsageWidget.Providers.Claude;

public interface IClaudeBrowserSessionClient
{
    Task<JsonElement> GetJsonAsync(string sessionKey, string path, CancellationToken cancellationToken = default);
}
