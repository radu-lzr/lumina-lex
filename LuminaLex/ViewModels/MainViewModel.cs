using System.Collections.ObjectModel;
using System.Diagnostics;
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
    [NotifyCanExecuteChangedFor(nameof(CorrectCommand))]
    [NotifyCanExecuteChangedFor(nameof(TranslateCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private bool _showDiff;

    public ObservableCollection<DiffSegment> DiffSegments { get; } = new();

    [ObservableProperty]
    private string _statusText = "Prêt";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CorrectCommand))]
    [NotifyCanExecuteChangedFor(nameof(TranslateCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private bool _isDebugVisible;

    public System.Collections.ObjectModel.ObservableCollection<string> DebugEntries =>
        DebugLogService.Instance.Entries;

    [RelayCommand]
    private void CopyOutput()
    {
        if (!string.IsNullOrEmpty(OutputText))
            System.Windows.Clipboard.SetText(OutputText);
    }

    [RelayCommand]
    private void CopyInput()
    {
        if (!string.IsNullOrEmpty(InputText))
            System.Windows.Clipboard.SetText(InputText);
    }

    [RelayCommand]
    private void PasteInput()
    {
        if (System.Windows.Clipboard.ContainsText())
            InputText = System.Windows.Clipboard.GetText();
    }

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

    private static string FormatElapsed(TimeSpan ts) =>
        ts.TotalSeconds >= 1 ? $"{ts.TotalSeconds:F1}s" : $"{ts.TotalMilliseconds:F0}ms";

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
        var sw = Stopwatch.StartNew();

        try
        {
            var original = InputText.Trim();
            var corrected = await _openAi.CorrectAsync(original, _settings.OpenAiKey, _cts.Token);
            sw.Stop();

            // Replace source with corrected text
            InputText = corrected;

            // Build colored diff segments for output
            DiffSegments.Clear();
            ShowDiff = true;
            foreach (var seg in BuildDiffSegments(original, corrected))
                DiffSegments.Add(seg);

            OutputText = corrected; // plain text for copy
            var elapsed = FormatElapsed(sw.Elapsed);
            StatusText = original == corrected
                ? $"Aucune correction nécessaire. ({elapsed})"
                : $"Corrigé. ({elapsed})";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Annulé. ({FormatElapsed(sw.Elapsed)})";
        }
        catch (ApiException ex)
        {
            StatusText = $"Erreur : {ex.Message} ({FormatElapsed(sw.Elapsed)})";
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur réseau : {ex.Message} ({FormatElapsed(sw.Elapsed)})";
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
        if (string.IsNullOrWhiteSpace(_settings.DeepLKey))
        {
            StatusText = "⚠ Clé DeepL manquante — ouvrez les paramètres";
            return;
        }

        IsProcessing = true;
        _cts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();

        try
        {
            var endpoint = _settings.DeepLKey.Contains(":fx", StringComparison.Ordinal) ? "Free" : "Pro";
            StatusText = $"Traduction (DeepL {endpoint})...";
            var translated = await _deepL.TranslateAsync(InputText.Trim(), _settings.DeepLKey, _cts.Token);
            sw.Stop();
            OutputText = translated;
            ShowDiff = false;

            StatusText = $"Traduit. ({FormatElapsed(sw.Elapsed)})";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Annulé. ({FormatElapsed(sw.Elapsed)})";
        }
        catch (ApiException ex)
        {
            StatusText = $"Erreur : {ex.Message} ({FormatElapsed(sw.Elapsed)})";
        }
        catch (Exception ex)
        {
            StatusText = $"Erreur réseau : {ex.Message} ({FormatElapsed(sw.Elapsed)})";
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
        DiffSegments.Clear();
        ShowDiff = false;
        StatusText = "Effacé.";
    }

    [RelayCommand]
    private void ToggleDebug()
    {
        IsDebugVisible = !IsDebugVisible;
        DebugLogService.Instance.IsEnabled = IsDebugVisible;
        if (IsDebugVisible)
            DebugLogService.Instance.Log("App", "Mode debug activ\u00e9");
    }

    [RelayCommand]
    private void ClearDebug()
    {
        DebugLogService.Instance.Clear();
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

    /// <summary>
    /// Word-level diff producing colored segments.
    /// </summary>
    private static List<DiffSegment> BuildDiffSegments(string original, string corrected)
    {
        if (original == corrected)
            return [new DiffSegment("✓ Aucune modification.", DiffType.Unchanged)];

        var oldWords = original.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var newWords = corrected.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        int m = oldWords.Length, n = newWords.Length;
        var dp = new int[m + 1, n + 1];
        for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
                dp[i, j] = oldWords[i - 1] == newWords[j - 1]
                    ? dp[i - 1, j - 1] + 1
                    : Math.Max(dp[i - 1, j], dp[i, j - 1]);

        var parts = new List<DiffSegment>();
        int ii = m, jj = n;
        while (ii > 0 || jj > 0)
        {
            if (ii > 0 && jj > 0 && oldWords[ii - 1] == newWords[jj - 1])
            {
                parts.Add(new DiffSegment(newWords[jj - 1], DiffType.Unchanged));
                ii--; jj--;
            }
            else if (jj > 0 && (ii == 0 || dp[ii, jj - 1] >= dp[ii - 1, jj]))
            {
                parts.Add(new DiffSegment(newWords[jj - 1], DiffType.Added));
                jj--;
            }
            else
            {
                parts.Add(new DiffSegment(oldWords[ii - 1], DiffType.Removed));
                ii--;
            }
        }

        parts.Reverse();
        return parts;
    }
}
