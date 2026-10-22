using Avalonia.Controls;
using MiHAva.UI.ViewModels;

namespace MiHAva.UI.Views;

public partial class DeviceDetailView : UserControl
{
    public DeviceDetailView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is DeviceDetailViewModel vm)
        {
            _ = vm.LoadPropertiesAsync();
        }
    }
}
