using Avalonia.Controls;
using Avalonia.Interactivity;
using MiHAva.UI.ViewModels;

namespace MiHAva.UI.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is LoginViewModel vm)
        {
            _ = vm.LoadSavedAccountsAsync();
        }
    }
}
