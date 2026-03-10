using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaLex.Models;
using LuminaLex.Services;

namespace LuminaLex.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly OpenAiService _openAi;
    private readonly DeepLService _deepL;
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private AppSettings _settings;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Prêt";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CorrectCommand))]
    [NotifyCanExecuteChangedFor(nameof(TranslateCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    public MainViewModel(
        OpenAiService openAi,
        DeepLService deepL,
        SettingsService settingsService,
        ThemeService themeService,
        AppSettings settings)
    {
        _openAi = openAi;
        _deepL = deepL;
        _settingsService = settingsService;
        _themeService = themeService;
        _settings = settings;
        _isDarkTheme = settings.IsDarkTheme;

        UpdateKeyStatus();
    }

    public void ReloadSettings(AppSettings settings)
    {
        _settings = settings;
        IsDarkTheme = settings.IsDarkTheme;
        UpdateKeyStatus();
    }

    private void UpdateKeyStatus()
    {
        if (string.IsNullOrWhiteSpace(_settings.OpenAiKey) && string.IsNullOrWhiteSpace(_settings.DeepLKey))
            StatusText = "⚠ Aucune clé configurée — ouvrez les paramètres (⚙)";
        else if (string.IsNullOrWhiteSpace(_settings.OpenAiKey))
            StatusText = "⚠ Clé OpenAI manquante — ouvrez les paramètres";
        else if (string.IsNullOrWhiteSpace(_settings.DeepLKey))
            StatusText = "⚠ Clé DeepL manquante — ouvrez les paramètres";
        else
        {
            var oaiMask = MaskKey(_settings.OpenAiKey);
            var dplMask = MaskKey(_settings.DeepLKey);
            var endpoint = _settings.DeepLKey.Contains(":fx", StringComparison.Ordinal) ? "Free" : "Pro";
            StatusText = $"Clés chargées — OpenAI: {oaiMask}  |  DeepL {endpoint}: {dplMask}";
        }
    }

    private static string MaskKey(string key)
    {
        if (key.Length <= 11) return "***";
        return $"{key[..8]}...{key[^3..]}";
    }

    private bool CanExecuteApi() => !IsProcessing && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(CanExecuteApi))]
    private async Task CorrectAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.OpenAiKey))
        {
            StatusText = "⚠ Clé OpenAI manquante — ouvrez les paramètres";
            return;
        }

        IsProcessing = true;
        _cts = new CancellationTokenSource();
        StatusText = "Correction (gpt-5-nano)...";

        try
        {
            var result = await _openAi.CorrectAsync(InputText.Trim(), _settings.OpenAiKey, _cts.Token);
            OutputText = result;
            StatusText = "Prêt.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Annulé.";
        }
        catch (ApiException ex)
        {
            StatusText = $"Erreur : {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur réseau : {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteApi))]
    private async Task TranslateAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.OpenAiKey))
        {
            StatusText = "⚠ Clé OpenAI manquante — ouvrez les paramètres";
            return;
        }
        if (string.IsNullOrWhiteSpace(_settings.DeepLKey))
        {
            StatusText = "⚠ Clé DeepL manquante — ouvrez les paramètres";
            return;
        }

        IsProcessing = true;
        _cts = new CancellationTokenSource();

        try
        {
            // Step 1: Correct
            StatusText = "Correction (gpt-5-nano)...";
            var corrected = await _openAi.CorrectAsync(InputText.Trim(), _settings.OpenAiKey, _cts.Token);
            OutputText = corrected;

            // Step 2: Translate
            var endpoint = _settings.DeepLKey.Contains(":fx", StringComparison.Ordinal) ? "Free" : "Pro";
            StatusText = $"Traduction (DeepL {endpoint})...";
            var translated = await _deepL.TranslateAsync(corrected, _settings.DeepLKey, _cts.Token);
            OutputText = translated;

            StatusText = "Terminé.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Annulé.";
        }
        catch (ApiException ex)
        {
            StatusText = $"Erreur : {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur réseau : {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        StatusText = "Effacé.";
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        _themeService.ApplyTheme(IsDarkTheme);
        _settings.IsDarkTheme = IsDarkTheme;
        _settingsService.Save(_settings);
    }

    // Raised by the settings window to request opening
    public event Action? OpenSettingsRequested;

    [RelayCommand]
    private void OpenSettings()
    {
        OpenSettingsRequested?.Invoke();
    }
}
