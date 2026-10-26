using System.Diagnostics;
using System.Text.Json;
using AIUsageWidget.Providers.Claude;

namespace AIUsageWidget.App.Services;

public sealed class MacHelperClient
{
    public async Task<JsonElement> InvokeAsync(object request, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("El cliente nativo requiere macOS.");
        var helper = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "Helpers", "AIUsageWidget.MacHelper.app", "Contents", "MacOS", "AIUsageWidget.MacHelper"));
        if (!File.Exists(helper)) throw new InvalidOperationException("Falta el componente macOS. Genera la aplicación con scripts/Build-Mac.sh.");
        var start = new ProcessStartInfo(helper)
        {
            UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo iniciar el componente macOS.");
        using var registration = timeout.Token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        // Drain diagnostics, but never include native output in logs or exceptions (it may contain provider data).
        var diagnostics = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), timeout.Token);
        process.StandardInput.Close();
        await process.WaitForExitAsync(timeout.Token);
        await diagnostics;
        timeout.Token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0) throw new InvalidOperationException("El componente macOS no pudo completar la operación. Revisa la sesión o los permisos del sistema.");
        using var document = JsonDocument.Parse(await output);
        return document.RootElement.Clone();
    }
}

public sealed class MacClaudeBrowserSessionClient(MacHelperClient helper) : IClaudeBrowserSessionClient
{
    public Task<JsonElement> GetJsonAsync(string sessionKey, string path, CancellationToken cancellationToken = default) =>
        helper.InvokeAsync(new { operation = "browser", sessionKey, path }, cancellationToken);
}
