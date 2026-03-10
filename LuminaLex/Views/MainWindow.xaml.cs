using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LuminaLex.Views;

public partial class MainWindow : Window
{
    private bool _isVisible;

    // Win32 constants for resize hit-testing
    private const int WM_NCHITTEST = 0x0084;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int ResizeGrabSize = 6; // pixels from the visible border edge to trigger resize

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Hook WndProc for custom resize hit-testing on borderless window
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            var result = HandleNcHitTest(lParam);
            if (result != 0)
            {
                handled = true;
                return (IntPtr)result;
            }
        }
        return IntPtr.Zero;
    }

    private int HandleNcHitTest(IntPtr lParam)
    {
        // Extract mouse position from lParam (screen coordinates)
        int x = (short)(lParam.ToInt64() & 0xFFFF);
        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

        // Convert to window-relative coordinates
        var point = PointFromScreen(new Point(x, y));
        double w = ActualWidth;
        double h = ActualHeight;

        // The visible border starts at Margin=16 from the window edge.
        // Allow resize from within the shadow margin area + a few pixels inside the border.
        const double margin = 16;
        double grabOuter = margin - 2;         // start grabbing 2px inside the shadow margin edge
        double grabInner = margin + ResizeGrabSize; // extend a few px inside the visible content

        bool left   = point.X >= grabOuter && point.X < grabInner;
        bool right  = point.X > w - grabInner && point.X <= w - grabOuter;
        bool top    = point.Y >= grabOuter && point.Y < grabInner;
        bool bottom = point.Y > h - grabInner && point.Y <= h - grabOuter;

        // Also allow grabbing from the transparent margin itself (outside the visible border)
        bool inLeftMargin   = point.X < margin;
        bool inRightMargin  = point.X > w - margin;
        bool inTopMargin    = point.Y < margin;
        bool inBottomMargin = point.Y > h - margin;

        bool isLeft   = left   || inLeftMargin;
        bool isRight  = right  || inRightMargin;
        bool isTop    = top    || inTopMargin;
        bool isBottom = bottom || inBottomMargin;

        if (isTop && isLeft)     return HTTOPLEFT;
        if (isTop && isRight)    return HTTOPRIGHT;
        if (isBottom && isLeft)  return HTBOTTOMLEFT;
        if (isBottom && isRight) return HTBOTTOMRIGHT;
        if (isLeft)   return HTLEFT;
        if (isRight)  return HTRIGHT;
        if (isTop)    return HTTOP;
        if (isBottom) return HTBOTTOM;

        return 0; // Not on a border — let WPF handle it
    }

    public void ToggleOverlay()
    {
        if (_isVisible)
            HideOverlay();
        else
            ShowOverlay();
    }

    // Win32 for window style manipulation (flicker prevention)
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public void ShowOverlay()
    {
        if (_isVisible) return;
        _isVisible = true;

        // Clear any lingering animation and force invisible
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;

        // Set initial scale for the scale+fade animation
        RootScale.ScaleX = 0.96;
        RootScale.ScaleY = 0.96;

        CenterOnCurrentMonitor();

        // Temporarily add WS_EX_NOACTIVATE so Show() doesn't steal focus/flash
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);

        Show();

        // Remove WS_EX_NOACTIVATE so the window can be activated normally
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        // Combined: fade in + scale up
        var duration = TimeSpan.FromMilliseconds(250);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fadeIn = new DoubleAnimation(0, 0.94, duration) { EasingFunction = ease };
        var scaleX = new DoubleAnimation(0.96, 1.0, duration) { EasingFunction = ease };
        var scaleY = new DoubleAnimation(0.96, 1.0, duration) { EasingFunction = ease };

        fadeIn.Completed += (_, _) =>
        {
            if (_isVisible) Activate();
        };

        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        BeginAnimation(OpacityProperty, fadeIn);
    }

    public void HideOverlay()
    {
        if (!_isVisible) return;
        _isVisible = false;

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        var fadeOut = new DoubleAnimation(Opacity, 0, duration) { EasingFunction = ease };
        var scaleX = new DoubleAnimation(1.0, 0.96, duration) { EasingFunction = ease };
        var scaleY = new DoubleAnimation(1.0, 0.96, duration) { EasingFunction = ease };

        fadeOut.Completed += (_, _) =>
        {
            if (!_isVisible)
            {
                Hide();
                BeginAnimation(OpacityProperty, null);
                Opacity = 0;
                RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                RootScale.ScaleX = 0.96;
                RootScale.ScaleY = 0.96;
            }
        };

        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Voulez-vous vraiment quitter Lumina Lex ?",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            Application.Current.Shutdown();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isVisible)
        {
            HideOverlay();
            e.Handled = true;
        }
    }

    // ── Multi-monitor: center on the monitor where the mouse cursor is ──

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private void CenterOnCurrentMonitor()
    {
        if (!GetCursorPos(out var cursor))
            return;

        var hMonitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref mi))
            return;

        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        double workLeft   = mi.rcWork.Left   * dpiX;
        double workTop    = mi.rcWork.Top    * dpiY;
        double workWidth  = (mi.rcWork.Right  - mi.rcWork.Left) * dpiX;
        double workHeight = (mi.rcWork.Bottom - mi.rcWork.Top)  * dpiY;

        Left = workLeft + (workWidth  - Width)  / 2;
        Top  = workTop  + (workHeight - Height) / 2;
    }
}
