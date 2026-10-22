using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MiHAva.Desktop.Views;

public partial class DesktopBoardWindow : Window
{
    public DesktopBoardWindow()
    {
        InitializeComponent();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 允许用户按住挂件空白背景任意拖拽移动窗口
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
