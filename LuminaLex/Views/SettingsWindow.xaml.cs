using System.Windows;
using System.Windows.Input;
using LuminaLex.ViewModels;

namespace LuminaLex.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private bool _suppressPasswordSync;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        _vm.CloseRequested += () => DialogResult = _vm.Saved;

        // Sync initial key values into PasswordBoxes
        Loaded += (_, _) =>
        {
            _suppressPasswordSync = true;
            OpenAiPasswordBox.Password = _vm.OpenAiKey;
            DeepLPasswordBox.Password = _vm.DeepLKey;
            _suppressPasswordSync = false;
        };
    }

    // PasswordBox doesn't support binding, so we sync manually
    private void OpenAiPasswordBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_suppressPasswordSync)
            _vm.OpenAiKey = OpenAiPasswordBox.Password;
    }

    private void DeepLPasswordBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_suppressPasswordSync)
            _vm.DeepLKey = DeepLPasswordBox.Password;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _vm.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
