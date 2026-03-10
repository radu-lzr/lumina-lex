using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LuminaLex.Services;

public sealed class HotkeyService : IDisposable
{
    // Win32 RegisterHotKey / UnregisterHotKey
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Low-level keyboard hook
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

    // Home key
    private const uint VK_HOME = 0x24;
    private const int HOTKEY_ID_HOME = 1;

    // ² key — OEM key, scan code 0x29 (top-left key on most keyboards)
    private const uint SCANCODE_SUPERSCRIPT_TWO = 0x29;

    private IntPtr _hookId = IntPtr.Zero;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private LowLevelKeyboardProc? _hookProc; // prevent GC collection

    public event Action? ToggleRequested;

    public void Register(Window window)
    {
        var helper = new WindowInteropHelper(window);
        // Ensure the Win32 window handle exists
        if (helper.Handle == IntPtr.Zero)
            helper.EnsureHandle();

        _windowHandle = helper.Handle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        // Register Home key as global hotkey
        RegisterHotKey(_windowHandle, HOTKEY_ID_HOME, 0, VK_HOME);

        // Install low-level keyboard hook for the ² key
        _hookProc = HookCallback;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
    }

    public void Unregister()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID_HOME);
        }

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
    }

    // Handle WM_HOTKEY messages (Home key)
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_HOME)
        {
            ToggleRequested?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // Low-level keyboard hook callback for ² key
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == WM_KEYDOWN)
        {
            // KBDLLHOOKSTRUCT: vkCode at offset 0, scanCode at offset 4
            uint scanCode = (uint)Marshal.ReadInt32(lParam, 4);
            if (scanCode == SCANCODE_SUPERSCRIPT_TWO)
            {
                Application.Current?.Dispatcher.BeginInvoke(() => ToggleRequested?.Invoke());
                return (IntPtr)1; // Suppress the key
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Unregister();
    }
}
