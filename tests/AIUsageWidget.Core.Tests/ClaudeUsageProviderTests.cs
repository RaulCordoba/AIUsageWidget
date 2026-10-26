using System.Net;
using System.Globalization;
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
        Assert.Equal(0.004, snapshot.SessionUsagePercentage);
        Assert.Null(snapshot.WeeklyResetAt);
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
                "utilization": 96,
                "resets_at": "2026-07-14T17:20:00+02:00"
              },
              "seven_day": {
                "utilization": 21,
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

    [Theory]
    [InlineData(0)]
    [InlineData(0.24)]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(26)]
    [InlineData(100)]
    public async Task UtilizationUsesPercentagePointsIncludingOnePercent(double value)
    {
        var number = value.ToString("R", CultureInfo.InvariantCulture);
        var provider = CreateProvider("""
            {"five_hour":{"utilization":VALUE},"seven_day":{"utilization":VALUE}}
            """.Replace("VALUE", number, StringComparison.Ordinal));
        var snapshot = await provider.GetUsageAsync();
        Assert.Equal(value, snapshot.SessionUsagePercentage);
        Assert.Equal(value, snapshot.WeeklyUsagePercentage);
    }

    [Theory]
    [InlineData(1, 1, 100)]
    [InlineData(1.5, 1, 100)]
    [InlineData(26, 100, 26)]
    public async Task ExplicitRatiosAlwaysConvertToPercentagePoints(double used, double limit, double expected)
    {
        var provider = CreateProvider(FormattableString.Invariant(
            $"{{\"five_hour\":{{\"used\":{used},\"limit\":{limit}}}}}"));
        Assert.Equal(expected, (await provider.GetUsageAsync()).SessionUsagePercentage);
    }

    [Fact]
    public async Task MissingSessionDoesNotBorrowWeeklyOrModelSpecificData()
    {
        var provider = CreateProvider("""
            {
              "five_hour": null,
              "seven_day_opus": {"utilization":100,"resets_at":"2026-10-27T09:00:00Z"},
              "seven_day": {"utilization":26,"resets_at":"2026-10-30T14:00:00Z"}
            }
            """);
        var snapshot = await provider.GetUsageAsync();
        Assert.Null(snapshot.SessionUsagePercentage);
        Assert.Null(snapshot.NextResetAt);
        Assert.Equal(26, snapshot.WeeklyUsagePercentage);
        Assert.Equal(DateTimeOffset.Parse("2026-10-30T14:00:00Z"), snapshot.WeeklyResetAt);
    }

    [Fact]
    public async Task MissingWeeklyResetDoesNotBorrowSessionOrNestedReset()
    {
        var provider = CreateProvider("""
            {
              "five_hour": {"utilization":26,"resets_at":"2026-10-26T14:00:00Z"},
              "seven_day": {"utilization":24,"details":{"resets_at":"2026-10-27T09:00:00Z"}}
            }
            """);
        var snapshot = await provider.GetUsageAsync();
        Assert.NotNull(snapshot.NextResetAt);
        Assert.Null(snapshot.WeeklyResetAt);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"unknown\"")]
    [InlineData("true")]
    [InlineData("-1")]
    public async Task UnavailableUtilizationStaysUnknownRatherThanUsingNestedPercentages(string value)
    {
        var provider = CreateProvider("""
            {"five_hour":{"utilization":VALUE,"details":{"utilization":100,"resets_at":"2026-10-26T14:00:00Z"}}}
            """.Replace("VALUE", value, StringComparison.Ordinal));
        var snapshot = await provider.GetUsageAsync();
        Assert.True(snapshot.IsConnected);
        Assert.Null(snapshot.SessionUsagePercentage);
        Assert.Null(snapshot.NextResetAt);
        Assert.Null(snapshot.WeeklyResetAt);
    }

    [Fact]
    public async Task RefreshRequestsFreshDataAndTimestampsItAfterTheResponse()
    {
        var clock = new TestClock { UtcNow = DateTimeOffset.Parse("2026-10-26T12:00:00Z") };
        var handler = new RoutingHandler(new Dictionary<string, string>
        {
            ["/api/organizations"] = """[{"uuid":"org-1","capabilities":["chat"]}]""",
            ["/api/organizations/org-1/usage"] = """{"five_hour":{"utilization":26,"resets_at":"2026-10-26T13:00:00Z"}}"""
        }, request =>
        {
            Assert.True(request.Headers.CacheControl?.NoCache);
            Assert.True(request.Headers.CacheControl?.NoStore);
            clock.UtcNow = clock.UtcNow.AddSeconds(10);
        });
        var provider = new ClaudeUsageProvider(new FakeHttpClientFactory(handler), new FakeCredentialStore(),
            Options.Create(new ClaudeUsageProviderOptions()), NullLogger<ClaudeUsageProvider>.Instance, [], clock);
        var snapshot = await provider.GetUsageAsync();
        Assert.Equal(DateTimeOffset.Parse("2026-10-26T12:00:20Z"), snapshot.RetrievedAt);
        Assert.Equal(TimeSpan.FromMinutes(60) - TimeSpan.FromSeconds(20), snapshot.TimeUntilReset);
    }

    [Fact]
    public async Task ResetOffsetsPreserveTheSameInstantAcrossDaylightSavingChanges()
    {
        var provider = CreateProvider("""
            {"five_hour":{"utilization":26,"resets_at":"2026-10-25T02:30:00+02:00"},
             "seven_day":{"utilization":24,"resets_at":"2026-10-25T02:30:00+01:00"}}
            """);
        var snapshot = await provider.GetUsageAsync();
        Assert.Equal(DateTimeOffset.Parse("2026-10-25T00:30:00Z"), snapshot.NextResetAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-25T01:30:00Z"), snapshot.WeeklyResetAt);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    [Fact]
    public async Task SubsequentRefreshReadsChangedPercentagesAndResets()
    {
        var responses = new Dictionary<string, string>
        {
            ["/api/organizations"] = """[{"uuid":"org-1","capabilities":["chat"]}]""",
            ["/api/organizations/org-1/usage"] = """
                {"five_hour":{"utilization":24,"resets_at":"2026-10-26T13:00:00Z"},
                 "seven_day":{"utilization":24,"resets_at":"2026-10-30T14:00:00Z"}}
                """
        };
        var provider = new ClaudeUsageProvider(new FakeHttpClientFactory(new RoutingHandler(responses)),
            new FakeCredentialStore(), Options.Create(new ClaudeUsageProviderOptions()),
            NullLogger<ClaudeUsageProvider>.Instance, []);
        var first = await provider.GetUsageAsync();
        responses["/api/organizations/org-1/usage"] = """
            {"five_hour":{"utilization":26,"resets_at":"2026-10-26T14:00:00Z"},
             "seven_day":{"utilization":26,"resets_at":"2026-10-31T14:00:00Z"}}
            """;
        var second = await provider.GetUsageAsync();
        Assert.Equal(24, first.SessionUsagePercentage);
        Assert.Equal(24, first.WeeklyUsagePercentage);
        Assert.Equal(26, second.SessionUsagePercentage);
        Assert.Equal(26, second.WeeklyUsagePercentage);
        Assert.Equal(DateTimeOffset.Parse("2026-10-26T14:00:00Z"), second.NextResetAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-31T14:00:00Z"), second.WeeklyResetAt);
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
        private readonly Action<HttpRequestMessage>? _onRequest;

        public RoutingHandler(IReadOnlyDictionary<string, string> responses, Action<HttpRequestMessage>? onRequest = null)
        {
            _responses = responses;
            _onRequest = onRequest;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest?.Invoke(request);
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
