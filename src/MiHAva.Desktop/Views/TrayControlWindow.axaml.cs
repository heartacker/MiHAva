using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace MiHAva.Desktop.Views;

public partial class TrayControlWindow : Window
{
    public Action? RequestOpenMainWindow { get; set; }

    public TrayControlWindow()
    {
        InitializeComponent();
        Deactivated += (s, e) =>
        {
            // 点击外部失去焦点时自动隐藏，提供类似系统气泡的原生体验
            Hide();
        };
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnOpenMainWindowClicked(object? sender, RoutedEventArgs e)
    {
        Hide();
        RequestOpenMainWindow?.Invoke();
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
