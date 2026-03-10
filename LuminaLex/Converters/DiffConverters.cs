using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LuminaLex.Models;

namespace LuminaLex.Converters;

/// <summary>Converts DiffType to foreground color.</summary>
public sealed class DiffForegroundConverter : IValueConverter
{
    public static DiffForegroundConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DiffType dt ? dt switch
        {
            DiffType.Added   => (SolidColorBrush)Application.Current.Resources["SuccessText"],
            DiffType.Removed => (SolidColorBrush)Application.Current.Resources["ErrorText"],
            _                => (SolidColorBrush)Application.Current.Resources["TextPrimary"],
        } : DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts DiffType.Removed to Strikethrough.</summary>
public sealed class DiffDecorationConverter : IValueConverter
{
    public static DiffDecorationConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DiffType.Removed ? TextDecorations.Strikethrough : null!;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts DiffType.Added to a subtle green background.</summary>
public sealed class DiffBackgroundConverter : IValueConverter
{
    public static DiffBackgroundConverter Instance { get; } = new();

    private static readonly SolidColorBrush AddedBg = new(Color.FromArgb(30, 52, 211, 153));
    private static readonly SolidColorBrush RemovedBg = new(Color.FromArgb(30, 248, 113, 113));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DiffType dt ? dt switch
        {
            DiffType.Added   => AddedBg,
            DiffType.Removed => RemovedBg,
            _                => Brushes.Transparent,
        } : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
