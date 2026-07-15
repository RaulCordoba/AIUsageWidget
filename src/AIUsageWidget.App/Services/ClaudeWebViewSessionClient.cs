using System.Text.Json;
using System.Windows;
using System.IO;
using AIUsageWidget.Infrastructure;
using AIUsageWidget.Providers.Claude;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace AIUsageWidget.App.Services;

public sealed class ClaudeWebViewSessionClient : IClaudeBrowserSessionClient
{
    private readonly LocalAppPaths _paths;

    public ClaudeWebViewSessionClient(LocalAppPaths paths)
    {
        _paths = paths;
    }

    public Task<JsonElement> GetJsonAsync(string sessionKey, string path, CancellationToken cancellationToken = default)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            return GetJsonOnUiThreadAsync(sessionKey, path, cancellationToken);
        }

        return dispatcher.InvokeAsync(() => GetJsonOnUiThreadAsync(sessionKey, path, cancellationToken)).Task.Unwrap();
    }

    private async Task<JsonElement> GetJsonOnUiThreadAsync(string sessionKey, string path, CancellationToken cancellationToken)
    {
        var webView = new WebView2();
        var window = new Window
        {
            Width = 2,
            Height = 2,
            Left = -32000,
            Top = -32000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Opacity = 0,
            Content = webView
        };

        try
        {
            window.Show();
            var userDataFolder = Path.Combine(_paths.Root, "webview2");
            Directory.CreateDirectory(userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(environment);

            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            var cookie = webView.CoreWebView2.CookieManager.CreateCookie("sessionKey", sessionKey, ".claude.ai", "/");
            cookie.IsSecure = true;
            cookie.IsHttpOnly = true;
            webView.CoreWebView2.CookieManager.AddOrUpdateCookie(cookie);

            var uri = new Uri(new Uri("https://claude.ai"), path);
            var body = await NavigateAndReadBodyAsync(webView, uri, cancellationToken);
            if (LooksLikeHtml(body))
            {
                throw new InvalidOperationException("Claude devolvio HTML/Cloudflare en WebView2.");
            }

            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
        finally
        {
            window.Close();
            webView.Dispose();
        }
    }

    private static async Task<string> NavigateAndReadBodyAsync(WebView2 webView, Uri uri, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _ = ReadBodyAsync();
        }

        async Task ReadBodyAsync()
        {
            try
            {
                var scriptResult = await webView.ExecuteScriptAsync("document.body ? document.body.innerText : ''");
                var body = JsonSerializer.Deserialize<string>(scriptResult) ?? string.Empty;
                tcs.TrySetResult(body);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        webView.NavigationCompleted += OnNavigationCompleted;
        try
        {
            webView.CoreWebView2.Navigate(uri.ToString());
            return await tcs.Task.ConfigureAwait(true);
        }
        finally
        {
            webView.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    private static bool LooksLikeHtml(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
               || trimmed.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
               || trimmed.Contains("Enable JavaScript and cookies", StringComparison.OrdinalIgnoreCase);
    }
}
