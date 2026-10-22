using System;
using Avalonia.Controls;
using MiHAva.Desktop.Views;
using MiHAva.UI;
using MiHAva.UI.ViewModels;
using MiHAva.UI.Views;

namespace MiHAva.Desktop.Views;

public partial class DesktopMainWindow : Window
{
    private DesktopBoardWindow? _boardWindow;

    public DesktopMainWindow()
    {
        InitializeComponent();
        _ = InitializeAppContentAsync();
    }

    private async Task InitializeAppContentAsync()
    {
        var initialVm = await MiHAvaApplication.ResolveInitialViewModelAsync(onLoggedIn: mainVm =>
        {
            SwitchToMainView(mainVm);
        });

        if (initialVm is MainViewModel mainVm)
        {
            SwitchToMainView(mainVm);
        }
        else if (initialVm is LoginViewModel loginVm)
        {
            var loginView = new LoginView { DataContext = loginVm };
            RootContent.Content = loginView;
        }
    }

    private void SwitchToMainView(MainViewModel mainVm)
    {
        var mainView = new MainView { DataContext = mainVm };

        // 订阅详情弹窗请求
        mainVm.OpenDeviceDetailRequested += OnOpenDeviceDetail;

        // 订阅桌面挂件切换请求
        mainVm.ToggleDesktopBoardRequested += OnToggleDesktopBoard;

        RootContent.Content = mainView;
    }

    private void OnOpenDeviceDetail(DeviceDetailViewModel detailVm)
    {
        var detailView = new DeviceDetailView { DataContext = detailVm };

        var dialog = new Window
        {
            Title = $"{detailVm.DeviceName} - 设备控制",
            Width = 620,
            Height = 720,
            MinWidth = 500,
            MinHeight = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = detailView
        };

        dialog.ShowDialog(this);
    }

    private void OnToggleDesktopBoard()
    {
        if (_boardWindow == null || !_boardWindow.IsVisible)
        {
            _boardWindow = new DesktopBoardWindow();
            if (RootContent.Content is MainView { DataContext: MainViewModel mainVm })
            {
                _boardWindow.DataContext = mainVm;
            }
            _boardWindow.Closed += (s, e) => _boardWindow = null;
            _boardWindow.Show();
        }
        else
        {
            _boardWindow.Activate();
        }
    }
}
