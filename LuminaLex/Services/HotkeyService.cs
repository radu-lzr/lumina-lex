using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace LuminaLex.Services;

public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 1;

    // Scancode for ² (top-left key on AZERTY / most EU keyboards)
    private const uint SCANCODE_SUPERSCRIPT_TWO = 0x29;

    private IntPtr _hookId = IntPtr.Zero;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private LowLevelKeyboardProc? _hookProc;

    private bool _useHook;           // true = LL hook for ², false = RegisterHotKey
    private uint _targetScanCode;    // for hook mode
    private string _currentGesture = string.Empty;

    public event Action? ToggleRequested;

    public void Register(Window window, string gesture = "²")
    {
        Unregister();

        _currentGesture = gesture;
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) helper.EnsureHandle();
        _windowHandle = helper.Handle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        if (gesture == "²")
        {
            // Use low-level keyboard hook for OEM ² key
            _useHook = true;
            _targetScanCode = SCANCODE_SUPERSCRIPT_TWO;
            _hookProc = HookCallback;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
        }
        else
        {
            // Parse modifiers + key and use RegisterHotKey
            _useHook = false;
            ParseGesture(gesture, out uint modifiers, out uint vk);
            if (vk != 0)
                RegisterHotKey(_windowHandle, HOTKEY_ID, modifiers, vk);
        }
    }

    public void Unregister()
    {
        if (_windowHandle != IntPtr.Zero)
            UnregisterHotKey(_windowHandle, HOTKEY_ID);

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleRequested?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == WM_KEYDOWN)
        {
            uint scanCode = (uint)Marshal.ReadInt32(lParam, 4);
            if (scanCode == _targetScanCode)
            {
                Application.Current?.Dispatcher.BeginInvoke(() => ToggleRequested?.Invoke());
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>Parses a gesture like "Ctrl+Shift+F6" or "Home" into Win32 modifier flags + virtual key code.</summary>
    private static void ParseGesture(string gesture, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = gesture.Split('+');
        foreach (var part in parts)
        {
            var p = part.Trim();
            switch (p.ToUpperInvariant())
            {
                case "CTRL": case "CONTROL": modifiers |= 0x0002; break;
                case "ALT":                  modifiers |= 0x0001; break;
                case "SHIFT":                modifiers |= 0x0004; break;
                default:
                    if (Enum.TryParse<Key>(p, ignoreCase: true, out var key))
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
    }

    /// <summary>Formats a WPF Key + modifiers into a display string (e.g. "Ctrl+F6").</summary>
    public static string FormatGesture(Key key, ModifierKeys mods)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt))     parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift))   parts.Add("Shift");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    public void Dispose() => Unregister();
}
