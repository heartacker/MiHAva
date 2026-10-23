using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using MiHAva.UI.ViewModels;
using MiHAva.UI.Views;

namespace MiHAva.UI.Views;

/// <summary>
/// 适用于移动端 (Android / iOS) 及单视图平台 (Browser) 的全屏容器，管理视图切换、弹窗覆盖及登录生命周期
/// </summary>
public class SingleViewShell : Grid
{
    private readonly ContentControl _mainContent = new()
    {
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch
    };

    private readonly Grid _overlayContainer = new()
    {
        IsVisible = false,
        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(160, 0, 0, 0))
    };

    public SingleViewShell()
    {
        Children.Add(_mainContent);
        Children.Add(_overlayContainer);
    }

    public async Task InitializeAsync()
    {
        var initialVm = await MiHAvaApplication.ResolveInitialViewModelAsync(onLoggedIn: mainVm =>
        {
            Dispatcher.UIThread.Post(() => SwitchToMainView(mainVm));
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

    public void SwitchToMainView(MainViewModel mainVm)
    {
        CloseOverlay();

        var mainView = new MainView { DataContext = mainVm };

        mainVm.OpenDeviceDetailRequested += OnOpenDeviceDetail;
        mainVm.LogoutRequested += OnLogout;
        mainVm.SwitchAccountRequested += OnSwitchAccount;

        _mainContent.Content = mainView;
    }

    public void ShowLoginView(LoginViewModel loginVm)
    {
        CloseOverlay();

        loginVm.CancelRequested += () =>
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var accounts = await MiHAvaApplication.AuthService.GetSavedAccountsAsync();
                if (accounts.Count > 0)
                {
                    var firstAccount = accounts[0];
                    await MiHAvaApplication.AuthService.SaveTokensAsync(firstAccount);
                    var mainVm = MiHAvaApplication.CreateMainViewModel(firstAccount);
                    _ = mainVm.InitializeAsync();
                    SwitchToMainView(mainVm);
                }
                else
                {
                    ShowGuestHomeView();
                }
            });
        };

        _mainContent.Content = new LoginView { DataContext = loginVm };
    }

    public void ShowGuestHomeView()
    {
        CloseOverlay();

        var guestView = new GuestHomeView();
        guestView.RequestLogin += () =>
        {
            var loginVm = new LoginViewModel(MiHAvaApplication.AuthService);
            loginVm.LoginSucceeded += newTokens =>
            {
                var mainVm = MiHAvaApplication.CreateMainViewModel(newTokens);
                _ = mainVm.InitializeAsync();
                Dispatcher.UIThread.Post(() => SwitchToMainView(mainVm));
            };
            ShowLoginView(loginVm);
            _ = loginVm.StartLoginAsync();
        };

        _mainContent.Content = guestView;
    }

    private void OnLogout()
    {
        MiHAvaApplication.AuthService.ClearSavedTokens();
        MiHAva.UI.Services.DeviceCacheService.ClearCache();
        ShowGuestHomeView();
    }

    private void OnSwitchAccount()
    {
        var loginVm = new LoginViewModel(MiHAvaApplication.AuthService);
        loginVm.LoginSucceeded += newTokens =>
        {
            var mainVm = MiHAvaApplication.CreateMainViewModel(newTokens);
            _ = mainVm.InitializeAsync();
            Dispatcher.UIThread.Post(() => SwitchToMainView(mainVm));
        };

        ShowLoginView(loginVm);
        _ = loginVm.StartLoginAsync();
    }

    private void OnOpenDeviceDetail(DeviceDetailViewModel detailVm)
    {
        // 移动端全屏卡片覆盖层，带圆角与关闭按钮
        var detailView = new DeviceDetailView { DataContext = detailVm };

        var closeButton = new Button
        {
            Content = "关闭",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 0, 16, 12),
            Padding = new Avalonia.Thickness(16, 8)
        };
        closeButton.Click += (s, e) => CloseOverlay();

        var topBar = new StackPanel
        {
            Children = { closeButton }
        };
        DockPanel.SetDock(topBar, Dock.Top);

        var dockPanel = new DockPanel();
        dockPanel.Children.Add(topBar);
        dockPanel.Children.Add(detailView);

        var cardBorder = new Border
        {
            Background = (Avalonia.Media.IBrush?)this.FindResource("SystemAltHighColor") ?? Avalonia.Media.Brushes.White,
            CornerRadius = new Avalonia.CornerRadius(16, 16, 0, 0),
            Margin = new Avalonia.Thickness(0, 48, 0, 0),
            Padding = new Avalonia.Thickness(16),
            Child = dockPanel
        };

        _overlayContainer.Children.Clear();
        _overlayContainer.Children.Add(cardBorder);
        _overlayContainer.IsVisible = true;
    }

    public bool HandleBackButton()
    {
        if (_overlayContainer.IsVisible)
        {
            CloseOverlay();
            return true;
        }
        return false;
    }

    public void CloseOverlay()
    {
        _overlayContainer.IsVisible = false;
        _overlayContainer.Children.Clear();
    }
}
