using System.Windows;
using System.Windows.Input;
using LuminaLex.Services;
using LuminaLex.ViewModels;

namespace LuminaLex.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private bool _suppressPasswordSync;
    private bool _isRecordingHotkey;

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
        // Don't handle Escape if we're recording a hotkey (let PreviewKeyDown handle it)
        if (_isRecordingHotkey) return;

        if (e.Key == Key.Escape)
        {
            _vm.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void HotkeyRecorder_GotFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = true;
        HotkeyRecorder.Text = "Appuyez sur une touche…";
    }

    private void HotkeyRecorder_LostFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = false;
        // Restore display to current gesture if user didn't pick anything
        HotkeyRecorder.Text = _vm.HotkeyGesture;
    }

    private void HotkeyRecorder_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Resolve system key (Alt combinations send SystemKey)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore raw modifier-only presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        // Escape cancels recording
        if (key == Key.Escape)
        {
            HotkeyRecorder.Text = _vm.HotkeyGesture;
            // Move focus away to stop recording
            FocusManager.SetFocusedElement(this, null);
            Keyboard.ClearFocus();
            return;
        }

        var mods = Keyboard.Modifiers;
        var gesture = HotkeyService.FormatGesture(key, mods);
        _vm.HotkeyGesture = gesture;
        HotkeyRecorder.Text = gesture;

        // Move focus away
        FocusManager.SetFocusedElement(this, null);
        Keyboard.ClearFocus();
    }

    private void ResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        _vm.HotkeyGesture = "²";
        HotkeyRecorder.Text = "²";
    }
}
