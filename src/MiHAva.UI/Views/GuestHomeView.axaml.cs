using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MiHAva.UI.Views;

public partial class GuestHomeView : UserControl
{
    public event Action? RequestLogin;

    public GuestHomeView()
    {
        InitializeComponent();
    }

    private void OnPromptLoginClicked(object? sender, RoutedEventArgs e)
    {
        RequestLogin?.Invoke();
    }
}
