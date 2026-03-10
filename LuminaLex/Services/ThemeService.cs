using System.Windows;

namespace LuminaLex.Services;

public sealed class ThemeService
{
    private const int ThemeDictionaryIndex = 0;

    public bool IsDark { get; private set; } = true;

    public void ApplyTheme(bool isDark)
    {
        IsDark = isDark;

        var uri = isDark
            ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
            : new Uri("Themes/LightTheme.xaml", UriKind.Relative);

        var newDict = new ResourceDictionary { Source = uri };
        var mergedDicts = Application.Current.Resources.MergedDictionaries;

        if (mergedDicts.Count > ThemeDictionaryIndex)
            mergedDicts[ThemeDictionaryIndex] = newDict;
        else
            mergedDicts.Insert(ThemeDictionaryIndex, newDict);
    }
}
