using System.Windows;
using AIUsageWidget.App.ViewModels;

namespace AIUsageWidget.App;

public partial class SettingsWindow : Window
{
    private const string StoredCredentialMask = "********";
    private readonly SettingsViewModel _viewModel;
    private bool _isSettingPasswordProgrammatically;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += SettingsWindow_Loaded;
    }

    private void ClaudeSessionBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSettingPasswordProgrammatically)
        {
            return;
        }

        _viewModel.ClaudeSessionKey = ClaudeSessionBox.Password == StoredCredentialMask
            ? string.Empty
            : ClaudeSessionBox.Password;
    }

    private void ClaudeSessionBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (_viewModel.HasClaudeCredentials && ClaudeSessionBox.Password == StoredCredentialMask)
        {
            SetClaudePasswordBox(string.Empty);
        }
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshCredentialMask();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.HasClaudeCredentials))
            {
                RefreshCredentialMask();
            }
        };
    }

    private void ProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _viewModel.RefreshProviderHelp();
    }

    private async void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SaveCommand.CanExecute(null))
        {
            await _viewModel.SaveCommand.ExecuteAsync(null);
        }

        Close();
    }

    private void RefreshCredentialMask()
    {
        if (_viewModel.HasClaudeCredentials && string.IsNullOrWhiteSpace(_viewModel.ClaudeSessionKey))
        {
            SetClaudePasswordBox(StoredCredentialMask);
        }
        else if (!_viewModel.HasClaudeCredentials)
        {
            SetClaudePasswordBox(string.Empty);
        }
    }

    private void SetClaudePasswordBox(string value)
    {
        _isSettingPasswordProgrammatically = true;
        ClaudeSessionBox.Password = value;
        _isSettingPasswordProgrammatically = false;
    }
}
