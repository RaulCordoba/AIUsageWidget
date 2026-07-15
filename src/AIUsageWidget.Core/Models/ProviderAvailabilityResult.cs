namespace AIUsageWidget.Core.Models;

public sealed class ProviderAvailabilityResult
{
    public bool IsAvailable { get; init; }
    public bool RequiresAuthentication { get; init; }
    public string? Message { get; init; }

    public static ProviderAvailabilityResult Available(string? message = null) => new()
    {
        IsAvailable = true,
        Message = message
    };

    public static ProviderAvailabilityResult Unavailable(string message, bool requiresAuthentication = false) => new()
    {
        IsAvailable = false,
        RequiresAuthentication = requiresAuthentication,
        Message = message
    };
}
