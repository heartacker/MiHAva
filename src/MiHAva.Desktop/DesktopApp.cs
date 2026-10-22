using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MiHAva.Desktop.Services;
using MiHAva.Desktop.Views;
using MiHAva.UI;
using MiHAva.UI.ViewModels;

namespace MiHAva.Desktop;

public partial class DesktopApp : MiHAvaApplication
{
    private TrayIcon? _trayIcon;
    private TrayControlWindow? _trayWindow;
    private DesktopMainWindow? _mainWindow;

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _mainWindow = new DesktopMainWindow();
            desktop.MainWindow = _mainWindow;

            // 初始化系统托盘
            SetupSystemTray(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupSystemTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var appIcon = DesktopIconHelper.GetOrCreateAppIcon();
        if (_mainWindow != null)
        {
            _mainWindow.Icon = appIcon;
        }

        // 创建原生托盘右键菜单
        var nativeMenu = new NativeMenu();

        var openMainItem = new NativeMenuItem("打开主工作台");
        openMainItem.Click += (s, e) => ShowMainWindow();

        var boardItem = new NativeMenuItem("桌面常驻挂件");
        boardItem.Click += (s, e) =>
        {
            if (SharedMainViewModel != null)
            {
                SharedMainViewModel.ToggleDesktopBoard();
            }
        };

        var refreshItem = new NativeMenuItem("刷新所有设备");
        refreshItem.Click += (s, e) =>
        {
            if (SharedMainViewModel != null)
            {
                _ = SharedMainViewModel.RefreshAllAsync();
            }
        };

        var exitItem = new NativeMenuItem("退出米家");
        exitItem.Click += (s, e) => desktop.Shutdown();

        nativeMenu.Items.Add(openMainItem);
        nativeMenu.Items.Add(boardItem);
        nativeMenu.Items.Add(refreshItem);
        nativeMenu.Items.Add(new NativeMenuItemSeparator());
        nativeMenu.Items.Add(exitItem);

        // 初始化 TrayIcon
        _trayIcon = new TrayIcon
        {
            Icon = appIcon,
            ToolTipText = "MiHAva - 米家客户端",
            Menu = nativeMenu,
            IsVisible = true
        };

        // 托盘点击事件：呼出快捷气泡窗口
        _trayIcon.Clicked += (s, e) => ToggleTrayControlWindow();

        var trayIcons = new TrayIcons { _trayIcon };
        SetValue(TrayIcon.IconsProperty, trayIcons);
    }

    private void ToggleTrayControlWindow()
    {
        if (_trayWindow == null)
        {
            _trayWindow = new TrayControlWindow
            {
                RequestOpenMainWindow = ShowMainWindow
            };
        }

        if (_trayWindow.IsVisible)
        {
            _trayWindow.Hide();
            return;
        }

        if (SharedMainViewModel != null)
        {
            _trayWindow.DataContext = SharedMainViewModel;
        }

        // 将快捷气泡显示在屏幕工作区右下角
        if (_mainWindow?.Screens?.Primary is { } primaryScreen)
        {
            var workingArea = primaryScreen.WorkingArea;
            _trayWindow.Position = new PixelPoint(
                (int)(workingArea.X + workingArea.Width - _trayWindow.Width - 16),
                (int)(workingArea.Y + workingArea.Height - _trayWindow.Height - 16));
        }

        _trayWindow.Show();
        _trayWindow.Activate();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new DesktopMainWindow();
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }
}
