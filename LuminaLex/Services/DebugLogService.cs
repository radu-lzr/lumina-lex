using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace LuminaLex.Services;

public sealed class DebugLogService
{
    public static DebugLogService Instance { get; } = new();

    public ObservableCollection<string> Entries { get; } = new();

    public bool IsEnabled { get; set; }

    private DebugLogService() { }

    public void Log(string tag, string message)
    {
        if (!IsEnabled) return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var entry = $"[{timestamp}] [{tag}] {message}";

        if (Application.Current?.Dispatcher is Dispatcher d && !d.CheckAccess())
            d.Invoke(() => Entries.Add(entry));
        else
            Entries.Add(entry);
    }

    public void Clear() => Entries.Clear();
}
