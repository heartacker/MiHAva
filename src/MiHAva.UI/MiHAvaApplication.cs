using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using MiHAva.UI.ViewModels;
using MiHAva.UI.Views;
using MiJia.Net.Auth;
using MiJia.Net.Cloud;
using MiJia.Net.Models.Auth;
using MiJia.Net.Spec;
using MiJia.Net.State;

namespace MiHAva.UI;

/// <summary>
/// 全平台通用的 MiHAva 共享应用层 (Desktop, Android, iOS, Browser 均通用)
/// </summary>
public partial class MiHAvaApplication : Application
{
    public static readonly IMiAuthService AuthService = new MiAuthService();
    public static readonly IMiotSpecClient SpecClient = new MiotSpecClient();
    public static readonly IDeviceStateCoordinator Coordinator = new DeviceStateCoordinator();

    public static MainViewModel? SharedMainViewModel { get; private set; }

    public MiHAvaApplication()
    {
        Name = "MiHAva - 米家跨平台客户端";
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        base.Initialize();
    }

    /// <summary>
    /// 初始化用户会话，若已登录返回 MainViewModel，否则返回 LoginViewModel
    /// </summary>
    public static async Task<object> ResolveInitialViewModelAsync(Action<MainViewModel>? onLoggedIn = null)
    {
        var tokens = await AuthService.LoadSavedTokensAsync();

        if (tokens != null && tokens.IsValid)
        {
            var mainVm = CreateMainViewModel(tokens);
            SharedMainViewModel = mainVm;
            _ = mainVm.InitializeAsync();
            return mainVm;
        }

        var loginVm = new LoginViewModel(AuthService);
        loginVm.LoginSucceeded += newTokens =>
        {
            var mainVm = CreateMainViewModel(newTokens);
            SharedMainViewModel = mainVm;
            _ = mainVm.InitializeAsync();
            onLoggedIn?.Invoke(mainVm);
        };
        _ = loginVm.StartLoginAsync();
        return loginVm;
    }

    public static MainViewModel CreateMainViewModel(MiAuthTokens tokens)
    {
        var cloudClient = new MiCloudClient(tokens);
        var jobChannel = new DeviceJobChannel(cloudClient, Coordinator);
        var mainVm = new MainViewModel(cloudClient, SpecClient, jobChannel, Coordinator);
        mainVm.UserIdText = string.IsNullOrEmpty(tokens.UserId) ? "米家用户" : $"UID: {tokens.UserId}";
        return mainVm;
    }
}
