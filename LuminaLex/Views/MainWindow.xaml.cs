using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace LuminaLex.Views;

public partial class MainWindow : Window
{
    private bool _isVisible;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void ToggleOverlay()
    {
        if (_isVisible)
            HideOverlay();
        else
            ShowOverlay();
    }

    public void ShowOverlay()
    {
        if (_isVisible) return;
        _isVisible = true;
        Show();
        Activate();

        // Fade in
        var anim = new DoubleAnimation(0, 0.94, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, anim);
    }

    public void HideOverlay()
    {
        if (!_isVisible) return;
        _isVisible = false;

        // Fade out then hide
        var anim = new DoubleAnimation(0.94, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) =>
        {
            if (!_isVisible) Hide();
        };
        BeginAnimation(OpacityProperty, anim);
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
}
