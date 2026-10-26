using AIUsageWidget.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIUsageWidget.App;
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel = null!;
    public SettingsWindow() => InitializeComponent();
    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
    }
    private void ProviderComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) => _viewModel?.RefreshProviderHelp();
    private async void Close_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SaveCommand.CanExecute(null)) await _viewModel.SaveCommand.ExecuteAsync(null);
        if (_viewModel.LastSaveSucceeded) Close();
    }
}
