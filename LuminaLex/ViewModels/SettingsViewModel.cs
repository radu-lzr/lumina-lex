using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaLex.Models;
using LuminaLex.Services;

namespace LuminaLex.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private string _openAiKey = string.Empty;

    [ObservableProperty]
    private string _deepLKey = string.Empty;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _showOpenAiKey;

    [ObservableProperty]
    private bool _showDeepLKey;

    public bool Saved { get; private set; }

    public SettingsViewModel(SettingsService settingsService, ThemeService themeService, AppSettings settings)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _settings = settings;

        OpenAiKey = settings.OpenAiKey;
        DeepLKey = settings.DeepLKey;
        IsDarkTheme = settings.IsDarkTheme;
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        _themeService.ApplyTheme(value);
    }

    [RelayCommand]
    private void ToggleShowOpenAiKey() => ShowOpenAiKey = !ShowOpenAiKey;

    [RelayCommand]
    private void ToggleShowDeepLKey() => ShowDeepLKey = !ShowDeepLKey;

    public event Action? CloseRequested;

    [RelayCommand]
    private void Save()
    {
        _settings.OpenAiKey = OpenAiKey.Trim();
        _settings.DeepLKey = DeepLKey.Trim();
        _settings.IsDarkTheme = IsDarkTheme;

        _settingsService.Save(_settings);
        Saved = true;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.OpenAiKey))
            parts.Add($"OpenAI: {MaskKey(_settings.OpenAiKey)}");
        if (!string.IsNullOrWhiteSpace(_settings.DeepLKey))
        {
            var ep = _settings.DeepLKey.Contains(":fx", StringComparison.Ordinal) ? "Free" : "Pro";
            parts.Add($"DeepL {ep}: {MaskKey(_settings.DeepLKey)}");
        }

        StatusText = parts.Count > 0
            ? $"Sauvegardé — {string.Join("  |  ", parts)}"
            : "Sauvegardé (aucune clé configurée)";

        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        // Revert theme if changed
        _themeService.ApplyTheme(_settings.IsDarkTheme);
        CloseRequested?.Invoke();
    }

    private static string MaskKey(string key)
    {
        if (key.Length <= 11) return "***";
        return $"{key[..8]}...{key[^3..]}";
    }
}
