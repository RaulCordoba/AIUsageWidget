using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;
using AIUsageWidget.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIUsageWidget.Providers.Claude;

public sealed class ClaudeUsageProvider : IUsageProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecureCredentialStore _credentialStore;
    private readonly ClaudeUsageProviderOptions _options;
    private readonly ILogger<ClaudeUsageProvider> _logger;
    private readonly IClaudeBrowserSessionClient? _browserSessionClient;

    public ClaudeUsageProvider(
        IHttpClientFactory httpClientFactory,
        ISecureCredentialStore credentialStore,
        IOptions<ClaudeUsageProviderOptions> options,
        ILogger<ClaudeUsageProvider> logger,
        IEnumerable<IClaudeBrowserSessionClient> browserSessionClients)
    {
        _httpClientFactory = httpClientFactory;
        _credentialStore = credentialStore;
        _options = options.Value;
        _logger = logger;
        _browserSessionClient = browserSessionClients.FirstOrDefault();
    }

    public string Id => "claude";
    public string DisplayName => "Claude";

    public async Task<ProviderAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var sessionKey = await _credentialStore.GetAsync(_options.CredentialKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return ProviderAvailabilityResult.Unavailable("Claude requiere iniciar sesion o configurar una clave de sesion.", true);
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.Timeout);
            var organization = await GetOrganizationAsync(sessionKey, timeoutCts.Token).ConfigureAwait(false);
            return organization is null
                ? ProviderAvailabilityResult.Unavailable("La sesion existe, pero Claude no devolvio ninguna organizacion de chat. Puede haber caducado.", true)
                : ProviderAvailabilityResult.Available($"Claude conectado: {organization.Name ?? organization.Id}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderAvailabilityResult.Unavailable("Tiempo de espera agotado al conectar con Claude.");
        }
        catch (ClaudeSessionExpiredException)
        {
            return ProviderAvailabilityResult.Unavailable("Sesion de Claude expirada o no autorizada. Inicia sesion de nuevo.", true);
        }
        catch (ClaudeBlockedException)
        {
            return ProviderAvailabilityResult.Unavailable("Claude/Cloudflare bloqueo la peticion HTTP directa. La sessionKey existe, pero esta integracion no oficial puede necesitar reautenticacion.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validando Claude sin registrar credenciales");
            return ProviderAvailabilityResult.Unavailable("No se pudo validar Claude. Revisa la conexion o la sesion.");
        }
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        var sessionKey = await _credentialStore.GetAsync(_options.CredentialKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return Disconnected(now, "Claude no esta configurado. Anade una sesion desde Configuracion.");
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.Timeout);
            var organization = await GetOrganizationAsync(sessionKey, timeoutCts.Token).ConfigureAwait(false);
            if (organization is null)
            {
                return Disconnected(now, "No se pudo detectar una organizacion de chat en Claude. Puede que la sesion haya expirado.");
            }

            var usage = await FetchJsonAsync(sessionKey, $"/api/organizations/{organization.Id}/usage", timeoutCts.Token).ConfigureAwait(false);
            return MapUsage(usage, now, organization);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Disconnected(now, "Tiempo de espera agotado al consultar Claude.");
        }
        catch (ClaudeSessionExpiredException)
        {
            return Disconnected(now, "Sesion de Claude expirada o no autorizada. Inicia sesion de nuevo.");
        }
        catch (ClaudeBlockedException)
        {
            return Disconnected(now, "Claude/Cloudflare bloqueo la peticion HTTP directa. Esta integracion no oficial puede requerir reautenticacion.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error consultando Claude sin registrar credenciales");
            return Disconnected(now, "No se pudo consultar Claude. Revisa la conexion o la sesion.");
        }
    }

    private HttpClient CreateClient(string sessionKey)
    {
        var client = _httpClientFactory.CreateClient("claude");
        client.BaseAddress = new Uri(_options.BaseAddress);
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Referrer = new Uri("https://claude.ai/");
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "https://claude.ai");
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"sessionKey={sessionKey}");
        return client;
    }

    private async Task<ClaudeOrganization?> GetOrganizationAsync(string sessionKey, CancellationToken cancellationToken)
    {
        var organizations = await FetchJsonAsync(sessionKey, "/api/organizations", cancellationToken).ConfigureAwait(false);
        if (organizations.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        ClaudeOrganization? firstChat = null;
        ClaudeOrganization? teamChat = null;
        foreach (var organization in organizations.EnumerateArray())
        {
            var id = TryFindString(organization, "uuid") ?? TryFindString(organization, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var hasChat = organization.TryGetProperty("capabilities", out var capabilities)
                          && capabilities.ValueKind == JsonValueKind.Array
                          && capabilities.EnumerateArray().Any(x => string.Equals(x.GetString(), "chat", StringComparison.OrdinalIgnoreCase));
            if (!hasChat)
            {
                continue;
            }

            var candidate = new ClaudeOrganization(id, TryFindString(organization, "name"));
            firstChat ??= candidate;
            if (string.Equals(TryFindString(organization, "raven_type"), "team", StringComparison.OrdinalIgnoreCase))
            {
                teamChat = candidate;
            }
        }

        return teamChat ?? firstChat;
    }

    private async Task<JsonElement> GetJsonAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            if (LooksLikeHtml(content))
            {
                throw new ClaudeBlockedException();
            }

            throw new ClaudeSessionExpiredException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Claude respondio con estado {(int)response.StatusCode}.");
        }

        if (LooksLikeHtml(content))
        {
            throw new ClaudeBlockedException();
        }

        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> FetchJsonAsync(string sessionKey, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var directTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            directTimeoutCts.CancelAfter(_options.DirectHttpTimeout);
            return await GetJsonAsync(CreateClient(sessionKey), path, directTimeoutCts.Token).ConfigureAwait(false);
        }
        catch (ClaudeBlockedException) when (_browserSessionClient is not null)
        {
            _logger.LogInformation("Claude bloqueo HttpClient; reintentando mediante WebView2 sin registrar credenciales.");
            return await _browserSessionClient.GetJsonAsync(sessionKey, path, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && _browserSessionClient is not null)
        {
            _logger.LogInformation("Claude no respondio a HttpClient a tiempo; reintentando mediante WebView2 sin registrar credenciales.");
            return await _browserSessionClient.GetJsonAsync(sessionKey, path, cancellationToken).ConfigureAwait(false);
        }
    }

    private UsageSnapshot MapUsage(JsonElement root, DateTimeOffset now, ClaudeOrganization organization)
    {
        var fiveHour = TryFindObject(root, "five_hour");
        var sevenDay = TryFindObject(root, "seven_day");
        var sessionPercentage = TryFindPercentage(fiveHour ?? root, "utilization", true)
                                ?? TryFindNumber(root, "session_usage_percentage")
                                ?? TryFindNumber(root, "sessionUsagePercentage")
                                ?? TryFindNumber(root, "percentage");
        var weeklyPercentage = TryFindPercentage(sevenDay ?? root, "utilization", true)
                               ?? TryFindNumber(root, "weekly_usage_percentage")
                               ?? TryFindNumber(root, "weeklyUsagePercentage");
        var reset = TryFindDate(fiveHour ?? root, "resets_at")
                    ?? TryFindDate(root, "next_reset_at")
                    ?? TryFindDate(root, "reset_at");

        return new UsageSnapshot
        {
            ProviderId = Id,
            ProviderName = DisplayName,
            ModelName = organization.Name,
            SessionUsagePercentage = sessionPercentage,
            DailyUsagePercentage = null,
            WeeklyUsagePercentage = weeklyPercentage,
            NextResetAt = reset,
            TimeUntilReset = ResetCalculator.Calculate(reset, now),
            RequestsUsed = TryFindLong(root, "requests_used"),
            RequestsLimit = TryFindLong(root, "requests_limit"),
            TokensUsed = TryFindLong(root, "tokens_used"),
            TokensLimit = TryFindLong(root, "tokens_limit"),
            EstimatedCost = TryFindDecimal(root, "estimated_cost"),
            IsConnected = true,
            StatusMessage = sessionPercentage is null && weeklyPercentage is null
                ? "Claude conectado, pero el formato de uso no esta disponible."
                : "Claude sincronizado mediante integracion no oficial",
            RetrievedAt = now
        };
    }

    private UsageSnapshot Disconnected(DateTimeOffset now, string message) => new()
    {
        ProviderId = Id,
        ProviderName = DisplayName,
        IsConnected = false,
        StatusMessage = message,
        RetrievedAt = now
    };

    private static JsonElement? TryFindObject(JsonElement element, string name)
    {
        foreach (var property in Walk(element))
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Object)
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? TryFindString(JsonElement element, string name)
    {
        foreach (var property in Walk(element))
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static double? TryFindNumber(JsonElement element, string name)
    {
        return TryFindPercentage(element, name, false);
    }

    private static double? TryFindPercentage(JsonElement element, string name, bool fractionCanMeanPercentage)
    {
        foreach (var property in Walk(element))
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetDouble(out var value))
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    return null;
                }

                var percentage = fractionCanMeanPercentage && value is >= 0 and <= 1
                    ? value * 100
                    : value;
                return Math.Clamp(percentage, 0, 100);
            }
        }

        return null;
    }

    private static long? TryFindLong(JsonElement element, string name)
    {
        foreach (var property in Walk(element))
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetInt64(out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static decimal? TryFindDecimal(JsonElement element, string name)
    {
        foreach (var property in Walk(element))
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetDecimal(out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static DateTimeOffset? TryFindDate(JsonElement element, string name)
    {
        var value = TryFindString(element, name);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool LooksLikeHtml(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
               || trimmed.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
               || trimmed.Contains("Enable JavaScript and cookies", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<JsonProperty> Walk(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property;
                foreach (var child in Walk(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in Walk(item))
                {
                    yield return child;
                }
            }
        }
    }

    private sealed record ClaudeOrganization(string Id, string? Name);

    private sealed class ClaudeSessionExpiredException : Exception;

    private sealed class ClaudeBlockedException : Exception;
}
