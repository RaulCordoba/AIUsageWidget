namespace AIUsageWidget.Providers.Claude;

public sealed class ClaudeUsageProviderOptions
{
    public string BaseAddress { get; init; } = "https://claude.ai";
    public string CredentialKey { get; init; } = "claude.sessionKey";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan DirectHttpTimeout { get; init; } = TimeSpan.FromSeconds(8);
}
