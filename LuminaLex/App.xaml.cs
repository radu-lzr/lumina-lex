using System.Windows;
using System.Windows.Interop;
using LuminaLex.Models;
using LuminaLex.Services;
using LuminaLex.ViewModels;
using LuminaLex.Views;

namespace LuminaLex;

public partial class App : Application
{
    private HotkeyService? _hotkeyService;
    private MainWindow? _mainWindow;
    private MainViewModel? _mainVm;
    private SettingsService? _settingsService;
    private ThemeService? _themeService;
    private AppSettings? _settings;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Services
        _settingsService = new SettingsService();
        _themeService = new ThemeService();
        var openAiService = new OpenAiService();
        var deepLService = new DeepLService();

        // Load settings & apply theme
        _settings = _settingsService.Load();
        _themeService.ApplyTheme(_settings.IsDarkTheme);

        // ViewModel
        _mainVm = new MainViewModel(openAiService, deepLService, _settingsService, _themeService, _settings);
        _mainVm.OpenSettingsRequested += OnOpenSettings;

        // Main window — starts hidden
        _mainWindow = new MainWindow
        {
            DataContext = _mainVm,
            Opacity = 0
        };

        // Ensure HWND exists for hotkey registration without showing the window
        new WindowInteropHelper(_mainWindow).EnsureHandle();

        // Register global hotkeys
        _hotkeyService = new HotkeyService();
        _hotkeyService.ToggleRequested += () =>
            Dispatcher.Invoke(() => _mainWindow.ToggleOverlay());
        _hotkeyService.Register(_mainWindow);
    }

    private void OnOpenSettings()
    {
        if (_settings == null || _settingsService == null || _themeService == null || _mainVm == null)
            return;

        var settingsVm = new SettingsViewModel(_settingsService, _themeService, _settings);
        var settingsWindow = new SettingsWindow(settingsVm);

        if (_mainWindow != null)
            settingsWindow.Owner = _mainWindow;

        settingsWindow.ShowDialog();

        if (settingsVm.Saved)
        {
            // Reload settings into main VM
            _settings = _settingsService.Load();
            _mainVm.ReloadSettings(_settings);
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
    }
}

