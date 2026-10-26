using AIUsageWidget.Core.Interfaces;
using AIUsageWidget.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIUsageWidget.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly ISecureCredentialStore _credentialStore;
    private readonly IStartupService _startupService;
    private readonly IEnumerable<IUsageProvider> _providers;

    [ObservableProperty] private AppSettings _settings = new();
    [ObservableProperty] private string _claudeSessionKey = string.Empty;
    [ObservableProperty] private string _status = "Listo";
    [ObservableProperty] private bool _isProviderHelpVisible;
    [ObservableProperty] private bool _hasClaudeCredentials;
    [ObservableProperty] private bool _lastSaveSucceeded;
    public Task Initialization { get; }

    public SettingsViewModel(
        ISettingsStore settingsStore,
        ISecureCredentialStore credentialStore,
        IStartupService startupService,
        IEnumerable<IUsageProvider> providers)
    {
        _settingsStore = settingsStore;
        _credentialStore = credentialStore;
        _startupService = startupService;
        _providers = providers;
        Initialization = LoadAsync();
    }

    public IReadOnlyList<string> ProviderIds => _providers.Select(x => x.Id).ToArray();
    public string CredentialHint => HasClaudeCredentials ? "Sesion guardada (escribe para sustituirla)" : "Cookie sessionKey de Claude";

    partial void OnHasClaudeCredentialsChanged(bool value) => OnPropertyChanged(nameof(CredentialHint));

    public string ProviderHelpText => Settings.SelectedProviderId.Equals("claude", StringComparison.OrdinalIgnoreCase)
        ? "Para Claude necesitas el valor de la cookie sessionKey de claude.ai. Inicia sesion en Claude desde el navegador, abre las herramientas de desarrollador, ve a Application > Cookies > https://claude.ai, copia solo el valor de sessionKey y pegalo en la caja inferior. Guardalo despues. No lo compartas ni lo escribas en archivos."
        : "El proveedor Demo no necesita credenciales. Los proveedores futuros mostraran aqui el dato de autenticacion necesario.";

    [RelayCommand]
    private void ToggleProviderHelp()
    {
        IsProviderHelpVisible = !IsProviderHelpVisible;
        RefreshProviderHelp();
    }

    public void RefreshProviderHelp()
    {
        OnPropertyChanged(nameof(ProviderHelpText));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await Initialization;
        LastSaveSucceeded = false;
        try
        {
            Settings = Settings.Validate();
            await _settingsStore.SaveAsync(Settings);
            if (Settings.StartWithWindows && !_startupService.IsEnabled())
            {
                _startupService.Enable();
            }
            else if (!Settings.StartWithWindows && _startupService.IsEnabled())
            {
                _startupService.Disable();
            }

            if (!string.IsNullOrWhiteSpace(ClaudeSessionKey))
            {
                await _credentialStore.SaveAsync("claude.sessionKey", NormalizeClaudeSessionKey(ClaudeSessionKey));
                ClaudeSessionKey = string.Empty;
                HasClaudeCredentials = true;
            }

            Status = "Configuración guardada";
            LastSaveSucceeded = true;
        }
        catch (Exception)
        {
            Status = "No se pudo guardar. Revisa los permisos de almacenamiento, inicio automatico y credenciales del sistema.";
        }
    }

    [RelayCommand]
    private async Task DeleteClaudeCredentialsAsync()
    {
        try
        {
            await _credentialStore.DeleteAsync("claude.sessionKey");
            ClaudeSessionKey = string.Empty;
            HasClaudeCredentials = false;
            Status = "Credenciales de Claude eliminadas";
        }
        catch (Exception) { Status = "No se pudieron eliminar las credenciales. Revisa el acceso al almacen seguro del sistema."; }
    }

    [RelayCommand]
    private async Task TestProviderAsync()
    {
        var provider = _providers.FirstOrDefault(x => x.Id == Settings.SelectedProviderId);
        if (provider is null)
        {
            Status = "Proveedor no encontrado";
            return;
        }

        try
        {
            var result = await provider.CheckAvailabilityAsync();
            Status = result.Message ?? (result.IsAvailable ? "Disponible" : "No disponible");
        }
        catch (Exception) { Status = "No se pudo comprobar la conexion. Revisa la sesion y el acceso al almacen seguro del sistema."; }
    }

    private async Task LoadAsync()
    {
        try
        {
            Settings = await _settingsStore.LoadAsync();
            Settings = Settings with { StartWithWindows = _startupService.IsEnabled() };
            HasClaudeCredentials = !string.IsNullOrWhiteSpace(await _credentialStore.GetAsync("claude.sessionKey"));
        }
        catch (Exception) { Status = "No se pudo cargar toda la configuracion. Revisa los permisos de almacenamiento y del almacen seguro del sistema."; }
    }

    private static string NormalizeClaudeSessionKey(string value)
    {
        var trimmed = value.Trim();
        const string cookieName = "sessionKey=";
        var start = trimmed.IndexOf(cookieName, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return trimmed;
        }

        start += cookieName.Length;
        var end = trimmed.IndexOf(';', start);
        return end < 0 ? trimmed[start..].Trim() : trimmed[start..end].Trim();
    }
}
