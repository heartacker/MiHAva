using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MiHAva.UI.ViewModels;

namespace MiHAva.UI.Controls;

public partial class DeviceCardView : UserControl
{
    public DeviceCardView()
    {
        InitializeComponent();
    }

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 点击源若是 ToggleSwitch，则不触发弹窗
        var source = e.Source as Visual;
        while (source != null && source != this)
        {
            if (source is ToggleSwitch)
            {
                return;
            }
            source = source.GetVisualParent();
        }

        if (DataContext is DeviceCardViewModel vm)
        {
            vm.OpenDetail();
        }
    }
}
