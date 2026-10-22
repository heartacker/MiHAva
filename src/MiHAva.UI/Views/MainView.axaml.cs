using Avalonia.Controls;
using Avalonia.Input;
using MiHAva.UI.ViewModels;

namespace MiHAva.UI.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsHomeSwitcherOpen = false;
        }
    }

    private void OnHomeItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is HomeCategoryItem home && DataContext is MainViewModel vm)
        {
            vm.SelectHome(home);
        }
    }
}
