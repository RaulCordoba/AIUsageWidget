using System.Net;
using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Providers.Claude;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AIUsageWidget.Core.Tests;

public sealed class ClaudeUsageProviderTests
{
    [Fact]
    public async Task GetUsageAsync_UsesDirectScopedUtilizationBeforeNestedValues()
    {
        var provider = CreateProvider(
            """
            {
              "five_hour": {
                "utilization": 0.004,
                "details": { "utilization": 1 },
                "resets_at": "2026-07-17T20:00:00+02:00"
              }
            }
            """);

        var snapshot = await provider.GetUsageAsync();

        Assert.True(snapshot.IsConnected);
        Assert.Equal(0.4, snapshot.SessionUsagePercentage);
    }

    [Fact]
    public async Task GetUsageAsync_CalculatesScopedUsageRatioBeforeNestedValues()
    {
        var provider = CreateProvider(
            """
            {
              "five_hour": {
                "used": 0.5,
                "limit": 100,
                "details": { "utilization": 1 },
                "resets_at": "2026-07-17T20:00:00+02:00"
              }
            }
            """);

        var snapshot = await provider.GetUsageAsync();

        Assert.True(snapshot.IsConnected);
        Assert.Equal(0.5, snapshot.SessionUsagePercentage);
    }

    [Fact]
    public async Task GetUsageAsync_MapsWeeklyUsageAndResetFromSevenDayScope()
    {
        var provider = CreateProvider(
            """
            {
              "five_hour": {
                "utilization": 0.96,
                "resets_at": "2026-07-14T17:20:00+02:00"
              },
              "seven_day": {
                "utilization": 0.21,
                "resets_at": "2026-07-21T15:00:00+02:00"
              }
            }
            """);

        var snapshot = await provider.GetUsageAsync();

        Assert.True(snapshot.IsConnected);
        Assert.Equal(96, snapshot.SessionUsagePercentage);
        Assert.Equal(21, snapshot.WeeklyUsagePercentage);
        Assert.Equal(DateTimeOffset.Parse("2026-07-14T17:20:00+02:00"), snapshot.NextResetAt);
        Assert.Equal(DateTimeOffset.Parse("2026-07-21T15:00:00+02:00"), snapshot.WeeklyResetAt);
    }

    private static ClaudeUsageProvider CreateProvider(string usageJson)
    {
        var factory = new FakeHttpClientFactory(new RoutingHandler(new Dictionary<string, string>
        {
            ["/api/organizations"] =
                """
                [
                  {
                    "uuid": "org-1",
                    "name": "Test Org",
                    "capabilities": ["chat"]
                  }
                ]
                """,
            ["/api/organizations/org-1/usage"] = usageJson
        }));

        return new ClaudeUsageProvider(
            factory,
            new FakeCredentialStore(),
            Options.Create(new ClaudeUsageProviderOptions
            {
                DirectHttpTimeout = TimeSpan.FromSeconds(5),
                Timeout = TimeSpan.FromSeconds(5)
            }),
            NullLogger<ClaudeUsageProvider>.Instance,
            []);
    }

    private sealed class FakeCredentialStore : ISecureCredentialStore
    {
        public Task SaveAsync(string key, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<string?>("session");

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _responses;

        public RoutingHandler(IReadOnlyDictionary<string, string> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (!_responses.TryGetValue(path, out var json))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }
}
