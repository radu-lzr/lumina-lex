namespace LuminaLex.Models;

public sealed class AppSettings
{
    public string OpenAiKey { get; set; } = string.Empty;
    public string DeepLKey { get; set; } = string.Empty;
    public bool IsDarkTheme { get; set; } = true;
    public int TimeoutResolve { get; set; } = 5000;
    public int TimeoutConnect { get; set; } = 10000;
    public int TimeoutSend { get; set; } = 0;
    public int TimeoutReceive { get; set; } = 30000;
}
