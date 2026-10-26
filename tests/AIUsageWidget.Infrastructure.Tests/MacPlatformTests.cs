using System.Xml.Linq;
using AIUsageWidget.Infrastructure.Security;
using AIUsageWidget.Infrastructure.SystemServices;

namespace AIUsageWidget.Infrastructure.Tests;
public sealed class MacPlatformTests
{
    [Fact]
    public void StartupRoundTripsPathsWithSpacesAndXmlCharacters()
    {
        var home = Path.Combine(Path.GetTempPath(), "AIUsageWidget-startup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var executable = Path.Combine(home, "Apps & Tools", "Widget.app", "Contents", "MacOS", "AIUsageWidget.Desktop");
            var startup = new MacStartupService(home, executable);
            Assert.False(startup.IsEnabled());
            startup.Enable();
            startup.Enable();
            Assert.True(startup.IsEnabled());
            var file = Path.Combine(home, "Library", "LaunchAgents", "com.aiusagewidget.desktop.plist");
            var document = XDocument.Load(file);
            var args = document.Descendants("array").Single().Elements("string").Select(x => x.Value).ToArray();
            Assert.Equal(["/usr/bin/open", "-a", Path.Combine(home, "Apps & Tools", "Widget.app"), "--args", "--autostart"], args);
            Assert.NotNull(document.Descendants("true").SingleOrDefault());
            startup.Disable();
            startup.Disable();
            Assert.False(startup.IsEnabled());
        }
        finally { if (Directory.Exists(home)) Directory.Delete(home, true); }
    }

    [MacIntegrationFact]
    public async Task KeychainCreatesUpdatesAndDeletesUtf8Secret()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var store = new MacKeychainCredentialStore();
        var key = "integration-test-" + Guid.NewGuid().ToString("N");
        try
        {
            Assert.Null(await store.GetAsync(key));
            await store.SaveAsync(key, "prueba-áé-日本語");
            Assert.Equal("prueba-áé-日本語", await store.GetAsync(key));
            await store.SaveAsync(key, "replacement");
            Assert.Equal("replacement", await store.GetAsync(key));
            await store.DeleteAsync(key);
            Assert.Null(await store.GetAsync(key));
            await store.DeleteAsync(key);
        }
        finally { await store.DeleteAsync(key); }
    }
}

public sealed class MacIntegrationFactAttribute : FactAttribute
{
    public MacIntegrationFactAttribute()
    {
        if (!OperatingSystem.IsMacOS() || Environment.GetEnvironmentVariable("AIUSAGEWIDGET_MAC_INTEGRATION") != "1")
            Skip = "Requires macOS and AIUSAGEWIDGET_MAC_INTEGRATION=1 (uses an isolated random Keychain entry).";
    }
}
