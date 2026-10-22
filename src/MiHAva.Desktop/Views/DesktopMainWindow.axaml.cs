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
    public bool IsExplicitShutdown { get; set; } = false;

    public DesktopMainWindow()
    {
        InitializeComponent();
        _ = InitializeAppContentAsync();

        // 拦截右上角关闭按钮，平滑最小化至系统托盘常驻
        Closing += (s, e) =>
        {
            if (!IsExplicitShutdown)
            {
                e.Cancel = true;
                Hide();
            }
        };
    }

    private async Task InitializeAppContentAsync()
    {
        var initialVm = await MiHAvaApplication.ResolveInitialViewModelAsync(onLoggedIn: mainVm =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SwitchToMainView(mainVm));
        });

        if (initialVm is MainViewModel mainVm)
        {
            SwitchToMainView(mainVm);
        }
        else if (initialVm is LoginViewModel loginVm)
        {
            ShowLoginView(loginVm);
        }
    }

    private void ShowLoginView(LoginViewModel loginVm)
    {
        loginVm.CancelRequested += () =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                var accounts = await MiHAvaApplication.AuthService.GetSavedAccountsAsync();
                if (accounts.Count > 0)
                {
                    // 若有已保存账号，默认切换到首个账号
                    var firstAccount = accounts[0];
                    _ = MiHAvaApplication.AuthService.SaveTokensAsync(firstAccount);
                    var mainVm = MiHAvaApplication.CreateMainViewModel(firstAccount);
                    _ = mainVm.InitializeAsync();
                    SwitchToMainView(mainVm);
                }
                else
                {
                    // 若没有任何账号，显示模糊化家庭预览界面，点击提示二维码登录
                    ShowGuestHomeView();
                }
            });
        };

        var loginView = new LoginView { DataContext = loginVm };
        RootContent.Content = loginView;
    }

    private void ShowGuestHomeView()
    {
        var guestView = new GuestHomeView();
        guestView.RequestLogin += () =>
        {
            var loginVm = new LoginViewModel(MiHAvaApplication.AuthService);
            loginVm.LoginSucceeded += newTokens =>
            {
                var mainVm = MiHAvaApplication.CreateMainViewModel(newTokens);
                _ = mainVm.InitializeAsync();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => SwitchToMainView(mainVm));
            };
            ShowLoginView(loginVm);
            _ = loginVm.StartLoginAsync();
        };
        RootContent.Content = guestView;
    }

    private void SwitchToMainView(MainViewModel mainVm)
    {
        var mainView = new MainView { DataContext = mainVm };

        // 订阅详情弹窗请求
        mainVm.OpenDeviceDetailRequested += OnOpenDeviceDetail;

        // 订阅桌面挂件切换请求
        mainVm.ToggleDesktopBoardRequested += OnToggleDesktopBoard;

        // 订阅退出登录与切换账号请求
        mainVm.LogoutRequested += OnLogout;
        mainVm.SwitchAccountRequested += OnSwitchAccount;

        RootContent.Content = mainView;
    }

    private void OnLogout()
    {
        // 彻底注销：清理所有保存的凭据（包括 auth.json 和 auth_accounts.json）与设备缓存
        MiHAvaApplication.AuthService.ClearSavedTokens();
        MiHAva.UI.Services.DeviceCacheService.ClearCache();

        // 退出登录后直接显示未登录访客界面（模糊主页，点击提示扫码登录）
        ShowGuestHomeView();
    }

    private void OnSwitchAccount()
    {
        // 切换账号 / 登录新账号：保留历史账号列表，进入登录页
        var loginVm = new LoginViewModel(MiHAvaApplication.AuthService);
        loginVm.LoginSucceeded += newTokens =>
        {
            var mainVm = MiHAvaApplication.CreateMainViewModel(newTokens);
            _ = mainVm.InitializeAsync();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SwitchToMainView(mainVm));
        };

        ShowLoginView(loginVm);
        _ = loginVm.StartLoginAsync();
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
